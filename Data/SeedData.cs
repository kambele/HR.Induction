using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;

namespace Namfisa.Induction.Data;

public static class SeedData
{
    private static readonly string[] DefaultDivisions =
    [
        "Corporate Communications and Consumer Education",
        "Governance, Risk and Compliance",
        "Information and Communication Technology",
        "Finance and Administration",
        "Legal and Licensing Services",
        "Internal Audit",
        "Pension Funds and Friendly Societies",
        "Insurance and Medical Aid Funds",
        "Capital Markets",
        "Research, Policy and Statistics",
        "Strategy and Projects",
        "Market Conduct",
        "Human Resources"
    ];

    public static async Task InitializeAsync(
        ApplicationDbContext dbContext,
        IInductionWorkflowService workflow,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (!await dbContext.ChecklistTemplateItems.AnyAsync())
        {
            dbContext.ChecklistTemplateItems.AddRange(BuildChecklistTemplates());
            await dbContext.SaveChangesAsync();
        }

        var templates = await dbContext.ChecklistTemplateItems.AsNoTracking().ToListAsync();
        var existingCases = await dbContext.InductionCases.AsNoTracking().ToListAsync();
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.Department,
            existingCases.Select(x => x.Department).Append("Employee Services"));
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.Division,
            DefaultDivisions.Concat(existingCases.Select(x => x.Division)));
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.ResponsibleRole,
            templates.Select(x => x.ResponsibleRole));
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.ChecklistSection,
            templates.Select(x => x.Section));
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.EmployeeTitle,
            ["Mr", "Ms", "Mrs", "Dr", "Prof", "Adv"]);
        await EnsureMasterDataAsync(dbContext, MasterDataCategory.Gender,
            ["Female", "Male", "Other", "Prefer not to say"]);

        if (!await dbContext.WorkflowConfigurations.AnyAsync())
        {
            dbContext.WorkflowConfigurations.Add(new WorkflowConfiguration
            {
                UpdatedBy = "System"
            });
            await dbContext.SaveChangesAsync();
        }

        if (!environment.IsDevelopment()
            || !configuration.GetValue<bool>("Demo:SeedSampleCase")
            || await dbContext.InductionCases.AnyAsync())
        {
            return;
        }

        var username = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var sample = new InductionCase
        {
            EmployeeTitle = "Ms",
            FirstName = "Sample",
            LastName = "Employee",
            EmployeeName = "Sample Employee",
            Gender = "Prefer not to say",
            Position = "Not recorded",
            EmployeeUsername = username,
            EmployeeNumber = "DEMO-001",
            Department = "Employee Services",
            Division = "Human Resources",
            AppointmentDate = DateTime.Today.AddDays(-3),
            LineManagerName = "Sample Line Manager",
            LineManagerUsername = username,
            HrBusinessPartnerName = "Sample HR Business Partner",
            HrBusinessPartnerUsername = username,
            CreatedBy = username
        };

        await workflow.AddTasksFromTemplateAsync(sample);
        dbContext.InductionCases.Add(sample);
        await dbContext.SaveChangesAsync();
    }

    private static IEnumerable<ChecklistTemplateItem> BuildChecklistTemplates()
    {
        var items = new List<ChecklistTemplateItem>();
        var order = 0;

        void Add(string section, string title, InductionPhase phase, string role, int offset, bool mandatory = true)
        {
            items.Add(new ChecklistTemplateItem
            {
                Section = section,
                Title = title,
                Phase = phase,
                ResponsibleRole = role,
                DueOffsetDays = offset,
                IsMandatory = mandatory,
                SortOrder = ++order,
                UpdatedBy = "System"
            });
        }

        Add("Prior to starting", "Employment contract, job description and payroll forms provided", InductionPhase.PreStart, "HRBP", -1);
        Add("Prior to starting", "Administration, ICT and HR informed of the new arrival", InductionPhase.PreStart, "HRBP", -1);
        Add("Prior to starting", "Domain user account and additional services request submitted", InductionPhase.PreStart, "HRBP", -1);

        Add("Key information", "Physical introduction to staff", InductionPhase.FirstDay, "HRBP", 0);
        Add("Key information", "Access card issued and fingerprint registered", InductionPhase.FirstDay, "Administration", 0);
        Add("Key information", "Parking disc issued where applicable", InductionPhase.FirstDay, "Administration", 0, false);
        Add("Key information", "Telephone usage explained", InductionPhase.FirstDay, "Administration", 0);
        Add("Key information", "Kitchen and ablution facilities shown", InductionPhase.FirstDay, "Team member", 0);
        Add("Key information", "Introduced to immediate colleagues", InductionPhase.FirstDay, "Line Manager", 0);
        Add("Key information", "Purpose and activities of the division explained", InductionPhase.FirstDay, "Line Manager", 0);
        Add("Key information", "How the division fits into the relevant organisational stream explained", InductionPhase.FirstDay, "Line Manager", 0);
        Add("Key information", "Additional role or department information covered", InductionPhase.FirstDay, "Line Manager", 0, false);

        Add("Working at NAMFISA", "Intranet, internet and email facilities explained", InductionPhase.FirstTwoWeeks, "ICT", 14);
        Add("Working at NAMFISA", "Job expectations set and job description discussed and signed", InductionPhase.FirstTwoWeeks, "Line Manager", 14);
        Add("Working at NAMFISA", "Performance agreement signed", InductionPhase.FirstTwoWeeks, "Line Manager", 14);
        Add("Working at NAMFISA", "Performance review process explained", InductionPhase.FirstTwoWeeks, "Line Manager", 14);
        Add("Working at NAMFISA", "Photocopier operation explained", InductionPhase.FirstTwoWeeks, "Team member", 14);
        Add("Working at NAMFISA", "Filing system explained", InductionPhase.FirstTwoWeeks, "Team member", 14);
        Add("Working at NAMFISA", "Incoming and outgoing mail system explained", InductionPhase.FirstTwoWeeks, "Team member", 14);
        Add("Working at NAMFISA", "Internal ordering system explained", InductionPhase.FirstTwoWeeks, "Team member", 14);

        Add("Salary and conditions", "Salary package structured", InductionPhase.FirstTwoWeeks, "Payroll", 14);
        Add("Salary and conditions", "Employment contract signed and submitted", InductionPhase.FirstTwoWeeks, "Employee", 14);
        Add("Salary and conditions", "Onboarding forms and supporting documents submitted", InductionPhase.FirstTwoWeeks, "Employee", 14);
        Add("Salary and conditions", "Flexible working procedures explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Salary and conditions", "Overtime arrangements explained", InductionPhase.FirstTwoWeeks, "Payroll", 14);
        Add("Salary and conditions", "Leave arrangements explained", InductionPhase.FirstTwoWeeks, "Payroll", 14);

        Add("Health and safety", "Health and Safety Policy and employee responsibilities explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Health and safety", "Incident and health and safety problem reporting procedure explained", InductionPhase.FirstTwoWeeks, "Health and Safety Representative", 14);

        Add("Policies and procedures", "Code of Conduct and Ethics explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Policies and procedures", "Declaration of Interest explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Policies and procedures", "Conditions of Employment explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Policies and procedures", "Leave Administration explained", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Policies and procedures", "Access to divisional information provided", InductionPhase.FirstTwoWeeks, "HRBP", 14);
        Add("Learning and development", "Role-specific learning and development needs recorded", InductionPhase.FirstTwoWeeks, "Line Manager", 14, false);

        foreach (var division in DefaultDivisions)
        {
            Add("Divisional induction", $"Induction completed with {division}", InductionPhase.Divisional, "Divisional contact", 60);
        }

        return items;
    }

    private static async Task EnsureMasterDataAsync(
        ApplicationDbContext dbContext,
        MasterDataCategory category,
        IEnumerable<string> values)
    {
        var currentItems = await dbContext.MasterDataItems
            .Where(x => x.Category == category)
            .ToListAsync();
        var existing = currentItems.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sortOrder = currentItems.Count == 0 ? 0 : currentItems.Max(x => x.SortOrder);
        foreach (var value in values
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            if (existing.Contains(value)) continue;
            dbContext.MasterDataItems.Add(new MasterDataItem
            {
                Category = category,
                Name = value,
                SortOrder = ++sortOrder,
                UpdatedBy = "System"
            });
        }
        await dbContext.SaveChangesAsync();
    }
}
