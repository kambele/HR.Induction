using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public static class InductionProgrammeRules
{
    public static DateTime StartsAt(InductionProgramme programme)
    {
        var firstSession = programme.Sessions
            .OrderBy(x => x.StartTime)
            .FirstOrDefault();
        return programme.ProgrammeDate.Date.Add(firstSession?.StartTime ?? TimeSpan.Zero);
    }

    public static bool IsCompleted(InductionProgramme programme) =>
        programme.Status != InductionProgrammeStatus.Cancelled
        && (programme.Status == InductionProgrammeStatus.Completed
            || (programme.Sessions.Count > 0
                && programme.Sessions.All(x => x.CompletedAtUtc.HasValue)));

    public static bool CanSendAttendanceInvitations(InductionProgramme programme, DateTime localNow) =>
        programme.Sessions.Count > 0
        && programme.Status is not InductionProgrammeStatus.Completed and not InductionProgrammeStatus.Cancelled
        && localNow < StartsAt(programme);
}
