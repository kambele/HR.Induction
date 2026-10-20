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
        if (attendance.ConfirmedAtUtc.HasValue || attendance.Status != ProgrammeAttendanceStatus.Pending)
        {
            return PortalUnavailable();
        }

        return View(new EmployeeAttendancePortalViewModel
        {
            Token = token,
            EmployeeName = attendance.InductionCase.EmployeeName,
            ProgrammeTitle = attendance.InductionProgramme.Title
        });
    }

    [HttpPost("Attendance/{token:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attendance(Guid token, ProgrammeAttendanceStatus status)
    {
        var attendance = await AttendanceTokenQuery()
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
            return View(new EmployeeAttendancePortalViewModel
            {
                Token = token,
                EmployeeName = attendance.InductionCase.EmployeeName,
                ProgrammeTitle = attendance.InductionProgramme.Title
            });
        }

        attendance.Status = status;
        attendance.ConfirmedBy = accessService.CurrentUsername(User);
        attendance.ConfirmedBySid = (User.Identity as WindowsIdentity)?.User?.Value;
        attendance.AuthenticationType = User.Identity?.AuthenticationType;
        attendance.ConfirmationSource = "Employee one-time link";
        attendance.ConfirmedAtUtc = DateTime.UtcNow;
        attendance.AttendanceAccessToken = null;
        attendance.AttendanceAccessTokenUsedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
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
                attendance.CalendarInvitationSentAtUtc = DateTime.UtcNow;
                attendance.CalendarInvitationSentBy = accessService.CurrentUsername(User);
                attendance.CalendarInvitationSentTo = attendance.InductionCase.EmployeeEmail.Trim();
                await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
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

        var induction = attendance!.InductionCase;
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
        attendance.FeedbackAccessToken = null;
        attendance.FeedbackAccessTokenUsedAtUtc = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return PortalUnavailable();
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception,
                "A duplicate or expired employee feedback submission was rejected for induction {InductionId}.",
                induction.Id);
            return PortalUnavailable();
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
        if (attendance.InductionCase.EmployeeFeedback is not null)
        {
            return PortalUnavailable();
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
        string message = "This one-time link is no longer available. It may already have been used or replaced by a newer invitation.")
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
        Feedback = feedback ?? new EmployeeFeedbackInputModel
        {
            PresentationRatings = attendance.InductionProgramme.Sessions
                .Where(x => x.RequiresFeedback)
                .OrderBy(x => x.SortOrder)
                .Select(x => new PresentationRatingInputModel { ProgrammeSessionId = x.Id })
                .ToList()
        }
    };
}
