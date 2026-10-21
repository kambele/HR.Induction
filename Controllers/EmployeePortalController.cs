using System.Security.Principal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

[Route("Employee")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class EmployeePortalController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService,
    IInductionEmailService emailService,
    ILogger<EmployeePortalController> logger) : Controller
{
    [HttpGet("Attendance/{token:guid}")]
    public async Task<IActionResult> Attendance(Guid token)
    {
        var attendance = await AttendanceTokenQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.AttendanceAccessToken == token,
                HttpContext.RequestAborted);

        if (attendance is null)
        {
            return PortalUnavailable();
        }
        if (!accessService.CanSelfConfirmAttendance(User, attendance.InductionCase))
        {
            return PortalUnavailable(StatusCodes.Status403Forbidden,
                "This invitation belongs to a different employee Windows account.");
        }
        return View(BuildAttendanceModel(attendance, token));
    }

    [HttpPost("Attendance/{token:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attendance(Guid token, ProgrammeAttendanceStatus status)
    {
        var attendance = await AttendanceTokenQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.AttendanceAccessToken == token,
                HttpContext.RequestAborted);

        if (attendance is null)
        {
            return PortalUnavailable();
        }
        if (!accessService.CanSelfConfirmAttendance(User, attendance.InductionCase))
        {
            return PortalUnavailable(StatusCodes.Status403Forbidden,
                "This invitation belongs to a different employee Windows account.");
        }
        if (attendance.ConfirmedAtUtc.HasValue || attendance.Status != ProgrammeAttendanceStatus.Pending)
        {
            return PortalUnavailable();
        }
        if (status is not ProgrammeAttendanceStatus.Present and not ProgrammeAttendanceStatus.Absent)
        {
            ModelState.AddModelError(nameof(status), "Select Present or Absent.");
            return View(BuildAttendanceModel(attendance, token));
        }

        var confirmedAtUtc = DateTime.UtcNow;
        var confirmedBy = accessService.CurrentUsername(User);
        var confirmedBySid = (User.Identity as WindowsIdentity)?.User?.Value;
        var authenticationType = User.Identity?.AuthenticationType;
        var updated = await dbContext.ProgrammeAttendances
            .Where(x => x.Id == attendance.Id
                && x.AttendanceAccessToken == token
                && x.ConfirmedAtUtc == null
                && x.Status == ProgrammeAttendanceStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.ConfirmedBy, confirmedBy)
                .SetProperty(x => x.ConfirmedBySid, confirmedBySid)
                .SetProperty(x => x.AuthenticationType, authenticationType)
                .SetProperty(x => x.ConfirmationSource, "Employee programme link")
                .SetProperty(x => x.ConfirmedAtUtc, confirmedAtUtc)
                .SetProperty(x => x.AttendanceAccessTokenUsedAtUtc, confirmedAtUtc),
                HttpContext.RequestAborted);
        if (updated != 1)
        {
            return PortalUnavailable();
        }

        if (status == ProgrammeAttendanceStatus.Present
            && !attendance.CalendarInvitationSentAtUtc.HasValue
            && !string.IsNullOrWhiteSpace(attendance.InductionCase.EmployeeEmail)
            && emailService.ConfigurationStatus.IsConfigured)
        {
            try
            {
                await emailService.SendProgrammeCalendarInvitationAsync(
                    attendance.InductionCase,
                    attendance.InductionProgramme,
                    HttpContext.RequestAborted);
                var calendarSentAtUtc = DateTime.UtcNow;
                var calendarSentTo = attendance.InductionCase.EmployeeEmail.Trim();
                await dbContext.ProgrammeAttendances
                    .Where(x => x.Id == attendance.Id && x.CalendarInvitationSentAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.CalendarInvitationSentAtUtc, calendarSentAtUtc)
                        .SetProperty(x => x.CalendarInvitationSentBy, confirmedBy)
                        .SetProperty(x => x.CalendarInvitationSentTo, calendarSentTo),
                        HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Attendance was confirmed, but the calendar invitation failed for induction {InductionId}.",
                    attendance.InductionCaseId);
            }
        }

        return RedirectToAction(nameof(Completed), new { type = "attendance" });
    }

    [HttpGet("Feedback/{token:guid}")]
    public async Task<IActionResult> Feedback(Guid token)
    {
        var attendance = await FeedbackTokenQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.FeedbackAccessToken == token,
                HttpContext.RequestAborted);

        var unavailable = ValidateFeedbackAccess(attendance);
        if (unavailable is not null)
        {
            return unavailable;
        }

        return View(BuildFeedbackModel(attendance!, token));
    }

    [HttpPost("Feedback/{token:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(Guid token, EmployeeFeedbackInputModel model)
    {
        var attendance = await FeedbackTokenQuery()
            .SingleOrDefaultAsync(
                x => x.FeedbackAccessToken == token,
                HttpContext.RequestAborted);

        var unavailable = ValidateFeedbackAccess(attendance);
        if (unavailable is not null)
        {
            return unavailable;
        }

        if (attendance!.InductionCase.EmployeeFeedback is not null)
        {
            return RedirectToAction(nameof(Feedback), new { token = token.ToString("N") });
        }

        var induction = attendance.InductionCase;
        var programmeSessions = attendance.InductionProgramme.Sessions
            .Where(x => x.RequiresFeedback)
            .OrderBy(x => x.SortOrder)
            .ToList();
        var submittedRatings = (model.PresentationRatings ?? [])
            .GroupBy(x => x.ProgrammeSessionId)
            .ToDictionary(x => x.Key, x => x.First());

        if (submittedRatings.Count != programmeSessions.Count
            || programmeSessions.Any(x => !submittedRatings.ContainsKey(x.Id)))
        {
            ModelState.AddModelError(nameof(model.PresentationRatings),
                "Rate every presentation included in the employee evaluation.");
        }

        if (!ModelState.IsValid)
        {
            return View(BuildFeedbackModel(attendance, token, model));
        }

        induction.EmployeeFeedback = new EmployeeFeedback
        {
            ProgrammeUnderstandingRating = model.ProgrammeUnderstandingRating,
            TimeAllocationResponse = model.TimeAllocationResponse,
            OverallProgrammeRating = model.OverallProgrammeRating,
            MostValuableAspect = model.MostValuableAspect.Trim(),
            ImprovementRecommendations = model.ImprovementRecommendations.Trim(),
            PresentationRatings = programmeSessions.Select(session => new PresentationRating
            {
                InductionProgrammeSessionId = session.Id,
                Rating = submittedRatings[session.Id].Rating!.Value
            }).ToList(),
            SubmittedBy = accessService.CurrentUsername(User)
        };
        attendance.FeedbackAccessTokenUsedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RedirectToAction(nameof(Feedback), new { token = token.ToString("N") });
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception,
                "A duplicate or expired employee feedback submission was rejected for induction {InductionId}.",
                induction.Id);
            return RedirectToAction(nameof(Feedback), new { token = token.ToString("N") });
        }

        return RedirectToAction(nameof(Completed), new { type = "feedback" });
    }

    [HttpGet("Completed/{type}")]
    public IActionResult Completed(string type)
    {
        var isFeedback = string.Equals(type, "feedback", StringComparison.OrdinalIgnoreCase);
        return View(new EmployeePortalMessageViewModel
        {
            Title = isFeedback ? "Feedback submitted" : "Attendance confirmed",
            Message = isFeedback
                ? "Thank you. Your feedback and evaluation have been submitted successfully."
                : "Thank you. Your attendance response has been recorded successfully.",
            IsSuccess = true
        });
    }

    [HttpGet("Unavailable")]
    public IActionResult Unavailable() => PortalUnavailable();

    private IQueryable<ProgrammeAttendance> AttendanceTokenQuery() => dbContext.ProgrammeAttendances
        .Include(x => x.InductionCase)
        .Include(x => x.InductionProgramme)
            .ThenInclude(x => x.Sessions);

    private IQueryable<ProgrammeAttendance> FeedbackTokenQuery() => dbContext.ProgrammeAttendances
        .Include(x => x.InductionCase)
            .ThenInclude(x => x.EmployeeFeedback)
                .ThenInclude(x => x!.PresentationRatings)
                    .ThenInclude(x => x.InductionProgrammeSession)
        .Include(x => x.InductionProgramme)
            .ThenInclude(x => x.Sessions);

    private IActionResult? ValidateFeedbackAccess(ProgrammeAttendance? attendance)
    {
        if (attendance is null)
        {
            return PortalUnavailable();
        }
        if (!accessService.CanSelfConfirmAttendance(User, attendance.InductionCase))
        {
            return PortalUnavailable(StatusCodes.Status403Forbidden,
                "This invitation belongs to a different employee Windows account.");
        }
        if (attendance.Status != ProgrammeAttendanceStatus.Present)
        {
            return PortalUnavailable(message: "Attendance must be confirmed as present before feedback can be submitted.");
        }
        if (!InductionProgrammeRules.IsCompleted(attendance.InductionProgramme))
        {
            return PortalUnavailable(message: "Feedback is not available until the induction programme has been completed.");
        }
        if (!InductionProgrammeRules.RequiresFeedback(attendance.InductionProgramme))
        {
            return PortalUnavailable(message: "Employee feedback is not required for this induction programme.");
        }

        return null;
    }

    private IActionResult PortalUnavailable(
        int statusCode = StatusCodes.Status410Gone,
        string message = "This secure link is no longer available. It may have been replaced by a newer invitation.")
    {
        Response.StatusCode = statusCode;
        return View("Unavailable", new EmployeePortalMessageViewModel
        {
            Title = "Link unavailable",
            Message = message,
            IsSuccess = false
        });
    }

    private static EmployeeFeedbackPortalViewModel BuildFeedbackModel(
        ProgrammeAttendance attendance,
        Guid token,
        EmployeeFeedbackInputModel? feedback = null) => new()
    {
        Token = token,
        Programme = attendance.InductionProgramme,
        SubmittedFeedback = attendance.InductionCase.EmployeeFeedback,
        Feedback = feedback ?? new EmployeeFeedbackInputModel
        {
            PresentationRatings = attendance.InductionProgramme.Sessions
                .Where(x => x.RequiresFeedback)
                .OrderBy(x => x.SortOrder)
                .Select(x => new PresentationRatingInputModel { ProgrammeSessionId = x.Id })
                .ToList()
        }
    };

    private static EmployeeAttendancePortalViewModel BuildAttendanceModel(
        ProgrammeAttendance attendance,
        Guid token) => new()
    {
        Token = token,
        EmployeeName = attendance.InductionCase.EmployeeName,
        Programme = attendance.InductionProgramme,
        AttendanceStatus = attendance.Status,
        CanConfirm = !attendance.ConfirmedAtUtc.HasValue
            && attendance.Status == ProgrammeAttendanceStatus.Pending
    };
}
