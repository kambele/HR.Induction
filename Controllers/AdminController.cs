using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Namfisa.Induction.Data;
using Namfisa.Induction.Models;
using Namfisa.Induction.Services;
using Namfisa.Induction.ViewModels;

namespace Namfisa.Induction.Controllers;

public class AdminController(
    ApplicationDbContext dbContext,
    IUserAccessService accessService,
    IInductionWorkflowService workflow,
    IInductionEmailService emailService,
    ILogger<AdminController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!accessService.IsHr(User)) return Forbid();

        var templateUpdate = await dbContext.ChecklistTemplateItems.MaxAsync(x => (DateTime?)x.UpdatedAtUtc);
        var masterUpdate = await dbContext.MasterDataItems.MaxAsync(x => (DateTime?)x.UpdatedAtUtc);
        var workflowUpdate = await dbContext.WorkflowConfigurations.MaxAsync(x => (DateTime?)x.UpdatedAtUtc);

        return View(new AdminDashboardViewModel
        {
            ActiveChecklistItems = await dbContext.ChecklistTemplateItems.CountAsync(x => x.IsActive),
            InactiveChecklistItems = await dbContext.ChecklistTemplateItems.CountAsync(x => !x.IsActive),
            ActiveMasterDataItems = await dbContext.MasterDataItems.CountAsync(x => x.IsActive && x.Category != MasterDataCategory.Position),
            InactiveMasterDataItems = await dbContext.MasterDataItems.CountAsync(x => !x.IsActive && x.Category != MasterDataCategory.Position),
            LastConfigurationUpdateUtc = new[] { templateUpdate, masterUpdate, workflowUpdate }.Max()
        });
    }

    public async Task<IActionResult> MasterData(MasterDataCategory? category, string? search)
    {
        if (!accessService.IsHr(User)) return Forbid();

        if (category == MasterDataCategory.Position) category = null;
        var query = dbContext.MasterDataItems.AsNoTracking().Where(x => x.Category != MasterDataCategory.Position);
        if (category.HasValue) query = query.Where(x => x.Category == category.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Description != null && x.Description.Contains(term)));
        }

        return View(new MasterDataListViewModel
        {
            Category = category,
            Search = search,
            Items = await query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeRecords(string? search)
    {
        if (!accessService.IsHr(User)) return Forbid();

        var query = dbContext.InductionCases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.EmployeeName.Contains(term)
                || (x.EmployeeEmail != null && x.EmployeeEmail.Contains(term))
                || x.EmployeeUsername.Contains(term)
                || x.Department.Contains(term)
                || x.Division.Contains(term)
                || (x.EmployeeNumber != null && x.EmployeeNumber.Contains(term)));
        }

        var items = await query
            .OrderBy(x => x.EmployeeName)
            .Select(x => new EmployeeRecordAdministrationItemViewModel
            {
                Id = x.Id,
                AccessToken = x.AccessToken,
                EmployeeName = x.EmployeeName,
                EmployeeEmail = x.EmployeeEmail,
                Department = x.Department,
                Division = x.Division,
                ProgrammeTitle = x.InductionProgramme == null ? null : x.InductionProgramme.Title,
                Status = x.Status,
                AppointmentDate = x.AppointmentDate,
                FeedbackSubmittedAtUtc = x.EmployeeFeedback == null ? null : x.EmployeeFeedback.SubmittedAtUtc,
                TrainingEvaluationCount = x.TrainingEvaluations.Count
            })
            .ToListAsync(HttpContext.RequestAborted);

        return View(new EmployeeRecordAdministrationViewModel
        {
            Search = search,
            TotalInductions = await dbContext.InductionCases.CountAsync(HttpContext.RequestAborted),
            ProgrammeFeedbackResponses = await dbContext.EmployeeFeedback.CountAsync(HttpContext.RequestAborted),
            TrainingEvaluationResponses = await dbContext.TrainingEvaluations.CountAsync(HttpContext.RequestAborted),
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearEmployeeFeedback(EmployeeRecordAdministrationActionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var ids = SelectedInductionIds(model);
        if (ids.Length == 0)
        {
            TempData["Error"] = "Select at least one employee induction.";
            return RedirectToEmployeeRecords(model.ReturnSearch);
        }

        var programmeFeedback = await dbContext.EmployeeFeedback
            .Where(x => ids.Contains(x.InductionCaseId))
            .ToListAsync(HttpContext.RequestAborted);
        var trainingEvaluations = await dbContext.TrainingEvaluations
            .Where(x => ids.Contains(x.InductionCaseId))
            .ToListAsync(HttpContext.RequestAborted);

        if (programmeFeedback.Count == 0 && trainingEvaluations.Count == 0)
        {
            TempData["Error"] = "The selected employee inductions do not contain feedback responses.";
            return RedirectToEmployeeRecords(model.ReturnSearch);
        }

        dbContext.EmployeeFeedback.RemoveRange(programmeFeedback);
        dbContext.TrainingEvaluations.RemoveRange(trainingEvaluations);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        logger.LogWarning(
            "Administrator {Administrator} cleared {ProgrammeFeedbackCount} induction feedback response(s) and {TrainingEvaluationCount} training evaluation(s) for induction IDs {InductionIds}.",
            accessService.CurrentUsername(User),
            programmeFeedback.Count,
            trainingEvaluations.Count,
            ids);
        TempData["Success"] = $"Cleared {programmeFeedback.Count} induction feedback response{(programmeFeedback.Count == 1 ? string.Empty : "s")} and {trainingEvaluations.Count} training evaluation{(trainingEvaluations.Count == 1 ? string.Empty : "s")}.";
        return RedirectToEmployeeRecords(model.ReturnSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEmployeeInductions(EmployeeRecordAdministrationActionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var ids = SelectedInductionIds(model);
        if (ids.Length == 0)
        {
            TempData["Error"] = "Select at least one employee induction.";
            return RedirectToEmployeeRecords(model.ReturnSearch);
        }
        if (!string.Equals(model.ConfirmationText?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["Error"] = "Type DELETE to confirm permanent removal of employee induction records.";
            return RedirectToEmployeeRecords(model.ReturnSearch);
        }

        var inductionCases = await dbContext.InductionCases
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(HttpContext.RequestAborted);
        if (inductionCases.Count == 0)
        {
            TempData["Error"] = "The selected employee inductions could not be found.";
            return RedirectToEmployeeRecords(model.ReturnSearch);
        }

        var removedIds = inductionCases.Select(x => x.Id).ToArray();
        dbContext.InductionCases.RemoveRange(inductionCases);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        logger.LogWarning(
            "Administrator {Administrator} permanently deleted {InductionCount} employee induction record(s): {InductionIds}.",
            accessService.CurrentUsername(User),
            inductionCases.Count,
            removedIds);
        TempData["Success"] = $"Removed {inductionCases.Count} employee induction record{(inductionCases.Count == 1 ? string.Empty : "s")} and the associated workflow data.";
        return RedirectToEmployeeRecords(model.ReturnSearch);
    }

    [HttpGet]
    public async Task<IActionResult> ProgrammeRecords(string? search)
    {
        if (!accessService.IsHr(User)) return Forbid();

        var query = dbContext.InductionProgrammes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Title.Contains(term) || (x.Venue != null && x.Venue.Contains(term)));
        }

        var items = await query
            .OrderByDescending(x => x.ProgrammeDate)
            .ThenBy(x => x.Title)
            .Select(x => new ProgrammeRecordAdministrationItemViewModel
            {
                Id = x.Id,
                Title = x.Title,
                ProgrammeDate = x.ProgrammeDate,
                Venue = x.Venue,
                Status = x.Status,
                ParticipantCount = x.Participants.Count,
                SessionCount = x.Sessions.Count,
                FeedbackResponseCount = x.Participants.Count(participant => participant.EmployeeFeedback != null),
                PresentationRatingCount = x.Sessions.SelectMany(session => session.Ratings).Count()
            })
            .ToListAsync(HttpContext.RequestAborted);

        return View(new ProgrammeRecordAdministrationViewModel
        {
            Search = search,
            TotalProgrammes = await dbContext.InductionProgrammes.CountAsync(HttpContext.RequestAborted),
            AssignedEmployees = await dbContext.InductionCases.CountAsync(x => x.InductionProgrammeId != null, HttpContext.RequestAborted),
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInductionProgrammes(ProgrammeRecordAdministrationActionViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var ids = (model.ProgrammeIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Take(200)
            .ToArray();
        if (ids.Length == 0)
        {
            TempData["Error"] = "Select at least one induction programme.";
            return RedirectToProgrammeRecords(model.ReturnSearch);
        }
        if (!string.Equals(model.ConfirmationText?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["Error"] = "Type DELETE to confirm permanent removal of induction programmes.";
            return RedirectToProgrammeRecords(model.ReturnSearch);
        }

        var blockedProgrammes = await dbContext.InductionProgrammes
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id)
                && (x.Participants.Any(participant => participant.EmployeeFeedback != null)
                    || x.Sessions.Any(session => session.Ratings.Any())))
            .Select(x => x.Title)
            .ToListAsync(HttpContext.RequestAborted);
        if (blockedProgrammes.Count > 0)
        {
            var examples = string.Join(", ", blockedProgrammes.Take(3));
            TempData["Error"] = $"Programme deletion was stopped because feedback exists for: {examples}. Clear the affected employee feedback first.";
            return RedirectToProgrammeRecords(model.ReturnSearch);
        }

        var programmes = await dbContext.InductionProgrammes
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(HttpContext.RequestAborted);
        if (programmes.Count == 0)
        {
            TempData["Error"] = "The selected induction programmes could not be found.";
            return RedirectToProgrammeRecords(model.ReturnSearch);
        }

        var removedIds = programmes.Select(x => x.Id).ToArray();
        dbContext.InductionProgrammes.RemoveRange(programmes);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        logger.LogWarning(
            "Administrator {Administrator} permanently deleted {ProgrammeCount} induction programme(s): {ProgrammeIds}.",
            accessService.CurrentUsername(User),
            programmes.Count,
            removedIds);
        TempData["Success"] = $"Removed {programmes.Count} induction programme{(programmes.Count == 1 ? string.Empty : "s")}. Assigned employee induction records were retained and unassigned.";
        return RedirectToProgrammeRecords(model.ReturnSearch);
    }

    [HttpGet]
    public IActionResult CreateMasterData(MasterDataCategory? category)
    {
        if (!accessService.IsHr(User)) return Forbid();
        return View("MasterDataEditor", new MasterDataEditViewModel
        {
            Category = category is null or MasterDataCategory.Position ? MasterDataCategory.Department : category.Value,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMasterData(MasterDataEditViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (model.Category == MasterDataCategory.Position)
        {
            ModelState.AddModelError(nameof(model.Category), "Position is entered as short text and is not managed as master data.");
        }
        await ValidateMasterDataNameAsync(model);
        if (!ModelState.IsValid) return View("MasterDataEditor", model);

        dbContext.MasterDataItems.Add(new MasterDataItem
        {
            Category = model.Category,
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            SortOrder = model.SortOrder,
            IsActive = model.IsActive,
            UpdatedBy = accessService.CurrentUsername(User)
        });
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Master data item created.";
        return RedirectToAction(nameof(MasterData), new { category = model.Category });
    }

    [HttpGet]
    public async Task<IActionResult> EditMasterData(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var item = await dbContext.MasterDataItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (item.Category == MasterDataCategory.Position)
        {
            TempData["Error"] = "Position is now entered as short text and is not managed as master data.";
            return RedirectToAction(nameof(MasterData));
        }

        return View("MasterDataEditor", new MasterDataEditViewModel
        {
            Id = item.Id,
            Category = item.Category,
            Name = item.Name,
            Description = item.Description,
            SortOrder = item.SortOrder,
            IsActive = item.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMasterData(MasterDataEditViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (!model.Id.HasValue) return BadRequest();

        var item = await dbContext.MasterDataItems.SingleOrDefaultAsync(x => x.Id == model.Id.Value);
        if (item is null) return NotFound();
        if (item.Category == MasterDataCategory.Position || model.Category == MasterDataCategory.Position)
        {
            TempData["Error"] = "Position is now entered as short text and is not managed as master data.";
            return RedirectToAction(nameof(MasterData));
        }
        await ValidateMasterDataNameAsync(model);
        if (!ModelState.IsValid) return View("MasterDataEditor", model);

        item.Category = model.Category;
        item.Name = model.Name.Trim();
        item.Description = model.Description?.Trim();
        item.SortOrder = model.SortOrder;
        item.IsActive = model.IsActive;
        Stamp(item);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Master data item updated.";
        return RedirectToAction(nameof(MasterData), new { category = model.Category });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMasterData(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var item = await dbContext.MasterDataItems.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (item.Category == MasterDataCategory.Position)
        {
            TempData["Error"] = "Position is now entered as short text and is not managed as master data.";
            return RedirectToAction(nameof(MasterData));
        }

        item.IsActive = !item.IsActive;
        Stamp(item);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = item.IsActive ? "Master data item activated." : "Master data item archived.";
        return RedirectToAction(nameof(MasterData), new { category = item.Category });
    }

    public async Task<IActionResult> ChecklistTemplates(InductionPhase? phase, string? search, bool showInactive)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var query = dbContext.ChecklistTemplateItems.AsNoTracking().AsQueryable();
        if (phase.HasValue) query = query.Where(x => x.Phase == phase.Value);
        if (!showInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.Section.Contains(term) || x.ResponsibleRole.Contains(term));
        }

        return View(new ChecklistTemplateListViewModel
        {
            Phase = phase,
            Search = search,
            ShowInactive = showInactive,
            Items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateChecklistTemplate()
    {
        if (!accessService.IsHr(User)) return Forbid();
        var nextOrder = (await dbContext.ChecklistTemplateItems.MaxAsync(x => (int?)x.SortOrder) ?? 0) + 10;
        var model = new ChecklistTemplateEditViewModel { SortOrder = nextOrder, IsActive = true, IsMandatory = true };
        await LoadTemplateOptionsAsync(model);
        return View("ChecklistTemplateEditor", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChecklistTemplate(ChecklistTemplateEditViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadTemplateOptionsAsync(model);
            return View("ChecklistTemplateEditor", model);
        }

        dbContext.ChecklistTemplateItems.Add(new ChecklistTemplateItem
        {
            Section = model.Section.Trim(),
            Title = model.Title.Trim(),
            Phase = model.Phase,
            ResponsibleRole = model.ResponsibleRole.Trim(),
            DueOffsetDays = model.DueOffsetDays,
            SortOrder = model.SortOrder,
            IsMandatory = model.IsMandatory,
            IsActive = model.IsActive,
            UpdatedBy = accessService.CurrentUsername(User)
        });
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Checklist activity created. It will be included in future induction plans.";
        return RedirectToAction(nameof(ChecklistTemplates));
    }

    [HttpGet]
    public async Task<IActionResult> EditChecklistTemplate(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var item = await dbContext.ChecklistTemplateItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();

        var model = new ChecklistTemplateEditViewModel
        {
            Id = item.Id,
            Section = item.Section,
            Title = item.Title,
            Phase = item.Phase,
            ResponsibleRole = item.ResponsibleRole,
            DueOffsetDays = item.DueOffsetDays,
            SortOrder = item.SortOrder,
            IsMandatory = item.IsMandatory,
            IsActive = item.IsActive
        };
        await LoadTemplateOptionsAsync(model);
        return View("ChecklistTemplateEditor", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChecklistTemplate(ChecklistTemplateEditViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (!model.Id.HasValue) return BadRequest();
        var item = await dbContext.ChecklistTemplateItems.SingleOrDefaultAsync(x => x.Id == model.Id.Value);
        if (item is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadTemplateOptionsAsync(model);
            return View("ChecklistTemplateEditor", model);
        }

        item.Section = model.Section.Trim();
        item.Title = model.Title.Trim();
        item.Phase = model.Phase;
        item.ResponsibleRole = model.ResponsibleRole.Trim();
        item.DueOffsetDays = model.DueOffsetDays;
        item.SortOrder = model.SortOrder;
        item.IsMandatory = model.IsMandatory;
        item.IsActive = model.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedBy = accessService.CurrentUsername(User);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Checklist activity updated. Existing employee records were not changed.";
        return RedirectToAction(nameof(ChecklistTemplates), new { phase = model.Phase, showInactive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleChecklistTemplate(int id)
    {
        if (!accessService.IsHr(User)) return Forbid();
        var item = await dbContext.ChecklistTemplateItems.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();

        item.IsActive = !item.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedBy = accessService.CurrentUsername(User);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = item.IsActive ? "Checklist activity activated." : "Checklist activity archived.";
        return RedirectToAction(nameof(ChecklistTemplates), new { phase = item.Phase, showInactive = true });
    }

    [HttpGet]
    public async Task<IActionResult> WorkflowSettings()
    {
        if (!accessService.IsHr(User)) return Forbid();
        var settings = await dbContext.WorkflowConfigurations.AsNoTracking().OrderBy(x => x.Id).FirstAsync();
        return View(ToViewModel(settings, emailService.ConfigurationStatus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkflowSettings(WorkflowSettingsViewModel model)
    {
        if (!accessService.IsHr(User)) return Forbid();
        if (!ModelState.IsValid)
        {
            ApplySmtpStatus(model, emailService.ConfigurationStatus);
            return View(model);
        }

        var settings = await dbContext.WorkflowConfigurations.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new WorkflowConfiguration();
            dbContext.WorkflowConfigurations.Add(settings);
        }

        settings.RequireMandatoryTasksBeforeSignOff = model.RequireMandatoryTasksBeforeSignOff;
        settings.RequireEmployeeFeedbackBeforeSignOff = model.RequireEmployeeFeedbackBeforeSignOff;
        settings.EnforceSequentialSignOff = model.EnforceSequentialSignOff;
        settings.LockChecklistWhenSignOffStarts = model.LockChecklistWhenSignOffStarts;
        settings.EmailSubjectTemplate = model.EmailSubjectTemplate.Trim();
        settings.EmailBodyTemplate = model.EmailBodyTemplate.Trim();
        settings.EmployeeAcknowledgement = model.EmployeeAcknowledgement.Trim();
        settings.LineManagerAcknowledgement = model.LineManagerAcknowledgement.Trim();
        settings.HrBusinessPartnerAcknowledgement = model.HrBusinessPartnerAcknowledgement.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.UpdatedBy = accessService.CurrentUsername(User);
        await dbContext.SaveChangesAsync();

        var activeCases = await dbContext.InductionCases
            .Include(x => x.Tasks)
            .Include(x => x.SignOffs)
            .Where(x => x.Status != InductionStatus.Completed && x.Status != InductionStatus.OnHold)
            .ToListAsync();
        foreach (var inductionCase in activeCases)
        {
            await workflow.RefreshStatusAsync(inductionCase);
        }
        await dbContext.SaveChangesAsync();

        TempData["Success"] = "Workflow and communication settings updated.";
        return RedirectToAction(nameof(WorkflowSettings));
    }

    private async Task ValidateMasterDataNameAsync(MasterDataEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name)) return;
        var normalizedName = model.Name.Trim();
        var maximumLength = model.Category switch
        {
            MasterDataCategory.EmployeeTitle => 30,
            MasterDataCategory.Gender => 50,
            MasterDataCategory.ResponsibleRole => 80,
            _ => 120
        };
        if (normalizedName.Length > maximumLength)
        {
            ModelState.AddModelError(nameof(model.Name), $"This data type allows a maximum of {maximumLength} characters.");
            return;
        }
        var duplicate = await dbContext.MasterDataItems.AnyAsync(x =>
            x.Category == model.Category
            && x.Name == normalizedName
            && (!model.Id.HasValue || x.Id != model.Id.Value));
        if (duplicate) ModelState.AddModelError(nameof(model.Name), "This value already exists in the selected data type.");
    }

    private static int[] SelectedInductionIds(EmployeeRecordAdministrationActionViewModel model) =>
        (model.InductionCaseIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Take(500)
            .ToArray();

    private RedirectToActionResult RedirectToEmployeeRecords(string? search) =>
        RedirectToAction(nameof(EmployeeRecords), new { search = string.IsNullOrWhiteSpace(search) ? null : search.Trim() });

    private RedirectToActionResult RedirectToProgrammeRecords(string? search) =>
        RedirectToAction(nameof(ProgrammeRecords), new { search = string.IsNullOrWhiteSpace(search) ? null : search.Trim() });

    private async Task LoadTemplateOptionsAsync(ChecklistTemplateEditViewModel model)
    {
        model.SectionOptions = await MasterOptionsAsync(MasterDataCategory.ChecklistSection, model.Section);
        model.ResponsibleRoleOptions = await MasterOptionsAsync(MasterDataCategory.ResponsibleRole, model.ResponsibleRole);
    }

    private async Task<IReadOnlyList<SelectListItem>> MasterOptionsAsync(MasterDataCategory category, string? selected)
    {
        var names = await dbContext.MasterDataItems
            .AsNoTracking()
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(selected) && !names.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            names.Insert(0, selected);
        }
        return names.Select(x => new SelectListItem(x, x, string.Equals(x, selected, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private void Stamp(MasterDataItem item)
    {
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedBy = accessService.CurrentUsername(User);
    }

    private static WorkflowSettingsViewModel ToViewModel(
        WorkflowConfiguration settings,
        SmtpConfigurationStatus smtpStatus) => new()
    {
        RequireMandatoryTasksBeforeSignOff = settings.RequireMandatoryTasksBeforeSignOff,
        RequireEmployeeFeedbackBeforeSignOff = settings.RequireEmployeeFeedbackBeforeSignOff,
        EnforceSequentialSignOff = settings.EnforceSequentialSignOff,
        LockChecklistWhenSignOffStarts = settings.LockChecklistWhenSignOffStarts,
        EmailSubjectTemplate = settings.EmailSubjectTemplate,
        EmailBodyTemplate = settings.EmailBodyTemplate,
        SmtpConfigured = smtpStatus.IsConfigured,
        SmtpConfigurationSummary = smtpStatus.Summary,
        EmployeeAcknowledgement = settings.EmployeeAcknowledgement,
        LineManagerAcknowledgement = settings.LineManagerAcknowledgement,
        HrBusinessPartnerAcknowledgement = settings.HrBusinessPartnerAcknowledgement
    };

    private static void ApplySmtpStatus(WorkflowSettingsViewModel model, SmtpConfigurationStatus smtpStatus)
    {
        model.SmtpConfigured = smtpStatus.IsConfigured;
        model.SmtpConfigurationSummary = smtpStatus.Summary;
    }
}
