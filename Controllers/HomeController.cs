using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
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
        var openTasks = dbContext.InductionTasks
            .AsNoTracking()
            .Include(x => x.InductionCase)
            .Where(x => x.Status == InductionTaskStatus.NotStarted);

        if (!isHr)
        {
            openTasks = openTasks.Where(x => x.AssignedToUsername == username);
        }

        var model = new DashboardViewModel
        {
            IsHr = isHr,
            ActiveInductions = await cases.CountAsync(x => x.Status != InductionStatus.Completed),
            AwaitingSignOff = await cases.CountAsync(x => x.Status == InductionStatus.AwaitingSignOff),
            CompletedThisMonth = await cases.CountAsync(x =>
                x.Status == InductionStatus.Completed
                && x.SignOffs.Any(s => s.SignedAtUtc >= monthStart)),
            OverdueTasks = await openTasks.CountAsync(x => x.DueDate < today),
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
