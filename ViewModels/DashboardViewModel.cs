using Namfisa.Induction.Models;

namespace Namfisa.Induction.ViewModels;

public class DashboardViewModel
{
    public int TotalInductions { get; set; }
    public int ActiveInductions { get; set; }
    public int OverdueTasks { get; set; }
    public int AwaitingSignOff { get; set; }
    public int CompletedThisMonth { get; set; }
    public int ScheduledProgrammes { get; set; }
    public decimal AttendanceRate { get; set; }
    public decimal FeedbackResponseRate { get; set; }
    public double AverageProgrammeRating { get; set; }
    public IReadOnlyList<DashboardTrendPoint> MonthlyTrend { get; set; } = [];
    public IReadOnlyList<DashboardCategoryValue> StatusDistribution { get; set; } = [];
    public IReadOnlyList<DashboardCategoryValue> AttendanceDistribution { get; set; } = [];
    public IReadOnlyList<InductionCase> RecentInductions { get; set; } = [];
    public IReadOnlyList<InductionTask> MyOpenTasks { get; set; } = [];
    public bool IsHr { get; set; }
}

public record DashboardTrendPoint(string Month, int Planned, int Completed);
public record DashboardCategoryValue(string Label, int Value);
