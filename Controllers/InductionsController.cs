using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Principal;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class InductionsController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService,
    IInductionWorkflowService workflow,
    IInductionImportService importService,
    IInductionReportService reportService,
    IInductionEmailService emailService,
    IWorkflowConfigurationService configurationService,
    IWebHostEnvironment environment,
    ILogger<InductionsController> logger) : Controller
{
    public async Task<IActionResult> Index(string? search, InductionStatus? status)
    {
        var username = accessService.CurrentUsername(User);
        var isHr = accessService.IsHr(User);
        var query = dbContext.InductionCases
            .AsNoTracking()
            .Include(x => x.Tasks)
            .AsQueryable();

        if (!isHr)
        {
            query = query.Where(x =>
                x.EmployeeUsername == username
                || x.LineManagerUsername == username
                || x.HrBusinessPartnerUsername == username
                || x.Tasks.Any(t => t.AssignedToUsername == username));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.EmployeeName.Contains(term)
                || x.FirstName.Contains(term)
                || x.LastName.Contains(term)
                || (x.EmployeeEmail != null && x.EmployeeEmail.Contains(term))
                || x.Position.Contains(term)
                || x.Department.Contains(term)
                || x.Division.Contains(term)
                || (x.EmployeeNumber != null && x.EmployeeNumber.Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.IsHr = isHr;
        ViewBag.SmtpConfigured = emailService.ConfigurationStatus.IsConfigured;
        ViewBag.SmtpSummary = emailService.ConfigurationStatus.Summary;
        return View(await query.OrderByDescending(x => x.AppointmentDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!accessService.IsHr(User)) return Forbid();

        var username = accessService.CurrentUsername(User);
        var model = new CreateInductionViewModel
        {
            AppointmentDate = DateTime.Today,
            HrBusinessPartnerName = User.Identity?.Name ?? string.Empty,
            HrBusinessPartnerUsername = username
        };
        await LoadCreateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInductionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var employeeTitle = model.EmployeeTitle?.Trim() ?? string.Empty;
        var firstName = model.FirstName?.Trim() ?? string.Empty;
        var lastName = model.LastName?.Trim() ?? string.Empty;
        var employeeName = $"{employeeTitle} {firstName} {lastName}".Trim();
        if (employeeName.Length > 150)
        {
            ModelState.AddModelError(nameof(model.LastName), "The combined title, first name and last name must not exceed 150 characters.");
        }
        var employeeAdId = model.EmployeeUsername?.Trim();
        var employeeNumber = model.EmployeeNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(employeeAdId)
            && await dbContext.InductionCases.AnyAsync(x =>
                x.EmployeeUsername == employeeAdId
                || (!string.IsNullOrWhiteSpace(employeeNumber) && x.EmployeeNumber == employeeNumber)))
        {
            ModelState.AddModelError(nameof(model.EmployeeUsername), "An induction plan already exists for this Employee AD ID or Employee Number.");
        }
        await ValidateMasterDataSelectionAsync(model);
        await ValidateProgrammeSelectionAsync(model.InductionProgrammeId);
        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync(model);
            return View(model);
        }

        var inductionCase = new InductionCase
        {
            EmployeeTitle = employeeTitle,
            FirstName = firstName,
            LastName = lastName,
            EmployeeName = employeeName,
            Gender = model.Gender.Trim(),
            Position = model.Position.Trim(),
            EmployeeUsername = employeeAdId!,
            EmployeeEmail = model.EmployeeEmail.Trim(),
            EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber,
            Department = model.Department.Trim(),
            Division = model.Division.Trim(),
            AppointmentDate = model.AppointmentDate.Date,
            InductionProgrammeId = model.InductionProgrammeId,
            LineManagerName = model.LineManagerName.Trim(),
            LineManagerUsername = model.LineManagerUsername.Trim(),
            HrBusinessPartnerName = model.HrBusinessPartnerName.Trim(),
            HrBusinessPartnerUsername = model.HrBusinessPartnerUsername.Trim(),
            CreatedBy = accessService.CurrentUsername(User)
        };

        await workflow.AddTasksFromTemplateAsync(inductionCase);
        dbContext.InductionCases.Add(inductionCase);
        await dbContext.SaveChangesAsync();

        TempData["Success"] = $"Induction plan created for {inductionCase.EmployeeName}.";
        return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var inductionCase = await dbContext.InductionCases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (inductionCase is null) return NotFound();

        var model = new EditInductionViewModel
        {
            Id = inductionCase.Id,
            EmployeeTitle = inductionCase.EmployeeTitle,
            FirstName = inductionCase.FirstName,
            LastName = inductionCase.LastName,
            Gender = inductionCase.Gender,
            Position = inductionCase.Position,
            EmployeeUsername = inductionCase.EmployeeUsername,
            EmployeeEmail = inductionCase.EmployeeEmail ?? string.Empty,
            EmployeeNumber = inductionCase.EmployeeNumber,
            Department = inductionCase.Department,
            Division = inductionCase.Division,
            AppointmentDate = inductionCase.AppointmentDate,
            InductionProgrammeId = inductionCase.InductionProgrammeId,
            LineManagerName = inductionCase.LineManagerName,
            LineManagerUsername = inductionCase.LineManagerUsername,
            HrBusinessPartnerName = inductionCase.HrBusinessPartnerName,
            HrBusinessPartnerUsername = inductionCase.HrBusinessPartnerUsername
        };
        await LoadCreateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditInductionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var inductionCase = await dbContext.InductionCases
            .Include(x => x.Tasks)
            .SingleOrDefaultAsync(x => x.Id == model.Id, HttpContext.RequestAborted);
        if (inductionCase is null) return NotFound();

        var employeeTitle = model.EmployeeTitle?.Trim() ?? string.Empty;
        var firstName = model.FirstName?.Trim() ?? string.Empty;
        var lastName = model.LastName?.Trim() ?? string.Empty;
        var employeeName = $"{employeeTitle} {firstName} {lastName}".Trim();
        var employeeAdId = model.EmployeeUsername?.Trim() ?? string.Empty;
        var employeeNumber = model.EmployeeNumber?.Trim();
        if (employeeName.Length > 150)
        {
            ModelState.AddModelError(nameof(model.LastName), "The combined title, first name and last name must not exceed 150 characters.");
        }
        if (await dbContext.InductionCases.AnyAsync(x => x.Id != model.Id
            && (x.EmployeeUsername == employeeAdId
                || (!string.IsNullOrWhiteSpace(employeeNumber) && x.EmployeeNumber == employeeNumber)), HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.EmployeeUsername), "Another induction plan already uses this Employee AD ID or Employee Number.");
        }
        await ValidateMasterDataSelectionAsync(model);
        await ValidateProgrammeSelectionAsync(model.InductionProgrammeId);
        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync(model);
            return View(model);
        }

        var previousAppointmentDate = inductionCase.AppointmentDate.Date;
        inductionCase.EmployeeTitle = employeeTitle;
        inductionCase.FirstName = firstName;
        inductionCase.LastName = lastName;
        inductionCase.EmployeeName = employeeName;
        inductionCase.Gender = model.Gender.Trim();
        inductionCase.Position = model.Position.Trim();
        inductionCase.EmployeeUsername = employeeAdId;
        inductionCase.EmployeeEmail = model.EmployeeEmail.Trim();
        inductionCase.EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber;
        inductionCase.Department = model.Department.Trim();
        inductionCase.Division = model.Division.Trim();
        inductionCase.AppointmentDate = model.AppointmentDate.Date;
        inductionCase.InductionProgrammeId = model.InductionProgrammeId;
        inductionCase.LineManagerName = model.LineManagerName.Trim();
        inductionCase.LineManagerUsername = model.LineManagerUsername.Trim();
        inductionCase.HrBusinessPartnerName = model.HrBusinessPartnerName.Trim();
        inductionCase.HrBusinessPartnerUsername = model.HrBusinessPartnerUsername.Trim();

        if (previousAppointmentDate != inductionCase.AppointmentDate.Date)
        {
            var templateOffsets = await dbContext.ChecklistTemplateItems.AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.DueOffsetDays, HttpContext.RequestAborted);
            foreach (var task in inductionCase.Tasks.Where(x => x.Status == InductionTaskStatus.NotStarted && x.ChecklistTemplateItemId.HasValue))
            {
                if (templateOffsets.TryGetValue(task.ChecklistTemplateItemId!.Value, out var offset))
                {
                    task.DueDate = inductionCase.AppointmentDate.AddDays(offset);
                }
            }
        }

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Success"] = $"Induction record updated for {inductionCase.EmployeeName}.";
        return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
    }

    [HttpGet]
    public IActionResult Import()
    {
        if (!accessService.IsHr(User)) return Forbid();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        if (!accessService.IsHr(User)) return Forbid();

        var templatePath = Path.Combine(environment.WebRootPath, "templates", "NAMFISA-Induction-Import-Template.xlsx");
        if (!System.IO.File.Exists(templatePath)) return NotFound("The induction import template is not available.");

        var masterItems = await dbContext.MasterDataItems
            .AsNoTracking()
            .Where(x => x.IsActive &&
                (x.Category == MasterDataCategory.EmployeeTitle
                 || x.Category == MasterDataCategory.Gender
                 || x.Category == MasterDataCategory.Department
                 || x.Category == MasterDataCategory.Division))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(HttpContext.RequestAborted);

        using var workbook = new XLWorkbook(templatePath);
        var referenceSheet = workbook.Worksheet("Reference Data");
        referenceSheet.Range("A2:D501").Clear(XLClearOptions.Contents);
        WriteReferenceValues(referenceSheet, 1, masterItems, MasterDataCategory.EmployeeTitle);
        WriteReferenceValues(referenceSheet, 2, masterItems, MasterDataCategory.Gender);
        WriteReferenceValues(referenceSheet, 3, masterItems, MasterDataCategory.Department);
        WriteReferenceValues(referenceSheet, 4, masterItems, MasterDataCategory.Division);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(
            output.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "NAMFISA-Induction-Import-Template.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadImport(IFormFile? file)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (file is null || file.Length == 0)
        {
            return BadRequest(InductionImportResult.Invalid("Select a completed .xlsx induction template to upload."));
        }
        if (file.Length > 10_485_760)
        {
            return BadRequest(InductionImportResult.Invalid("The workbook is larger than 10 MB."));
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(InductionImportResult.Invalid("Only .xlsx workbooks created from the induction template can be uploaded."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importService.ImportAsync(
                stream,
                accessService.CurrentUsername(User),
                HttpContext.RequestAborted);
            return result.WasProcessed ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to import induction plan workbook {FileName}.", file.FileName);
            return BadRequest(InductionImportResult.Invalid("The workbook could not be read. Download a new template, copy the employee rows into it, and try again."));
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var inductionCase = await dbContext.InductionCases
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Tasks)
            .Include(x => x.SignOffs)
            .Include(x => x.EmployeeFeedback)
                .ThenInclude(x => x!.PresentationRatings)
                    .ThenInclude(x => x.InductionProgrammeSession)
            .Include(x => x.TrainingEvaluations)
            .Include(x => x.InductionProgramme)
                .ThenInclude(x => x!.Sessions)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanView(User, inductionCase)) return Forbid();

        var completed = inductionCase.Tasks.Count(x =>
            x.Status is InductionTaskStatus.Completed or InductionTaskStatus.NotApplicable);
        var settings = await configurationService.GetAsync(HttpContext.RequestAborted);

        return View(new InductionDetailsViewModel
        {
            Induction = inductionCase,
            CompletedTaskCount = completed,
            TotalTaskCount = inductionCase.Tasks.Count,
            CanManage = accessService.IsHr(User),
            SmtpConfigured = emailService.ConfigurationStatus.IsConfigured,
            CanSubmitFeedback = inductionCase.EmployeeFeedback is null
                && inductionCase.InductionProgramme?.Sessions.Count > 0
                && accessService.CanSubmitFeedback(User, inductionCase),
            CanSignEmployee = accessService.CanSign(User, inductionCase, SignOffRole.Employee)
                && (!settings.RequireEmployeeFeedbackBeforeSignOff || inductionCase.EmployeeFeedback is not null),
            CanSignLineManager = accessService.CanSign(User, inductionCase, SignOffRole.LineManager)
                && (!settings.EnforceSequentialSignOff
                    || inductionCase.SignOffs.Any(x => x.Role == SignOffRole.Employee)),
            CanSignHrBusinessPartner = accessService.CanSign(User, inductionCase, SignOffRole.HrBusinessPartner)
                && (!settings.EnforceSequentialSignOff
                    || inductionCase.SignOffs.Any(x => x.Role == SignOffRole.LineManager)),
            LockChecklistWhenSignOffStarts = settings.LockChecklistWhenSignOffStarts,
            RequireMandatoryTasksBeforeSignOff = settings.RequireMandatoryTasksBeforeSignOff,
            RequireEmployeeFeedbackBeforeSignOff = settings.RequireEmployeeFeedbackBeforeSignOff
        });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport(int id)
    {
        var inductionCase = await dbContext.InductionCases
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Tasks)
            .Include(x => x.SignOffs)
            .Include(x => x.EmployeeFeedback)
                .ThenInclude(x => x!.PresentationRatings)
                    .ThenInclude(x => x.InductionProgrammeSession)
            .Include(x => x.TrainingEvaluations)
            .Include(x => x.InductionProgramme)
                .ThenInclude(x => x!.Sessions)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanView(User, inductionCase)) return Forbid();

        var report = reportService.Generate(inductionCase);
        var employeeName = string.Concat(inductionCase.EmployeeName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var fileName = $"Induction-report-{employeeName}-{DateTime.Today:yyyyMMdd}.pdf";
        return File(report, "application/pdf", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployeeEmail(UpdateEmployeeEmailViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();

        var inductionCase = await dbContext.InductionCases
            .SingleOrDefaultAsync(x => x.Id == model.InductionCaseId, HttpContext.RequestAborted);
        if (inductionCase is null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid employee email address.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        inductionCase.EmployeeEmail = model.EmployeeEmail.Trim();
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Success"] = $"Employee email updated for {inductionCase.EmployeeName}.";
        return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendInvitations(SendInductionInvitationsViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();

        IActionResult RedirectBack() => model.ReturnToInductionId.HasValue
            ? RedirectToAction(nameof(Details), new { id = model.ReturnToInductionId.Value })
            : RedirectToAction(nameof(Index));

        if (!ModelState.IsValid || model.InductionCaseIds.Length == 0)
        {
            TempData["Error"] = "Select at least one induction plan to invite.";
            return RedirectBack();
        }

        var smtpStatus = emailService.ConfigurationStatus;
        if (!smtpStatus.IsConfigured)
        {
            TempData["Error"] = smtpStatus.Summary;
            return RedirectBack();
        }

        var inductionIds = model.InductionCaseIds
            .Distinct()
            .Take(100)
            .ToArray();
        var inductionCases = await dbContext.InductionCases
            .Where(x => inductionIds.Contains(x.Id))
            .OrderBy(x => x.EmployeeName)
            .ToListAsync(HttpContext.RequestAborted);
        if (inductionCases.Count == 0)
        {
            TempData["Error"] = "The selected induction plans could not be found.";
            return RedirectBack();
        }

        var settings = await configurationService.GetAsync(HttpContext.RequestAborted);
        var sentCount = 0;
        var failures = new List<string>();
        var sentBy = accessService.CurrentUsername(User);

        foreach (var inductionCase in inductionCases)
        {
            if (string.IsNullOrWhiteSpace(inductionCase.EmployeeEmail))
            {
                failures.Add($"{inductionCase.EmployeeName}: employee email is missing.");
                continue;
            }

            var inductionUrl = BuildInductionUrl(inductionCase.Id);
            if (string.IsNullOrWhiteSpace(inductionUrl))
            {
                failures.Add($"{inductionCase.EmployeeName}: the induction link could not be created.");
                continue;
            }

            try
            {
                await emailService.SendInvitationAsync(
                    inductionCase,
                    inductionUrl,
                    settings.EmailSubjectTemplate,
                    settings.EmailBodyTemplate,
                    HttpContext.RequestAborted);
                inductionCase.InvitationSentAtUtc = DateTime.UtcNow;
                inductionCase.InvitationSentBy = sentBy;
                inductionCase.InvitationSentTo = inductionCase.EmployeeEmail.Trim();
                await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
                sentCount++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to send induction {InductionId} invitation to {Recipient}.",
                    inductionCase.Id, inductionCase.EmployeeEmail);
                failures.Add($"{inductionCase.EmployeeName}: delivery failed.");
            }
        }

        if (sentCount > 0)
        {
            TempData["Success"] = $"Sent {sentCount} induction invitation{(sentCount == 1 ? string.Empty : "s")} directly from the system.";
        }
        if (failures.Count > 0)
        {
            var examples = string.Join(" ", failures.Take(3));
            TempData["Error"] = $"{failures.Count} invitation{(failures.Count == 1 ? string.Empty : "s")} could not be sent. {examples}";
        }

        return RedirectBack();
    }

    private string? BuildInductionUrl(int inductionCaseId)
    {
        if (emailService.PublicBaseUrl is { Length: > 0 } publicBaseUrl)
        {
            return $"{publicBaseUrl.TrimEnd('/')}/Inductions/Details/{inductionCaseId}";
        }

        return Url.Action(
            nameof(Details),
            "Inductions",
            new { id = inductionCaseId },
            Request.Scheme,
            Request.Host.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTask(
        int taskId,
        InductionTaskStatus status,
        string? notes,
        string? assignedToUsername,
        DateTime? dueDate)
    {
        var task = await dbContext.InductionTasks
            .AsSplitQuery()
            .Include(x => x.InductionCase)
                .ThenInclude(x => x.Tasks)
            .Include(x => x.InductionCase)
                .ThenInclude(x => x.SignOffs)
            .SingleOrDefaultAsync(x => x.Id == taskId);

        if (task is null) return NotFound();
        if (!accessService.CanComplete(User, task)) return Forbid();

        var settings = await configurationService.GetAsync(HttpContext.RequestAborted);
        if (settings.LockChecklistWhenSignOffStarts && task.InductionCase.SignOffs.Count != 0)
        {
            TempData["Error"] = "This record is locked because sign-off has started.";
            return RedirectToAction(nameof(Details), new { id = task.InductionCaseId });
        }

        if (status == InductionTaskStatus.NotApplicable && string.IsNullOrWhiteSpace(notes))
        {
            TempData["Error"] = "Add a reason before marking an item not applicable.";
            return RedirectToAction(nameof(Details), new { id = task.InductionCaseId });
        }

        task.Status = status;
        task.Notes = notes?.Trim();
        if (accessService.IsHr(User))
        {
            task.AssignedToUsername = string.IsNullOrWhiteSpace(assignedToUsername)
                ? null
                : assignedToUsername.Trim();
            if (dueDate.HasValue)
            {
                task.DueDate = dueDate.Value.Date;
            }
        }
        task.CompletedBy = status == InductionTaskStatus.NotStarted ? null : accessService.CurrentUsername(User);
        task.CompletedAtUtc = status == InductionTaskStatus.NotStarted ? null : DateTime.UtcNow;
        await workflow.RefreshStatusAsync(task.InductionCase, HttpContext.RequestAborted);
        await dbContext.SaveChangesAsync();

        TempData["Success"] = "Checklist item updated.";
        return RedirectToAction(nameof(Details), new { id = task.InductionCaseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(EmployeeFeedbackInputModel model)
    {
        var inductionCase = await dbContext.InductionCases
            .Include(x => x.EmployeeFeedback)
            .Include(x => x.InductionProgramme)
                .ThenInclude(x => x!.Sessions)
            .SingleOrDefaultAsync(x => x.Id == model.InductionCaseId);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanSubmitFeedback(User, inductionCase)) return Forbid();
        if (inductionCase.EmployeeFeedback is not null)
        {
            TempData["Error"] = "Feedback has already been submitted.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        var programmeSessions = inductionCase.InductionProgramme?.Sessions.OrderBy(x => x.SortOrder).ToList() ?? [];
        var submittedRatings = model.PresentationRatings
            .GroupBy(x => x.ProgrammeSessionId)
            .ToDictionary(x => x.Key, x => x.First());
        if (programmeSessions.Count == 0)
        {
            ModelState.AddModelError(nameof(model.PresentationRatings), "An induction programme with scheduled presentations must be assigned first.");
        }
        else if (submittedRatings.Count != programmeSessions.Count
            || programmeSessions.Any(x => !submittedRatings.ContainsKey(x.Id)))
        {
            ModelState.AddModelError(nameof(model.PresentationRatings), "Rate every divisional presentation in the assigned programme.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Complete every programme rating and feedback question before submitting.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        inductionCase.EmployeeFeedback = new EmployeeFeedback
        {
            ProgrammeUnderstandingRating = model.ProgrammeUnderstandingRating,
            TimeAllocationResponse = model.TimeAllocationResponse,
            OverallProgrammeRating = model.OverallProgrammeRating,
            MostValuableAspect = model.MostValuableAspect.Trim(),
            ImprovementRecommendations = model.ImprovementRecommendations.Trim(),
            PresentationRatings = programmeSessions.Select(session => new PresentationRating
            {
                InductionProgrammeSessionId = session.Id,
                Rating = submittedRatings[session.Id].Rating!.Value
            }).ToList(),
            SubmittedBy = accessService.CurrentUsername(User)
        };

        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Employee induction feedback submitted.";
        return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOff(SignOffInputModel model)
    {
        var inductionCase = await dbContext.InductionCases
            .AsSplitQuery()
            .Include(x => x.Tasks)
            .Include(x => x.SignOffs)
            .Include(x => x.EmployeeFeedback)
            .SingleOrDefaultAsync(x => x.Id == model.InductionCaseId);

        if (inductionCase is null) return NotFound();
        if (!accessService.CanSign(User, inductionCase, model.Role)) return Forbid();
        if (inductionCase.SignOffs.Any(x => x.Role == model.Role))
        {
            TempData["Error"] = "This sign-off has already been completed.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        var settings = await configurationService.GetAsync(HttpContext.RequestAborted);
        var mandatoryComplete = inductionCase.Tasks
            .Where(x => x.IsMandatory)
            .All(x => x.Status is InductionTaskStatus.Completed or InductionTaskStatus.NotApplicable);

        if (settings.RequireMandatoryTasksBeforeSignOff && !mandatoryComplete)
        {
            TempData["Error"] = "All mandatory checklist items must be completed before sign-off.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        if (settings.RequireEmployeeFeedbackBeforeSignOff
            && model.Role == SignOffRole.Employee
            && inductionCase.EmployeeFeedback is null)
        {
            TempData["Error"] = "Employee feedback must be submitted before employee sign-off.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        if (settings.EnforceSequentialSignOff
            && model.Role == SignOffRole.LineManager
            && !inductionCase.SignOffs.Any(x => x.Role == SignOffRole.Employee))
        {
            TempData["Error"] = "Employee sign-off must be completed first.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        if (settings.EnforceSequentialSignOff
            && model.Role == SignOffRole.HrBusinessPartner
            && !inductionCase.SignOffs.Any(x => x.Role == SignOffRole.LineManager))
        {
            TempData["Error"] = "Line manager sign-off must be completed first.";
            return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
        }

        inductionCase.SignOffs.Add(new SignOff
        {
            Role = model.Role,
            SignedBy = accessService.CurrentUsername(User),
            ExpectedSignerUsername = ExpectedSignerFor(model.Role, inductionCase),
            SignedBySid = User.FindFirstValue(ClaimTypes.PrimarySid)
                ?? (User.Identity as WindowsIdentity)?.User?.Value,
            AuthenticationType = User.Identity?.AuthenticationType,
            Acknowledgement = AcknowledgementFor(model.Role, settings),
            Comments = model.Comments?.Trim()
        });

        await workflow.RefreshStatusAsync(inductionCase, HttpContext.RequestAborted);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Sign-off recorded with your Windows identity and timestamp.";
        return RedirectToAction(nameof(Details), new { id = inductionCase.Id });
    }

    private async Task LoadCreateOptionsAsync(CreateInductionViewModel model)
    {
        model.EmployeeTitleOptions = await MasterOptionsAsync(MasterDataCategory.EmployeeTitle, model.EmployeeTitle);
        model.GenderOptions = await MasterOptionsAsync(MasterDataCategory.Gender, model.Gender);
        model.DepartmentOptions = await MasterOptionsAsync(MasterDataCategory.Department, model.Department);
        model.DivisionOptions = await MasterOptionsAsync(MasterDataCategory.Division, model.Division);
        model.ProgrammeOptions = await dbContext.InductionProgrammes
            .AsNoTracking()
            .Where(x => x.Status != InductionProgrammeStatus.Cancelled)
            .OrderByDescending(x => x.ProgrammeDate)
            .ThenBy(x => x.Title)
            .Select(x => new SelectListItem(
                $"{x.ProgrammeDate:dd MMM yyyy} - {x.Title}",
                x.Id.ToString(),
                model.InductionProgrammeId == x.Id))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private async Task<IReadOnlyList<SelectListItem>> MasterOptionsAsync(MasterDataCategory category, string? selected)
    {
        var values = await dbContext.MasterDataItems
            .AsNoTracking()
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        return values.Select(x => new SelectListItem(x, x, string.Equals(x, selected, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private async Task ValidateMasterDataSelectionAsync(CreateInductionViewModel model)
    {
        if (!await IsActiveMasterDataAsync(MasterDataCategory.EmployeeTitle, model.EmployeeTitle))
        {
            ModelState.AddModelError(nameof(model.EmployeeTitle), "Select an active employee title from the administration workspace.");
        }
        if (!await IsActiveMasterDataAsync(MasterDataCategory.Gender, model.Gender))
        {
            ModelState.AddModelError(nameof(model.Gender), "Select an active gender from the administration workspace.");
        }
        if (!await IsActiveMasterDataAsync(MasterDataCategory.Department, model.Department))
        {
            ModelState.AddModelError(nameof(model.Department), "Select an active department from the administration workspace.");
        }
        if (!await IsActiveMasterDataAsync(MasterDataCategory.Division, model.Division))
        {
            ModelState.AddModelError(nameof(model.Division), "Select an active division from the administration workspace.");
        }
    }

    private Task<bool> IsActiveMasterDataAsync(MasterDataCategory category, string? value) =>
        !string.IsNullOrWhiteSpace(value)
            ? dbContext.MasterDataItems.AnyAsync(x => x.Category == category && x.IsActive && x.Name == value.Trim())
            : Task.FromResult(false);

    private async Task ValidateProgrammeSelectionAsync(int? programmeId)
    {
        if (!programmeId.HasValue) return;
        if (!await dbContext.InductionProgrammes.AnyAsync(
                x => x.Id == programmeId.Value && x.Status != InductionProgrammeStatus.Cancelled,
                HttpContext.RequestAborted))
        {
            ModelState.AddModelError("InductionProgrammeId", "Select an available induction programme.");
        }
    }

    private static void WriteReferenceValues(
        IXLWorksheet worksheet,
        int column,
        IEnumerable<MasterDataItem> items,
        MasterDataCategory category)
    {
        var row = 2;
        foreach (var value in items.Where(x => x.Category == category).Select(x => x.Name).Take(500))
        {
            worksheet.Cell(row++, column).Value = value;
        }
    }

    private static string ExpectedSignerFor(SignOffRole role, InductionCase inductionCase) => role switch
    {
        SignOffRole.Employee => inductionCase.EmployeeUsername,
        SignOffRole.LineManager => inductionCase.LineManagerUsername,
        SignOffRole.HrBusinessPartner => inductionCase.HrBusinessPartnerUsername,
        _ => string.Empty
    };

    private static string AcknowledgementFor(SignOffRole role, WorkflowConfiguration settings) => role switch
    {
        SignOffRole.Employee => settings.EmployeeAcknowledgement,
        SignOffRole.LineManager => settings.LineManagerAcknowledgement,
        SignOffRole.HrBusinessPartner => settings.HrBusinessPartnerAcknowledgement,
        _ => "Induction sign-off confirmed."
    };
}
