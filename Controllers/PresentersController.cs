using System.Security.Principal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class PresentersController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService,
    IInductionEmailService emailService,
    ILogger<PresentersController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var currentUsername = accessService.CurrentUsername(User);
        var query = dbContext.InductionProgrammes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Sessions)
            .Include(x => x.Participants)
            .Include(x => x.Attendances)
            .AsQueryable();

        if (!accessService.IsHr(User))
        {
            query = query.Where(x => x.Sessions.Any(session => session.PresenterUsername == currentUsername));
        }

        var programmes = await query
            .OrderByDescending(x => x.ProgrammeDate)
            .ThenBy(x => x.Title)
            .ToListAsync(HttpContext.RequestAborted);
        return View(programmes);
    }

    [HttpGet]
    public async Task<IActionResult> Programme(int id)
    {
        var programme = await ProgrammeQuery()
            .SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();
        if (!accessService.CanManageProgrammeAttendance(User, programme)) return Forbid();

        return View(BuildProgrammeModel(programme));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAttendance(UpdateProgrammeAttendanceViewModel model)
    {
        if (model.Status is not ProgrammeAttendanceStatus.Present and not ProgrammeAttendanceStatus.Absent)
        {
            TempData["Error"] = "Select Present or Absent.";
            return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
        }

        var programme = await dbContext.InductionProgrammes
            .Include(x => x.Sessions)
            .Include(x => x.Participants)
                .ThenInclude(x => x.ProgrammeAttendance)
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();
        if (!accessService.CanManageProgrammeAttendance(User, programme)) return Forbid();

        var induction = programme.Participants.SingleOrDefault(x => x.Id == model.InductionCaseId);
        if (induction is null) return BadRequest("The employee is not assigned to this programme.");

        var attendance = GetOrCreateAttendance(programme, induction);
        ApplyConfirmationAudit(attendance, model.Status, "Presenter");
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["Success"] = $"{induction.EmployeeName} marked {model.Status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Programme), new { id = programme.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendAttendanceInvitations(SendAttendanceInvitationsViewModel model)
    {
        var programme = await dbContext.InductionProgrammes
            .Include(x => x.Sessions)
            .Include(x => x.Participants)
                .ThenInclude(x => x.ProgrammeAttendance)
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();
        if (!accessService.CanManageProgrammeAttendance(User, programme)) return Forbid();

        if (!ModelState.IsValid || model.InductionCaseIds.Length == 0)
        {
            TempData["Error"] = "Select at least one attendee to invite.";
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }
        if (!emailService.ConfigurationStatus.IsConfigured)
        {
            TempData["Error"] = emailService.ConfigurationStatus.Summary;
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }

        var selectedIds = model.InductionCaseIds.Distinct().Take(200).ToHashSet();
        var attendees = programme.Participants
            .Where(x => selectedIds.Contains(x.Id))
            .OrderBy(x => x.EmployeeName)
            .ToList();
        var sentBy = accessService.CurrentUsername(User);
        var sentCount = 0;
        var failures = new List<string>();

        foreach (var attendee in attendees)
        {
            if (string.IsNullOrWhiteSpace(attendee.EmployeeEmail))
            {
                failures.Add($"{attendee.EmployeeName}: email address is missing.");
                continue;
            }

            var attendanceUrl = BuildAttendanceUrl(attendee.Id);
            if (string.IsNullOrWhiteSpace(attendanceUrl))
            {
                failures.Add($"{attendee.EmployeeName}: attendance link could not be created.");
                continue;
            }

            try
            {
                await emailService.SendAttendanceInvitationAsync(
                    attendee,
                    programme,
                    attendanceUrl,
                    HttpContext.RequestAborted);

                var attendance = GetOrCreateAttendance(programme, attendee);
                attendance.InvitationSentAtUtc = DateTime.UtcNow;
                attendance.InvitationSentBy = sentBy;
                attendance.InvitationSentTo = attendee.EmployeeEmail.Trim();
                await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
                sentCount++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Failed to send programme {ProgrammeId} attendance invitation to induction {InductionId}.",
                    programme.Id,
                    attendee.Id);
                failures.Add($"{attendee.EmployeeName}: delivery failed.");
            }
        }

        if (sentCount > 0)
        {
            TempData["Success"] = $"Sent {sentCount} attendance invitation{(sentCount == 1 ? string.Empty : "s")}.";
        }
        if (failures.Count > 0)
        {
            TempData["Error"] = $"{failures.Count} invitation{(failures.Count == 1 ? string.Empty : "s")} could not be sent. {string.Join(" ", failures.Take(3))}";
        }

        return RedirectToAction(nameof(Programme), new { id = programme.Id });
    }

    private IQueryable<InductionProgramme> ProgrammeQuery() => dbContext.InductionProgrammes
        .AsNoTracking()
        .AsSplitQuery()
        .Include(x => x.Sessions)
        .Include(x => x.Participants)
            .ThenInclude(x => x.ProgrammeAttendance)
        .Include(x => x.Attendances);

    private PresenterProgrammeViewModel BuildProgrammeModel(InductionProgramme programme) => new()
    {
        Programme = programme,
        SmtpConfigured = emailService.ConfigurationStatus.IsConfigured,
        SmtpSummary = emailService.ConfigurationStatus.Summary,
        Attendees = programme.Participants
            .OrderBy(x => x.EmployeeName)
            .Select(induction => new PresenterAttendeeViewModel
            {
                Induction = induction,
                Status = induction.ProgrammeAttendance?.Status ?? ProgrammeAttendanceStatus.Pending,
                ConfirmedAtUtc = induction.ProgrammeAttendance?.ConfirmedAtUtc,
                ConfirmedBy = induction.ProgrammeAttendance?.ConfirmedBy,
                ConfirmationSource = induction.ProgrammeAttendance?.ConfirmationSource,
                InvitationSentAtUtc = induction.ProgrammeAttendance?.InvitationSentAtUtc
            })
            .ToList()
    };

    private ProgrammeAttendance GetOrCreateAttendance(InductionProgramme programme, InductionCase induction)
    {
        if (induction.ProgrammeAttendance is not null) return induction.ProgrammeAttendance;

        var attendance = new ProgrammeAttendance
        {
            InductionProgrammeId = programme.Id,
            InductionCaseId = induction.Id,
            Status = ProgrammeAttendanceStatus.Pending
        };
        induction.ProgrammeAttendance = attendance;
        dbContext.ProgrammeAttendances.Add(attendance);
        return attendance;
    }

    private void ApplyConfirmationAudit(
        ProgrammeAttendance attendance,
        ProgrammeAttendanceStatus status,
        string source)
    {
        attendance.Status = status;
        attendance.ConfirmedBy = accessService.CurrentUsername(User);
        attendance.ConfirmedAtUtc = DateTime.UtcNow;
        attendance.ConfirmationSource = source;
        attendance.AuthenticationType = User.Identity?.AuthenticationType;
        attendance.ConfirmedBySid = (User.Identity as WindowsIdentity)?.User?.Value;
    }

    private string? BuildAttendanceUrl(int inductionCaseId)
    {
        if (emailService.PublicBaseUrl is { Length: > 0 } publicBaseUrl)
        {
            return $"{publicBaseUrl.TrimEnd('/')}/Attendance/Confirm/{inductionCaseId}";
        }

        return Url.Action(
            "Confirm",
            "Attendance",
            new { id = inductionCaseId },
            Request.Scheme,
            Request.Host.Value);
    }
}
