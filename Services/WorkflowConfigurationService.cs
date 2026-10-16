using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public interface IWorkflowConfigurationService
{
    Task<WorkflowConfiguration> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class WorkflowConfigurationService(ApplicationDbContext dbContext) : IWorkflowConfigurationService
{
    public async Task<WorkflowConfiguration> GetAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WorkflowConfigurations
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
        ?? new WorkflowConfiguration { UpdatedBy = "System" };
}
