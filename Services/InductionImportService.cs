using System.Globalization;
using System.Data;
using System.Net.Mail;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Services;

public interface IInductionImportService
{
    Task<InductionImportResult> ImportAsync(
        Stream workbookStream,
        string createdBy,
        CancellationToken cancellationToken = default);
}

public class InductionImportService(
    ApplicationDbContext dbContext,
    IInductionWorkflowService workflow) : IInductionImportService
{
    private const int MaximumRows = 500;
    private const string WorksheetName = "Induction Plans";
    private static readonly string[] ExpectedHeaders =
    [
        "Employee Title",
        "First Name",
        "Last Name",
        "Gender",
        "Position",
        "Employee AD ID",
        "Employee Email Address",
        "Employee Number",
        "Department",
        "Division",
        "Appointment Date",
        "Line Manager Name",
        "Line Manager AD ID",
        "HR Business Partner Name",
        "HR Business Partner AD ID"
    ];

    public async Task<InductionImportResult> ImportAsync(
        Stream workbookStream,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(workbookStream);
        if (!workbook.TryGetWorksheet(WorksheetName, out var worksheet))
        {
            return InductionImportResult.Invalid($"The workbook must contain a worksheet named '{WorksheetName}'. Download a new template and try again.");
        }

        var headerError = ValidateHeaders(worksheet);
        if (headerError is not null)
        {
            return InductionImportResult.Invalid(headerError);
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var dataRows = Enumerable.Range(2, Math.Max(0, lastRow - 1))
            .Where(row => !IsBlankRow(worksheet, row))
            .ToList();

        if (dataRows.Count == 0)
        {
            return InductionImportResult.Invalid("No employee rows were found in the Induction Plans worksheet.");
        }

        if (dataRows.Count > MaximumRows)
        {
            return InductionImportResult.Invalid($"The workbook contains {dataRows.Count} employee rows. Upload a maximum of {MaximumRows} rows at a time.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var masterItems = await dbContext.MasterDataItems
            .Where(x => x.Category == MasterDataCategory.EmployeeTitle
                     || x.Category == MasterDataCategory.Gender
                     || x.Category == MasterDataCategory.Department
                     || x.Category == MasterDataCategory.Division)
            .ToListAsync(cancellationToken);

        var titles = MasterLookup(masterItems.Where(x => x.IsActive), MasterDataCategory.EmployeeTitle);
        var genders = MasterLookup(masterItems.Where(x => x.IsActive), MasterDataCategory.Gender);
        var departments = MasterItemLookup(masterItems, MasterDataCategory.Department);
        var divisions = MasterItemLookup(masterItems, MasterDataCategory.Division);
        var nextDepartmentSortOrder = masterItems
            .Where(x => x.Category == MasterDataCategory.Department)
            .Select(x => x.SortOrder)
            .DefaultIfEmpty()
            .Max();
        var nextDivisionSortOrder = masterItems
            .Where(x => x.Category == MasterDataCategory.Division)
            .Select(x => x.SortOrder)
            .DefaultIfEmpty()
            .Max();

        var existingKeys = await dbContext.InductionCases
            .AsNoTracking()
            .Select(x => new { x.EmployeeUsername, x.EmployeeNumber })
            .ToListAsync(cancellationToken);
        var employeeAdIds = existingKeys
            .Select(x => x.EmployeeUsername)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var employeeNumbers = existingKeys
            .Select(x => x.EmployeeNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new InductionImportResult { WasProcessed = true };
        foreach (var rowNumber in dataRows)
        {
            var values = ReadRow(worksheet, rowNumber);
            var employeeLabel = string.Join(" ", new[] { values[1], values[2] }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            if (string.IsNullOrWhiteSpace(employeeLabel)) employeeLabel = $"Row {rowNumber}";

            var errors = new List<string>();
            var title = Required(values[0], "Employee Title", 30, errors);
            var firstName = Required(values[1], "First Name", 100, errors);
            var lastName = Required(values[2], "Last Name", 100, errors);
            var gender = Required(values[3], "Gender", 50, errors);
            var position = Required(values[4], "Position", 150, errors);
            var employeeAdId = RequiredAdId(values[5], "Employee AD ID", errors);
            var employeeEmail = RequiredEmail(values[6], "Employee Email Address", errors);
            var employeeNumber = Optional(values[7], "Employee Number", 40, errors);
            var department = Required(values[8], "Department", 120, errors);
            var division = Required(values[9], "Division", 120, errors);
            var appointmentDate = ReadDate(worksheet.Cell(rowNumber, 11), errors);
            var lineManagerName = Required(values[11], "Line Manager Name", 150, errors);
            var lineManagerAdId = RequiredAdId(values[12], "Line Manager AD ID", errors);
            var hrBusinessPartnerName = Required(values[13], "HR Business Partner Name", 150, errors);
            var hrBusinessPartnerAdId = RequiredAdId(values[14], "HR Business Partner AD ID", errors);

            title = CanonicalMasterValue(title, titles, "Employee Title", errors);
            gender = CanonicalMasterValue(gender, genders, "Gender", errors);
            department = CanonicalManagedMasterValue(department, departments);
            division = CanonicalManagedMasterValue(division, divisions);

            var employeeName = $"{title} {firstName} {lastName}".Trim();
            if (employeeName.Length > 150)
            {
                errors.Add("The combined title, first name and last name exceeds 150 characters.");
            }

            if (errors.Count > 0)
            {
                result.FailedCount++;
                result.Rows.Add(new InductionImportRowResult(rowNumber, employeeLabel, "Failed", string.Join(" ", errors)));
                continue;
            }

            var duplicateReason = DuplicateReason(employeeAdId, employeeNumber, employeeAdIds, employeeNumbers);
            if (duplicateReason is not null)
            {
                result.DuplicateCount++;
                result.Rows.Add(new InductionImportRowResult(rowNumber, employeeLabel, "Duplicate", duplicateReason));
                continue;
            }

            var administrationChanges = new List<string>();
            department = EnsureImportedMasterValue(
                department,
                MasterDataCategory.Department,
                departments,
                ref nextDepartmentSortOrder,
                createdBy,
                out var departmentChange);
            RecordMasterDataChange("Department", departmentChange, result, administrationChanges);

            division = EnsureImportedMasterValue(
                division,
                MasterDataCategory.Division,
                divisions,
                ref nextDivisionSortOrder,
                createdBy,
                out var divisionChange);
            RecordMasterDataChange("Division", divisionChange, result, administrationChanges);

            var inductionCase = new InductionCase
            {
                EmployeeTitle = title,
                FirstName = firstName,
                LastName = lastName,
                EmployeeName = employeeName,
                Gender = gender,
                Position = position,
                EmployeeUsername = employeeAdId,
                EmployeeEmail = employeeEmail,
                EmployeeNumber = employeeNumber,
                Department = department,
                Division = division,
                AppointmentDate = appointmentDate!.Value.Date,
                LineManagerName = lineManagerName,
                LineManagerUsername = lineManagerAdId,
                HrBusinessPartnerName = hrBusinessPartnerName,
                HrBusinessPartnerUsername = hrBusinessPartnerAdId,
                CreatedBy = createdBy
            };

            await workflow.AddTasksFromTemplateAsync(inductionCase, cancellationToken);
            dbContext.InductionCases.Add(inductionCase);
            employeeAdIds.Add(employeeAdId);
            if (!string.IsNullOrWhiteSpace(employeeNumber)) employeeNumbers.Add(employeeNumber);
            result.ImportedCount++;
            var rowMessage = administrationChanges.Count == 0
                ? "Induction plan created."
                : $"Induction plan created. {string.Join(" ", administrationChanges)}";
            result.Rows.Add(new InductionImportRowResult(rowNumber, employeeName, "Imported", rowMessage));
        }

        if (result.ImportedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        result.Message = result.ImportedCount == 0
            ? "No new induction plans were imported. Review the duplicate and failed rows below."
            : $"Imported {result.ImportedCount} induction plan{(result.ImportedCount == 1 ? string.Empty : "s")}.";
        if (result.MasterDataAddedCount > 0)
        {
            result.Message += $" Added {result.MasterDataAddedCount} new department or division value{(result.MasterDataAddedCount == 1 ? string.Empty : "s")} to Administration.";
        }
        if (result.MasterDataReactivatedCount > 0)
        {
            result.Message += $" Reactivated {result.MasterDataReactivatedCount} archived department or division value{(result.MasterDataReactivatedCount == 1 ? string.Empty : "s")}.";
        }
        return result;
    }

    private static string? ValidateHeaders(IXLWorksheet worksheet)
    {
        for (var column = 1; column <= ExpectedHeaders.Length; column++)
        {
            var actual = worksheet.Cell(1, column).GetFormattedString().Trim();
            if (!string.Equals(actual, ExpectedHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
            {
                return $"Column {column} must be named '{ExpectedHeaders[column - 1]}'. Download a new template and do not change the headings.";
            }
        }
        return null;
    }

    private static bool IsBlankRow(IXLWorksheet worksheet, int rowNumber) =>
        Enumerable.Range(1, ExpectedHeaders.Length)
            .All(column => string.IsNullOrWhiteSpace(worksheet.Cell(rowNumber, column).GetFormattedString()));

    private static string[] ReadRow(IXLWorksheet worksheet, int rowNumber) =>
        Enumerable.Range(1, ExpectedHeaders.Length)
            .Select(column => worksheet.Cell(rowNumber, column).GetFormattedString().Trim())
            .ToArray();

    private static Dictionary<string, string> MasterLookup(
        IEnumerable<MasterDataItem> items,
        MasterDataCategory category) =>
        items.Where(x => x.Category == category)
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Name.Trim(), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, MasterDataItem> MasterItemLookup(
        IEnumerable<MasterDataItem> items,
        MasterDataCategory category) =>
        items.Where(x => x.Category == category)
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

    private static string CanonicalManagedMasterValue(
        string value,
        IReadOnlyDictionary<string, MasterDataItem> existingValues)
    {
        if (value.Length == 0) return value;
        return existingValues.TryGetValue(value, out var existing) ? existing.Name.Trim() : value;
    }

    private string EnsureImportedMasterValue(
        string value,
        MasterDataCategory category,
        IDictionary<string, MasterDataItem> existingValues,
        ref int nextSortOrder,
        string updatedBy,
        out MasterDataImportChange change)
    {
        if (existingValues.TryGetValue(value, out var existing))
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.UpdatedBy = updatedBy;
                change = MasterDataImportChange.Reactivated;
            }
            else
            {
                change = MasterDataImportChange.None;
            }

            return existing.Name.Trim();
        }

        var item = new MasterDataItem
        {
            Category = category,
            Name = value,
            SortOrder = ++nextSortOrder,
            IsActive = true,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };
        dbContext.MasterDataItems.Add(item);
        existingValues.Add(value, item);
        change = MasterDataImportChange.Added;
        return item.Name;
    }

    private static void RecordMasterDataChange(
        string label,
        MasterDataImportChange change,
        InductionImportResult result,
        ICollection<string> rowMessages)
    {
        switch (change)
        {
            case MasterDataImportChange.Added:
                result.MasterDataAddedCount++;
                rowMessages.Add($"{label} added to Administration.");
                break;
            case MasterDataImportChange.Reactivated:
                result.MasterDataReactivatedCount++;
                rowMessages.Add($"{label} reactivated in Administration.");
                break;
        }
    }

    private static string Required(string value, string field, int maximumLength, ICollection<string> errors)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0) errors.Add($"{field} is required.");
        else if (normalized.Length > maximumLength) errors.Add($"{field} exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string value, string field, int maximumLength, ICollection<string> errors)
    {
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) errors.Add($"{field} exceeds {maximumLength} characters.");
        return normalized.Length == 0 ? null : normalized;
    }

    private static string RequiredAdId(string value, string field, ICollection<string> errors)
    {
        var normalized = Required(value, field, 150, errors);
        if (normalized.Length > 0 && !Regex.IsMatch(normalized, @"^[^\\]+\\[^\\]+$"))
        {
            errors.Add($"{field} must use DOMAIN\\username.");
        }
        return normalized;
    }

    private static string RequiredEmail(string value, string field, ICollection<string> errors)
    {
        var normalized = Required(value, field, 254, errors);
        if (normalized.Length > 0
            && (!MailAddress.TryCreate(normalized, out var address)
                || !string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"{field} must be a valid email address.");
        }
        return normalized;
    }

    private static string CanonicalMasterValue(
        string value,
        IReadOnlyDictionary<string, string> activeValues,
        string field,
        ICollection<string> errors)
    {
        if (value.Length == 0) return value;
        if (activeValues.TryGetValue(value, out var canonical)) return canonical;
        errors.Add($"{field} '{value}' is not active in Administration.");
        return value;
    }

    private static DateTime? ReadDate(IXLCell cell, ICollection<string> errors)
    {
        if (cell.TryGetValue<DateTime>(out var typedDate)) return typedDate.Date;

        var text = cell.GetFormattedString().Trim();
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd MMM yyyy", "d MMM yyyy" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exactDate)
            || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out exactDate))
        {
            return exactDate.Date;
        }

        errors.Add("Appointment Date is required and must be a valid date.");
        return null;
    }

    private static string? DuplicateReason(
        string employeeAdId,
        string? employeeNumber,
        IReadOnlySet<string> employeeAdIds,
        IReadOnlySet<string> employeeNumbers)
    {
        if (employeeAdIds.Contains(employeeAdId)) return $"Employee AD ID '{employeeAdId}' already has an induction plan.";
        if (!string.IsNullOrWhiteSpace(employeeNumber) && employeeNumbers.Contains(employeeNumber))
        {
            return $"Employee Number '{employeeNumber}' already has an induction plan.";
        }
        return null;
    }

    private enum MasterDataImportChange
    {
        None,
        Added,
        Reactivated
    }
}
