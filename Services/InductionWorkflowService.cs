using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public interface IInductionWorkflowService
{
    Task AddTasksFromTemplateAsync(InductionCase inductionCase, CancellationToken cancellationToken = default);
    Task<ChecklistRefreshResult> RefreshTasksFromProgrammeAsync(InductionCase inductionCase, CancellationToken cancellationToken = default);
    Task RefreshStatusAsync(InductionCase inductionCase, CancellationToken cancellationToken = default);
}

public sealed record ChecklistRefreshResult(int Added, int Included, int Excluded, int ApplicableTasks);

public class InductionWorkflowService(ApplicationDbContext dbContext) : IInductionWorkflowService
{
    public async Task AddTasksFromTemplateAsync(InductionCase inductionCase, CancellationToken cancellationToken = default)
    {
        var applicablePhases = await GetApplicablePhasesAsync(inductionCase.InductionProgrammeId, cancellationToken);
        var templates = await dbContext.ChecklistTemplateItems
            .Where(x => x.IsActive && applicablePhases.Contains(x.Phase))
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
                ExcludedByProgramme = false,
                SortOrder = template.SortOrder
            });
        }
    }

    public async Task<ChecklistRefreshResult> RefreshTasksFromProgrammeAsync(
        InductionCase inductionCase,
        CancellationToken cancellationToken = default)
    {
        var applicablePhases = await GetApplicablePhasesAsync(inductionCase.InductionProgrammeId, cancellationToken);
        var templates = await dbContext.ChecklistTemplateItems
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var added = 0;
        var included = 0;
        var excluded = 0;

        foreach (var task in inductionCase.Tasks)
        {
            var shouldExclude = !applicablePhases.Contains(task.Phase);
            if (task.ExcludedByProgramme == shouldExclude) continue;

            task.ExcludedByProgramme = shouldExclude;
            if (shouldExclude) excluded++;
            else included++;
        }

        var existingTemplateIds = inductionCase.Tasks
            .Where(x => x.ChecklistTemplateItemId.HasValue)
            .Select(x => x.ChecklistTemplateItemId!.Value)
            .ToHashSet();

        foreach (var template in templates.Where(x => applicablePhases.Contains(x.Phase)))
        {
            if (existingTemplateIds.Contains(template.Id)) continue;

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
                ExcludedByProgramme = false,
                SortOrder = template.SortOrder
            });
            added++;
        }

        var applicableTasks = inductionCase.Tasks.Count(x => !x.ExcludedByProgramme);
        return new ChecklistRefreshResult(added, included, excluded, applicableTasks);
    }

    public async Task RefreshStatusAsync(InductionCase inductionCase, CancellationToken cancellationToken = default)
    {
        if (inductionCase.Status == InductionStatus.OnHold) return;

        var settings = await dbContext.WorkflowConfigurations
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? new WorkflowConfiguration { UpdatedBy = "System" };

        var applicableTasks = inductionCase.Tasks.Where(x => !x.ExcludedByProgramme).ToList();
        var allMandatoryComplete = applicableTasks
            .Where(x => x.IsMandatory)
            .All(x => x.Status is InductionTaskStatus.Completed or InductionTaskStatus.NotApplicable);

        var hasAnyProgress = applicableTasks.Any(x => x.Status != InductionTaskStatus.NotStarted);
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

    private async Task<HashSet<InductionPhase>> GetApplicablePhasesAsync(
        int? programmeId,
        CancellationToken cancellationToken)
    {
        if (!programmeId.HasValue)
        {
            return Enum.GetValues<InductionPhase>().ToHashSet();
        }

        var phases = await dbContext.InductionProgrammeChecklistPhases
            .AsNoTracking()
            .Where(x => x.InductionProgrammeId == programmeId.Value)
            .Select(x => x.Phase)
            .ToListAsync(cancellationToken);

        return phases.Count == 0
            ? Enum.GetValues<InductionPhase>().ToHashSet()
            : phases.ToHashSet();
    }

    private static string? ResolveAssignee(string role, InductionCase inductionCase) => role switch
    {
        "Employee" => inductionCase.EmployeeUsername,
        "Line Manager" => inductionCase.LineManagerUsername,
        "HRBP" => inductionCase.HrBusinessPartnerUsername,
        _ => null
    };
}
