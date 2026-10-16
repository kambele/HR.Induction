using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Helpers;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class HomeController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var username = accessService.CurrentUsername(User);
        var isHr = accessService.IsHr(User);
        var cases = dbContext.InductionCases.AsNoTracking();

        if (!isHr)
        {
            cases = cases.Where(x =>
                x.EmployeeUsername == username
                || x.LineManagerUsername == username
                || x.HrBusinessPartnerUsername == username
                || x.Tasks.Any(t => t.AssignedToUsername == username));
        }

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var trendStart = monthStart.AddMonths(-5);
        var openTasks = dbContext.InductionTasks
            .AsNoTracking()
            .Include(x => x.InductionCase)
            .Where(x => x.Status == InductionTaskStatus.NotStarted);

        if (!isHr)
        {
            openTasks = openTasks.Where(x => x.AssignedToUsername == username);
        }

        var analyticsCases = await cases
            .Select(x => new
            {
                x.AppointmentDate,
                x.Status,
                x.InductionProgrammeId,
                AttendanceStatus = x.ProgrammeAttendance == null
                    ? ProgrammeAttendanceStatus.Pending
                    : x.ProgrammeAttendance.Status,
                HasFeedback = x.EmployeeFeedback != null && x.EmployeeFeedback.OverallProgrammeRating != null,
                OverallRating = x.EmployeeFeedback == null ? null : x.EmployeeFeedback.OverallProgrammeRating
            })
            .ToListAsync(HttpContext.RequestAborted);

        var programmeAssigned = analyticsCases.Where(x => x.InductionProgrammeId.HasValue).ToList();
        var presentCount = programmeAssigned.Count(x => x.AttendanceStatus == ProgrammeAttendanceStatus.Present);
        var feedbackCases = analyticsCases.Where(x => x.HasFeedback).ToList();
        var monthlyTrend = Enumerable.Range(0, 6)
            .Select(offset => trendStart.AddMonths(offset))
            .Select(month => new DashboardTrendPoint(
                month.ToString("MMM yyyy"),
                analyticsCases.Count(x => x.AppointmentDate.Year == month.Year && x.AppointmentDate.Month == month.Month),
                analyticsCases.Count(x => x.AppointmentDate.Year == month.Year
                    && x.AppointmentDate.Month == month.Month
                    && x.Status == InductionStatus.Completed)))
            .ToList();

        var scheduledProgrammes = isHr
            ? await dbContext.InductionProgrammes.AsNoTracking().CountAsync(x =>
                x.Status == InductionProgrammeStatus.Scheduled && x.ProgrammeDate >= today,
                HttpContext.RequestAborted)
            : await dbContext.InductionProgrammes.AsNoTracking().CountAsync(x =>
                x.Status == InductionProgrammeStatus.Scheduled
                && x.ProgrammeDate >= today
                && (x.Participants.Any(participant => participant.EmployeeUsername == username)
                    || x.Sessions.Any(session => session.PresenterUsername == username)),
                HttpContext.RequestAborted);

        var model = new DashboardViewModel
        {
            IsHr = isHr,
            TotalInductions = analyticsCases.Count,
            ActiveInductions = await cases.CountAsync(x => x.Status != InductionStatus.Completed),
            AwaitingSignOff = await cases.CountAsync(x => x.Status == InductionStatus.AwaitingSignOff),
            CompletedThisMonth = await cases.CountAsync(x =>
                x.Status == InductionStatus.Completed
                && x.SignOffs.Any(s => s.SignedAtUtc >= monthStart)),
            OverdueTasks = await openTasks.CountAsync(x => x.DueDate < today),
            ScheduledProgrammes = scheduledProgrammes,
            AttendanceRate = programmeAssigned.Count == 0
                ? 0
                : Math.Round(presentCount * 100m / programmeAssigned.Count, 1),
            FeedbackResponseRate = programmeAssigned.Count == 0
                ? 0
                : Math.Round(feedbackCases.Count * 100m / programmeAssigned.Count, 1),
            AverageProgrammeRating = feedbackCases.Count == 0
                ? 0
                : Math.Round(feedbackCases.Average(x => x.OverallRating!.Value), 2),
            MonthlyTrend = monthlyTrend,
            StatusDistribution = Enum.GetValues<InductionStatus>()
                .Select(status => new DashboardCategoryValue(
                    DisplayText.For(status),
                    analyticsCases.Count(x => x.Status == status)))
                .ToList(),
            AttendanceDistribution =
            [
                new DashboardCategoryValue("Present", programmeAssigned.Count(x => x.AttendanceStatus == ProgrammeAttendanceStatus.Present)),
                new DashboardCategoryValue("Absent", programmeAssigned.Count(x => x.AttendanceStatus == ProgrammeAttendanceStatus.Absent)),
                new DashboardCategoryValue("Pending", programmeAssigned.Count(x => x.AttendanceStatus == ProgrammeAttendanceStatus.Pending))
            ],
            RecentInductions = await cases
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(6)
                .ToListAsync(),
            MyOpenTasks = await openTasks
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.SortOrder)
                .Take(8)
                .ToListAsync()
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
