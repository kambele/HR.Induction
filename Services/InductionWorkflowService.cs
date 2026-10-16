using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public interface IInductionWorkflowService
{
    Task AddTasksFromTemplateAsync(InductionCase inductionCase, CancellationToken cancellationToken = default);
    Task RefreshStatusAsync(InductionCase inductionCase, CancellationToken cancellationToken = default);
}

public class InductionWorkflowService(ApplicationDbContext dbContext) : IInductionWorkflowService
{
    public async Task AddTasksFromTemplateAsync(InductionCase inductionCase, CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.ChecklistTemplateItems
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var template in templates)
        {
            inductionCase.Tasks.Add(new InductionTask
            {
                ChecklistTemplateItemId = template.Id,
                Section = template.Section,
                Title = template.Title,
                Phase = template.Phase,
                ResponsibleRole = template.ResponsibleRole,
                AssignedToUsername = ResolveAssignee(template.ResponsibleRole, inductionCase),
                DueDate = inductionCase.AppointmentDate.Date.AddDays(template.DueOffsetDays),
                IsMandatory = template.IsMandatory,
                SortOrder = template.SortOrder
            });
        }
    }

    public async Task RefreshStatusAsync(InductionCase inductionCase, CancellationToken cancellationToken = default)
    {
        if (inductionCase.Status == InductionStatus.OnHold) return;

        var settings = await dbContext.WorkflowConfigurations
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? new WorkflowConfiguration { UpdatedBy = "System" };

        var allMandatoryComplete = inductionCase.Tasks
            .Where(x => x.IsMandatory)
            .All(x => x.Status is InductionTaskStatus.Completed or InductionTaskStatus.NotApplicable);

        var hasAnyProgress = inductionCase.Tasks.Any(x => x.Status != InductionTaskStatus.NotStarted);
        var hasAllSignOffs = Enum.GetValues<SignOffRole>()
            .All(role => inductionCase.SignOffs.Any(x => x.Role == role));

        var readyForSignOff = !settings.RequireMandatoryTasksBeforeSignOff || allMandatoryComplete;
        inductionCase.Status = hasAllSignOffs
            ? InductionStatus.Completed
            : readyForSignOff
                ? InductionStatus.AwaitingSignOff
                : hasAnyProgress
                    ? InductionStatus.InProgress
                    : InductionStatus.Planned;
    }

    private static string? ResolveAssignee(string role, InductionCase inductionCase) => role switch
    {
        "Employee" => inductionCase.EmployeeUsername,
        "Line Manager" => inductionCase.LineManagerUsername,
        "HRBP" => inductionCase.HrBusinessPartnerUsername,
        _ => null
    };
}
