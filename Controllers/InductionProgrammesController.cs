using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class InductionProgrammesController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService,
    IInductionReportService reportService,
    IInductionWorkflowService workflowService,
    IWorkflowConfigurationService workflowConfigurationService) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!accessService.IsHr(User)) return Forbid();

        var programmes = await dbContext.InductionProgrammes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Sessions)
            .Include(x => x.Participants)
                .ThenInclude(x => x.EmployeeFeedback)
            .OrderByDescending(x => x.ProgrammeDate)
            .ThenBy(x => x.Title)
            .ToListAsync(HttpContext.RequestAborted);
        return View(programmes);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await ProgrammeQuery().SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var settings = await workflowConfigurationService.GetAsync(HttpContext.RequestAborted);
        var employees = await dbContext.InductionCases
            .AsNoTracking()
            .OrderBy(x => x.EmployeeName)
            .Select(x => new ProgrammeEmployeeSelectionItemViewModel
            {
                Id = x.Id,
                AccessToken = x.AccessToken,
                EmployeeName = x.EmployeeName,
                EmployeeUsername = x.EmployeeUsername,
                Department = x.Department,
                Division = x.Division,
                Status = x.Status,
                CurrentProgrammeId = x.InductionProgrammeId,
                CurrentProgrammeTitle = x.InductionProgramme == null ? null : x.InductionProgramme.Title,
                ApplicableTaskCount = x.Tasks.Count(task => !task.ExcludedByProgramme),
                TotalTaskCount = x.Tasks.Count,
                SignOffCount = x.SignOffs.Count,
                HasFeedback = x.EmployeeFeedback != null
            })
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var employee in employees)
        {
            var signOffLocked = settings.LockChecklistWhenSignOffStarts && employee.SignOffCount > 0;
            employee.CanAssign = employee.CurrentProgrammeId == programme.Id
                || (!employee.HasFeedback && !signOffLocked);
            employee.CanApplyPhases = employee.CurrentProgrammeId == programme.Id && !signOffLocked;
            employee.ProtectionReason = employee.HasFeedback && employee.CurrentProgrammeId != programme.Id
                ? "Feedback already submitted"
                : signOffLocked
                    ? "Sign-off has started"
                    : null;
        }

        return View(new InductionProgrammeDetailsViewModel
        {
            Programme = programme,
            AssignedEmployees = employees.Where(x => x.CurrentProgrammeId == programme.Id).ToList(),
            AvailableEmployees = employees.Where(x => x.CurrentProgrammeId != programme.Id).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignEmployees(ProgrammeEmployeeSelectionInputModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await dbContext.InductionProgrammes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var selectedIds = (model.InductionCaseIds ?? []).Distinct().Take(500).ToArray();
        if (selectedIds.Length == 0)
        {
            TempData["Error"] = "Select at least one employee induction to add.";
            return RedirectToAction(nameof(Details), new { id = model.ProgrammeId });
        }

        var settings = await workflowConfigurationService.GetAsync(HttpContext.RequestAborted);
        var inductions = await dbContext.InductionCases
            .Include(x => x.ProgrammeAttendance)
            .Include(x => x.EmployeeFeedback)
            .Include(x => x.SignOffs)
            .Where(x => selectedIds.Contains(x.Id))
            .ToListAsync(HttpContext.RequestAborted);

        var assigned = 0;
        var unchanged = 0;
        var protectedRecords = 0;
        foreach (var induction in inductions)
        {
            if (induction.InductionProgrammeId == programme.Id)
            {
                unchanged++;
                continue;
            }

            var signOffLocked = settings.LockChecklistWhenSignOffStarts && induction.SignOffs.Count > 0;
            if (induction.EmployeeFeedback is not null || signOffLocked)
            {
                protectedRecords++;
                continue;
            }

            if (induction.ProgrammeAttendance is not null)
            {
                dbContext.ProgrammeAttendances.Remove(induction.ProgrammeAttendance);
                induction.ProgrammeAttendance = null;
            }

            induction.InductionProgrammeId = programme.Id;
            assigned++;
        }

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        var missing = selectedIds.Length - inductions.Count;
        TempData[assigned > 0 ? "Success" : "Error"] = assigned > 0
            ? $"{assigned} employee induction{(assigned == 1 ? " was" : "s were")} added to {programme.Title}. Use Apply selected phases to update their checklists."
            : "No employee inductions were added.";
        if (protectedRecords + missing > 0)
        {
            TempData["Warning"] = $"{protectedRecords + missing} selected record{(protectedRecords + missing == 1 ? " was" : "s were")} skipped because it was protected or unavailable.";
        }
        else if (unchanged > 0)
        {
            TempData["Warning"] = $"{unchanged} selected record{(unchanged == 1 ? " was" : "s were")} already assigned to this programme.";
        }

        return RedirectToAction(nameof(Details), new { id = model.ProgrammeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyChecklistPhases(ProgrammeEmployeeSelectionInputModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await dbContext.InductionProgrammes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == model.ProgrammeId, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var selectedIds = (model.InductionCaseIds ?? []).Distinct().Take(500).ToArray();
        if (selectedIds.Length == 0)
        {
            TempData["Error"] = "Select at least one assigned employee induction.";
            return RedirectToAction(nameof(Details), new { id = model.ProgrammeId });
        }

        var settings = await workflowConfigurationService.GetAsync(HttpContext.RequestAborted);
        var inductions = await dbContext.InductionCases
            .Include(x => x.Tasks)
            .Include(x => x.SignOffs)
            .Where(x => selectedIds.Contains(x.Id) && x.InductionProgrammeId == model.ProgrammeId)
            .ToListAsync(HttpContext.RequestAborted);

        var updated = 0;
        var addedTasks = 0;
        var includedTasks = 0;
        var excludedTasks = 0;
        var protectedRecords = 0;
        foreach (var induction in inductions)
        {
            if (settings.LockChecklistWhenSignOffStarts && induction.SignOffs.Count > 0)
            {
                protectedRecords++;
                continue;
            }

            var result = await workflowService.RefreshTasksFromProgrammeAsync(induction, HttpContext.RequestAborted);
            await workflowService.RefreshStatusAsync(induction, HttpContext.RequestAborted);
            addedTasks += result.Added;
            includedTasks += result.Included;
            excludedTasks += result.Excluded;
            updated++;
        }

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        var unavailable = selectedIds.Length - inductions.Count;
        TempData[updated > 0 ? "Success" : "Error"] = updated > 0
            ? $"Programme phases applied to {updated} employee induction{(updated == 1 ? string.Empty : "s")}. Checklist changes: {addedTasks} added, {includedTasks} restored and {excludedTasks} excluded."
            : "No employee induction checklists were updated.";
        if (protectedRecords + unavailable > 0)
        {
            TempData["Warning"] = $"{protectedRecords + unavailable} selected record{(protectedRecords + unavailable == 1 ? " was" : "s were")} skipped because sign-off had started or it was no longer assigned to this programme.";
        }

        return RedirectToAction(nameof(Details), new { id = model.ProgrammeId });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!accessService.IsHr(User)) return Forbid();
        var model = new InductionProgrammeEditorViewModel
        {
            ProgrammeDate = DateTime.Today,
            ApplicableChecklistPhases = Enum.GetValues<InductionPhase>().ToList(),
            Sessions = [new InductionProgrammeSessionInputModel
            {
                StartTime = new TimeSpan(8, 30, 0),
                EndTime = new TimeSpan(9, 0, 0)
            }]
        };
        await LoadDivisionOptionsAsync(model);
        return View("Editor", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InductionProgrammeEditorViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        await ValidateProgrammeAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadDivisionOptionsAsync(model);
            return View("Editor", model);
        }

        var username = accessService.CurrentUsername(User);
        var programme = new InductionProgramme
        {
            CreatedBy = username,
            UpdatedBy = username
        };
        ApplyEditor(programme, model, username);
        dbContext.InductionProgrammes.Add(programme);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["Success"] = $"Induction programme created for {programme.ProgrammeDate:dd MMMM yyyy}.";
        return RedirectToAction(nameof(Details), new { id = programme.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await dbContext.InductionProgrammes
            .AsNoTracking()
            .Include(x => x.Sessions)
            .Include(x => x.ChecklistPhases)
            .SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var model = new InductionProgrammeEditorViewModel
        {
            Id = programme.Id,
            Title = programme.Title,
            ProgrammeDate = programme.ProgrammeDate,
            Venue = programme.Venue,
            Notes = programme.Notes,
            Status = programme.Status,
            ApplicableChecklistPhases = programme.ChecklistPhases.Count == 0
                ? Enum.GetValues<InductionPhase>().ToList()
                : programme.ChecklistPhases.Select(x => x.Phase).OrderBy(x => x).ToList(),
            ScheduleLocked = await ProgrammeHasResponsesAsync(programme.Id),
            Sessions = programme.Sessions
                .OrderBy(x => x.SortOrder)
                .Select(x => new InductionProgrammeSessionInputModel
                {
                    Id = x.Id,
                    Division = x.Division,
                    PresenterName = x.PresenterName,
                    PresenterUsername = x.PresenterUsername,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime
                }).ToList()
        };
        await LoadDivisionOptionsAsync(model);
        return View("Editor", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InductionProgrammeEditorViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await dbContext.InductionProgrammes
            .Include(x => x.Sessions)
            .Include(x => x.ChecklistPhases)
            .SingleOrDefaultAsync(x => x.Id == model.Id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        model.ScheduleLocked = await ProgrammeHasResponsesAsync(programme.Id);
        if (model.ScheduleLocked)
        {
            foreach (var key in ModelState.Keys.Where(x => x.StartsWith("Sessions", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                ModelState.Remove(key);
            }
            model.Sessions = programme.Sessions.OrderBy(x => x.SortOrder).Select(x => new InductionProgrammeSessionInputModel
            {
                Id = x.Id,
                Division = x.Division,
                PresenterName = x.PresenterName,
                PresenterUsername = x.PresenterUsername,
                StartTime = x.StartTime,
                EndTime = x.EndTime
            }).ToList();
        }

        await ValidateProgrammeAsync(model, validateSessions: !model.ScheduleLocked);
        if (!ModelState.IsValid)
        {
            await LoadDivisionOptionsAsync(model);
            return View("Editor", model);
        }

        ApplyEditor(programme, model, accessService.CurrentUsername(User), updateSessions: !model.ScheduleLocked);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        TempData["Success"] = "Induction programme updated.";
        return RedirectToAction(nameof(Details), new { id = programme.Id });
    }

    public async Task<IActionResult> Report(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await ProgrammeQuery().SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var responses = programme.Participants
            .Select(x => x.EmployeeFeedback)
            .Where(x => x?.OverallProgrammeRating is not null)
            .Select(x => x!)
            .ToList();
        var allPresentationRatings = programme.Sessions.SelectMany(x => x.Ratings).ToList();
        var model = new InductionProgrammeReportViewModel
        {
            Programme = programme,
            ParticipantCount = programme.Participants.Count,
            ResponseCount = responses.Count,
            ResponseRate = programme.Participants.Count == 0 ? 0 : Math.Round(responses.Count * 100m / programme.Participants.Count, 1),
            AverageOverallRating = responses.Count == 0 ? 0 : responses.Average(x => x.OverallProgrammeRating!.Value),
            AveragePresentationRating = allPresentationRatings.Count == 0 ? 0 : allPresentationRatings.Average(x => x.Rating),
            DivisionRatings = programme.Sessions.OrderBy(x => x.SortOrder).Select(x => new DivisionRatingSummary(
                x.Division,
                x.PresenterName,
                x.Ratings.Count == 0 ? 0 : Math.Round(x.Ratings.Average(r => r.Rating), 2),
                x.Ratings.Count)).ToList(),
            OverallRatingDistribution = Enumerable.Range(1, 5)
                .Select(rating => new RatingCountSummary(rating, responses.Count(x => x.OverallProgrammeRating == rating)))
                .ToList(),
            TimeAllocationDistribution = Enum.GetValues<TimeAllocationResponse>()
                .Select(value => new NamedCountSummary(DisplayTimeAllocation(value), responses.Count(x => x.TimeAllocationResponse == value)))
                .ToList()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var programme = await ProgrammeQuery().SingleOrDefaultAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (programme is null) return NotFound();

        var report = reportService.GenerateProgramme(programme);
        return File(report, "application/pdf", $"Corporate-induction-programme-{programme.ProgrammeDate:yyyyMMdd}.pdf");
    }

    private IQueryable<InductionProgramme> ProgrammeQuery() => dbContext.InductionProgrammes
        .AsNoTracking()
        .AsSplitQuery()
        .Include(x => x.Sessions)
            .ThenInclude(x => x.Ratings)
        .Include(x => x.ChecklistPhases)
        .Include(x => x.Participants)
            .ThenInclude(x => x.EmployeeFeedback)
                .ThenInclude(x => x!.PresentationRatings)
        .Include(x => x.Attendances);

    private async Task ValidateProgrammeAsync(InductionProgrammeEditorViewModel model, bool validateSessions = true)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Venue = string.IsNullOrWhiteSpace(model.Venue) ? null : model.Venue.Trim();
        model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        var validPhases = Enum.GetValues<InductionPhase>().ToHashSet();
        model.ApplicableChecklistPhases = (model.ApplicableChecklistPhases ?? [])
            .Where(validPhases.Contains)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        if (model.ApplicableChecklistPhases.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ApplicableChecklistPhases), "Select at least one checklist phase.");
        }
        if (!validateSessions) return;

        var activeDivisions = await dbContext.MasterDataItems.AsNoTracking()
            .Where(x => x.Category == MasterDataCategory.Division && x.IsActive)
            .Select(x => x.Name)
            .ToListAsync(HttpContext.RequestAborted);
        var divisionLookup = activeDivisions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenDivisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < model.Sessions.Count; index++)
        {
            var session = model.Sessions[index];
            session.Division = session.Division?.Trim() ?? string.Empty;
            session.PresenterName = session.PresenterName?.Trim() ?? string.Empty;
            session.PresenterUsername = session.PresenterUsername?.Trim() ?? string.Empty;
            if (!divisionLookup.Contains(session.Division))
            {
                ModelState.AddModelError($"Sessions[{index}].Division", "Select an active division.");
            }
            if (session.Division.Length > 0 && !seenDivisions.Add(session.Division))
            {
                ModelState.AddModelError($"Sessions[{index}].Division", "Each division can appear only once in a programme.");
            }
            if (session.StartTime.HasValue && session.EndTime.HasValue && session.EndTime <= session.StartTime)
            {
                ModelState.AddModelError($"Sessions[{index}].EndTime", "End time must be after start time.");
            }
        }

        var ordered = model.Sessions
            .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
            .OrderBy(x => x.StartTime)
            .ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].StartTime < ordered[index - 1].EndTime)
            {
                ModelState.AddModelError(nameof(model.Sessions), "Presentation time slots must not overlap.");
                break;
            }
        }
    }

    private static void ApplyEditor(
        InductionProgramme programme,
        InductionProgrammeEditorViewModel model,
        string username,
        bool updateSessions = true)
    {
        programme.Title = model.Title.Trim();
        programme.ProgrammeDate = model.ProgrammeDate.Date;
        programme.Venue = model.Venue;
        programme.Notes = model.Notes;
        programme.Status = model.Status;
        programme.UpdatedBy = username;
        programme.UpdatedAtUtc = DateTime.UtcNow;

        var selectedPhases = model.ApplicableChecklistPhases.ToHashSet();
        foreach (var existingPhase in programme.ChecklistPhases.Where(x => !selectedPhases.Contains(x.Phase)).ToList())
        {
            programme.ChecklistPhases.Remove(existingPhase);
        }
        var existingPhases = programme.ChecklistPhases.Select(x => x.Phase).ToHashSet();
        foreach (var phase in selectedPhases.Where(x => !existingPhases.Contains(x)).OrderBy(x => x))
        {
            programme.ChecklistPhases.Add(new InductionProgrammeChecklistPhase { Phase = phase });
        }

        if (!updateSessions) return;
        programme.Sessions.Clear();
        var ordered = model.Sessions.OrderBy(x => x.StartTime).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            programme.Sessions.Add(new InductionProgrammeSession
            {
                Division = ordered[index].Division.Trim(),
                PresenterName = ordered[index].PresenterName.Trim(),
                PresenterUsername = ordered[index].PresenterUsername.Trim(),
                StartTime = ordered[index].StartTime!.Value,
                EndTime = ordered[index].EndTime!.Value,
                SortOrder = index + 1
            });
        }
    }

    private async Task LoadDivisionOptionsAsync(InductionProgrammeEditorViewModel model)
    {
        model.DivisionOptions = await dbContext.MasterDataItems.AsNoTracking()
            .Where(x => x.Category == MasterDataCategory.Division && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Name))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private Task<bool> ProgrammeHasResponsesAsync(int programmeId) => dbContext.PresentationRatings
        .AnyAsync(x => x.InductionProgrammeSession.InductionProgrammeId == programmeId, HttpContext.RequestAborted);

    private static string DisplayTimeAllocation(TimeAllocationResponse value) => value switch
    {
        TimeAllocationResponse.Appropriate => "Appropriate",
        TimeAllocationResponse.TooShort => "Too short",
        TimeAllocationResponse.TooLong => "Too long",
        _ => value.ToString()
    };
}
