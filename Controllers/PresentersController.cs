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
    IInductionWorkflowService workflowService,
    IWorkflowConfigurationService workflowConfigurationService,
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

        return View(await BuildProgrammeModelAsync(programme));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePresentationCompletion(UpdateProgrammeSessionCompletionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();

        var programme = await dbContext.InductionProgrammes
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var session = programme.Sessions.SingleOrDefault(x => x.Id == model.SessionId);
        if (session is null) return NotFound();

        var username = accessService.CurrentUsername(User);
        session.CompletedAtUtc = model.IsCompleted ? DateTime.UtcNow : null;
        session.CompletedBy = model.IsCompleted ? username : null;
        if (programme.Status != InductionProgrammeStatus.Cancelled)
        {
            var allPresentationsComplete = programme.Sessions.Count > 0
                && programme.Sessions.All(x => x.CompletedAtUtc.HasValue);
            if (allPresentationsComplete)
            {
                programme.Status = InductionProgrammeStatus.Completed;
            }
            else if (programme.Status == InductionProgrammeStatus.Completed)
            {
                programme.Status = InductionProgrammeStatus.Scheduled;
            }
            programme.UpdatedBy = username;
            programme.UpdatedAtUtc = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["Success"] = model.IsCompleted
            ? $"{session.Division} presentation marked complete."
            : $"{session.Division} presentation reopened.";
        return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgrammeChecklistItem(UpdateProgrammeChecklistItemViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (!ModelState.IsValid || !model.Status.HasValue)
        {
            TempData["Error"] = "Select a valid checklist status and use DOMAIN\\username when entering an assignee.";
            return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
        }
        if (model.Status == InductionTaskStatus.NotApplicable && string.IsNullOrWhiteSpace(model.Notes))
        {
            TempData["Error"] = "Add a reason before marking a checklist activity not applicable.";
            return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
        }

        var programme = await dbContext.InductionProgrammes
            .AsSplitQuery()
            .Include(x => x.ChecklistPhases)
            .Include(x => x.Participants)
                .ThenInclude(x => x.Tasks)
            .Include(x => x.Participants)
                .ThenInclude(x => x.SignOffs)
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var applicablePhases = programme.ChecklistPhases.Count == 0
            ? Enum.GetValues<InductionPhase>().ToHashSet()
            : programme.ChecklistPhases.Select(x => x.Phase).ToHashSet();
        var template = await dbContext.ChecklistTemplateItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == model.ChecklistTemplateItemId
                     && x.IsActive
                     && applicablePhases.Contains(x.Phase),
                HttpContext.RequestAborted);
        if (template is null)
        {
            TempData["Error"] = "The checklist activity is not active for this programme.";
            return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
        }

        var settings = await workflowConfigurationService.GetAsync(HttpContext.RequestAborted);
        var username = accessService.CurrentUsername(User);
        var updated = 0;
        var protectedRecords = 0;
        foreach (var induction in programme.Participants)
        {
            if (settings.LockChecklistWhenSignOffStarts && induction.SignOffs.Count > 0)
            {
                protectedRecords++;
                continue;
            }

            await workflowService.RefreshTasksFromProgrammeAsync(induction, HttpContext.RequestAborted);
            var task = induction.Tasks.FirstOrDefault(x =>
                x.ChecklistTemplateItemId == model.ChecklistTemplateItemId
                && !x.ExcludedByProgramme);
            if (task is null) continue;

            task.Status = model.Status.Value;
            task.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            if (!string.IsNullOrWhiteSpace(model.AssignedToUsername))
            {
                task.AssignedToUsername = model.AssignedToUsername.Trim();
            }
            if (model.DueDate.HasValue)
            {
                task.DueDate = model.DueDate.Value.Date;
            }
            task.CompletedBy = model.Status == InductionTaskStatus.NotStarted ? null : username;
            task.CompletedAtUtc = model.Status == InductionTaskStatus.NotStarted ? null : DateTime.UtcNow;
            await workflowService.RefreshStatusAsync(induction, HttpContext.RequestAborted);
            updated++;
        }

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        TempData[updated > 0 ? "Success" : "Error"] = updated > 0
            ? $"{template.Title} updated for {updated} employee induction{(updated == 1 ? string.Empty : "s")}."
            : "No employee induction checklist was updated.";
        if (protectedRecords > 0)
        {
            TempData["Warning"] = $"{protectedRecords} employee induction{(protectedRecords == 1 ? " was" : "s were")} skipped because sign-off had started.";
        }

        return RedirectToAction(nameof(Programme), new { id = model.ProgrammeId });
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
    public async Task<IActionResult> SendFeedbackInvitations(SendFeedbackInvitationsViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();

        var programme = await dbContext.InductionProgrammes
            .Include(x => x.Sessions)
            .Include(x => x.Participants)
                .ThenInclude(x => x.ProgrammeAttendance)
            .Include(x => x.Participants)
                .ThenInclude(x => x.EmployeeFeedback)
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        if (!InductionProgrammeRules.IsCompleted(programme))
        {
            TempData["Error"] = "Feedback invitations become available only after the programme is completed.";
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }
        if (!InductionProgrammeRules.RequiresFeedback(programme))
        {
            TempData["Error"] = "Employee feedback is not required for this induction programme.";
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }
        if (!ModelState.IsValid || model.InductionCaseIds.Length == 0)
        {
            TempData["Error"] = "Select at least one eligible employee to invite for feedback.";
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }
        if (!emailService.ConfigurationStatus.IsConfigured)
        {
            TempData["Error"] = emailService.ConfigurationStatus.Summary;
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }

        var selectedIds = model.InductionCaseIds.Distinct().Take(200).ToHashSet();
        var employees = programme.Participants
            .Where(x => selectedIds.Contains(x.Id))
            .OrderBy(x => x.EmployeeName)
            .ToList();
        var sentBy = accessService.CurrentUsername(User);
        var sentCount = 0;
        var failures = new List<string>();

        foreach (var employee in employees)
        {
            if (employee.ProgrammeAttendance?.Status != ProgrammeAttendanceStatus.Present)
            {
                failures.Add($"{employee.EmployeeName}: attendance is not marked present.");
                continue;
            }
            if (employee.EmployeeFeedback is not null)
            {
                failures.Add($"{employee.EmployeeName}: feedback has already been submitted.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(employee.EmployeeEmail))
            {
                failures.Add($"{employee.EmployeeName}: email address is missing.");
                continue;
            }

            var feedbackToken = Guid.NewGuid();
            var feedbackUrl = BuildFeedbackUrl(feedbackToken);
            if (string.IsNullOrWhiteSpace(feedbackUrl))
            {
                failures.Add($"{employee.EmployeeName}: feedback link could not be created.");
                continue;
            }

            try
            {
                await emailService.SendFeedbackInvitationAsync(
                    employee,
                    programme,
                    feedbackUrl,
                    HttpContext.RequestAborted);

                var attendance = GetOrCreateAttendance(programme, employee);
                attendance.FeedbackAccessToken = feedbackToken;
                attendance.FeedbackAccessTokenIssuedAtUtc = DateTime.UtcNow;
                attendance.FeedbackAccessTokenUsedAtUtc = null;
                attendance.FeedbackInvitationSentAtUtc = DateTime.UtcNow;
                attendance.FeedbackInvitationSentBy = sentBy;
                attendance.FeedbackInvitationSentTo = employee.EmployeeEmail.Trim();
                await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
                sentCount++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Failed to send programme {ProgrammeId} feedback invitation to induction {InductionId}.",
                    programme.Id,
                    employee.Id);
                failures.Add($"{employee.EmployeeName}: delivery failed.");
            }
        }

        if (sentCount > 0)
        {
            TempData["Success"] = $"Sent {sentCount} feedback invitation{(sentCount == 1 ? string.Empty : "s")}.";
        }
        if (failures.Count > 0)
        {
            TempData["Error"] = $"{failures.Count} feedback invitation{(failures.Count == 1 ? string.Empty : "s")} could not be sent. {string.Join(" ", failures.Take(3))}";
        }

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

        if (!InductionProgrammeRules.CanSendAttendanceInvitations(programme, DateTime.Now))
        {
            TempData["Error"] = "Attendance invitations can be sent only before the programme starts.";
            return RedirectToAction(nameof(Programme), new { id = programme.Id });
        }

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
            if (attendee.ProgrammeAttendance?.ConfirmedAtUtc is not null
                || attendee.ProgrammeAttendance?.Status is ProgrammeAttendanceStatus.Present or ProgrammeAttendanceStatus.Absent)
            {
                failures.Add($"{attendee.EmployeeName}: attendance has already been confirmed.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(attendee.EmployeeEmail))
            {
                failures.Add($"{attendee.EmployeeName}: email address is missing.");
                continue;
            }

            var attendanceToken = Guid.NewGuid();
            var attendanceUrl = BuildAttendanceUrl(attendanceToken);
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
                attendance.AttendanceAccessToken = attendanceToken;
                attendance.AttendanceAccessTokenIssuedAtUtc = DateTime.UtcNow;
                attendance.AttendanceAccessTokenUsedAtUtc = null;
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
        .Include(x => x.ChecklistPhases)
        .Include(x => x.Participants)
            .ThenInclude(x => x.ProgrammeAttendance)
        .Include(x => x.Participants)
            .ThenInclude(x => x.EmployeeFeedback)
        .Include(x => x.Participants)
            .ThenInclude(x => x.Tasks)
        .Include(x => x.Participants)
            .ThenInclude(x => x.SignOffs)
        .Include(x => x.Attendances);

    private async Task<PresenterProgrammeViewModel> BuildProgrammeModelAsync(InductionProgramme programme)
    {
        var settings = await workflowConfigurationService.GetAsync(HttpContext.RequestAborted);
        var programmeStartsAt = InductionProgrammeRules.StartsAt(programme);
        var programmeCompleted = InductionProgrammeRules.IsCompleted(programme);
        var feedbackRequired = InductionProgrammeRules.RequiresFeedback(programme);
        var isHr = accessService.IsHr(User);
        var applicablePhases = programme.ChecklistPhases.Count == 0
            ? Enum.GetValues<InductionPhase>().ToHashSet()
            : programme.ChecklistPhases.Select(x => x.Phase).ToHashSet();
        var templates = await dbContext.ChecklistTemplateItems
            .AsNoTracking()
            .Where(x => x.IsActive && applicablePhases.Contains(x.Phase))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(HttpContext.RequestAborted);

        return new PresenterProgrammeViewModel
        {
            Programme = programme,
            CanManageChecklist = isHr,
            LockChecklistWhenSignOffStarts = settings.LockChecklistWhenSignOffStarts,
            CanSendAttendanceInvitations = InductionProgrammeRules.CanSendAttendanceInvitations(programme, DateTime.Now),
            CanSendFeedbackInvitations = isHr && programmeCompleted && feedbackRequired,
            FeedbackRequired = feedbackRequired,
            ProgrammeCompleted = programmeCompleted,
            ProgrammeStartsAt = programmeStartsAt,
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
                    InvitationSentAtUtc = induction.ProgrammeAttendance?.InvitationSentAtUtc,
                    FeedbackInvitationSentAtUtc = induction.ProgrammeAttendance?.FeedbackInvitationSentAtUtc,
                    HasFeedback = induction.EmployeeFeedback is not null
                })
                .ToList(),
            ChecklistItems = templates.Select(template =>
            {
                var tasks = programme.Participants
                    .SelectMany(x => x.Tasks)
                    .Where(x => x.ChecklistTemplateItemId == template.Id && !x.ExcludedByProgramme)
                    .ToList();
                return new PresenterChecklistItemViewModel
                {
                    ChecklistTemplateItemId = template.Id,
                    Section = template.Section,
                    Title = template.Title,
                    Phase = template.Phase,
                    ResponsibleRole = template.ResponsibleRole,
                    SortOrder = template.SortOrder,
                    ParticipantCount = programme.Participants.Count,
                    EmployeeTaskCount = tasks.Count,
                    CompletedCount = tasks.Count(x => x.Status == InductionTaskStatus.Completed),
                    NotApplicableCount = tasks.Count(x => x.Status == InductionTaskStatus.NotApplicable)
                };
            }).ToList()
        };
    }

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
        if (attendance.AttendanceAccessToken.HasValue)
        {
            attendance.AttendanceAccessToken = null;
            attendance.AttendanceAccessTokenUsedAtUtc = DateTime.UtcNow;
        }
    }

    private string? BuildAttendanceUrl(Guid token)
    {
        var tokenText = token.ToString("N");
        if (emailService.PublicBaseUrl is { Length: > 0 } publicBaseUrl)
        {
            return $"{publicBaseUrl.TrimEnd('/')}/Employee/Attendance/{tokenText}";
        }

        return Url.Action(
            "Attendance",
            "EmployeePortal",
            new { token = tokenText },
            Request.Scheme,
            Request.Host.Value);
    }

    private string? BuildFeedbackUrl(Guid token)
    {
        var tokenText = token.ToString("N");
        if (emailService.PublicBaseUrl is { Length: > 0 } publicBaseUrl)
        {
            return $"{publicBaseUrl.TrimEnd('/')}/Employee/Feedback/{tokenText}";
        }

        return Url.Action(
            "Feedback",
            "EmployeePortal",
            new { token = tokenText },
            Request.Scheme,
            Request.Host.Value);
    }
}
