using System.Security.Principal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;

namespace Namfisa.Induction.Controllers;

public class AttendanceController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Confirm(int id)
    {
        var induction = await AttendanceQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (induction is null) return NotFound();
        if (!accessService.CanSelfConfirmAttendance(User, induction)) return Forbid();
        if (induction.InductionProgramme is null) return BadRequest("No induction programme is assigned to this employee.");

        return View(induction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int inductionCaseId, ProgrammeAttendanceStatus status)
    {
        if (status is not ProgrammeAttendanceStatus.Present and not ProgrammeAttendanceStatus.Absent)
        {
            TempData["Error"] = "Select Present or Absent.";
            return RedirectToAction(nameof(Confirm), new { id = inductionCaseId });
        }

        var induction = await AttendanceQuery()
            .SingleOrDefaultAsync(x => x.Id == inductionCaseId, HttpContext.RequestAborted);
        if (induction is null) return NotFound();
        if (!accessService.CanSelfConfirmAttendance(User, induction)) return Forbid();
        if (induction.InductionProgramme is null) return BadRequest("No induction programme is assigned to this employee.");

        var attendance = induction.ProgrammeAttendance ?? new ProgrammeAttendance
        {
            InductionProgrammeId = induction.InductionProgramme.Id,
            InductionCaseId = induction.Id
        };
        if (induction.ProgrammeAttendance is null)
        {
            induction.ProgrammeAttendance = attendance;
            dbContext.ProgrammeAttendances.Add(attendance);
        }

        attendance.Status = status;
        attendance.ConfirmedBy = accessService.CurrentUsername(User);
        attendance.ConfirmedBySid = (User.Identity as WindowsIdentity)?.User?.Value;
        attendance.AuthenticationType = User.Identity?.AuthenticationType;
        attendance.ConfirmationSource = "Employee";
        attendance.ConfirmedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["Success"] = $"Attendance confirmed as {status.ToString().ToLowerInvariant()}.";
        return status == ProgrammeAttendanceStatus.Present && induction.EmployeeFeedback is null
            ? RedirectToAction("Feedback", "Inductions", new { id = induction.Id })
            : RedirectToAction(nameof(Confirm), new { id = induction.Id });
    }

    private IQueryable<InductionCase> AttendanceQuery() => dbContext.InductionCases
        .Include(x => x.ProgrammeAttendance)
        .Include(x => x.EmployeeFeedback)
        .Include(x => x.InductionProgramme)
            .ThenInclude(x => x!.Sessions);
}
