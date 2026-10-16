using Namfisa.Induction.Models;

namespace Namfisa.Induction.ViewModels;

public class DashboardViewModel
{
    public int ActiveInductions { get; set; }
    public int OverdueTasks { get; set; }
    public int AwaitingSignOff { get; set; }
    public int CompletedThisMonth { get; set; }
    public IReadOnlyList<InductionCase> RecentInductions { get; set; } = [];
    public IReadOnlyList<InductionTask> MyOpenTasks { get; set; } = [];
    public bool IsHr { get; set; }
}
