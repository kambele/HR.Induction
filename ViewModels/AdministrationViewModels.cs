using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.ViewModels;

public class AdminDashboardViewModel
{
    public int ActiveChecklistItems { get; set; }
    public int InactiveChecklistItems { get; set; }
    public int ActiveMasterDataItems { get; set; }
    public int InactiveMasterDataItems { get; set; }
    public DateTime? LastConfigurationUpdateUtc { get; set; }
}

public class EmployeeRecordAdministrationViewModel
{
    public string? Search { get; set; }
    public int TotalInductions { get; set; }
    public int ProgrammeFeedbackResponses { get; set; }
    public int TrainingEvaluationResponses { get; set; }
    public IReadOnlyList<EmployeeRecordAdministrationItemViewModel> Items { get; set; } = [];
}

public class EmployeeRecordAdministrationItemViewModel
{
    public int Id { get; set; }
    public Guid AccessToken { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string? ProgrammeTitle { get; set; }
    public InductionStatus Status { get; set; }
    public DateTime AppointmentDate { get; set; }
    public DateTime? FeedbackSubmittedAtUtc { get; set; }
    public int TrainingEvaluationCount { get; set; }
    public bool HasFeedback => FeedbackSubmittedAtUtc.HasValue || TrainingEvaluationCount > 0;
}

public class EmployeeRecordAdministrationActionViewModel
{
    [Required, MinLength(1)]
    public int[] InductionCaseIds { get; set; } = [];

    public string? ReturnSearch { get; set; }

    [StringLength(20)]
    public string? ConfirmationText { get; set; }
}

public class ProgrammeRecordAdministrationViewModel
{
    public string? Search { get; set; }
    public int TotalProgrammes { get; set; }
    public int AssignedEmployees { get; set; }
    public IReadOnlyList<ProgrammeRecordAdministrationItemViewModel> Items { get; set; } = [];
}

public class ProgrammeRecordAdministrationItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ProgrammeDate { get; set; }
    public string? Venue { get; set; }
    public InductionProgrammeStatus Status { get; set; }
    public int ParticipantCount { get; set; }
    public int SessionCount { get; set; }
    public int FeedbackResponseCount { get; set; }
    public int PresentationRatingCount { get; set; }
    public bool CanDelete => FeedbackResponseCount == 0 && PresentationRatingCount == 0;
}

public class ProgrammeRecordAdministrationActionViewModel
{
    [Required, MinLength(1)]
    public int[] ProgrammeIds { get; set; } = [];

    public string? ReturnSearch { get; set; }

    [StringLength(20)]
    public string? ConfirmationText { get; set; }
}

public class MasterDataListViewModel
{
    public MasterDataCategory? Category { get; set; }
    public string? Search { get; set; }
    public IReadOnlyList<MasterDataItem> Items { get; set; } = [];
}

public class MasterDataEditViewModel
{
    public int? Id { get; set; }

    [Required, Display(Name = "Data type")]
    public MasterDataCategory Category { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    [Range(0, 10000), Display(Name = "Display order")]
    public int SortOrder { get; set; }

    [Display(Name = "Available for use")]
    public bool IsActive { get; set; } = true;
}

public class ChecklistTemplateListViewModel
{
    public InductionPhase? Phase { get; set; }
    public string? Search { get; set; }
    public bool ShowInactive { get; set; }
    public IReadOnlyList<ChecklistTemplateItem> Items { get; set; } = [];
}

public class ChecklistTemplateEditViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(120)]
    public string Section { get; set; } = string.Empty;

    [Required, StringLength(400), Display(Name = "Activity wording")]
    public string Title { get; set; } = string.Empty;

    [Required]
    public InductionPhase Phase { get; set; }

    [Required, StringLength(80), Display(Name = "Responsible role")]
    public string ResponsibleRole { get; set; } = string.Empty;

    [Range(-365, 3650), Display(Name = "Due offset in days")]
    public int DueOffsetDays { get; set; }

    [Range(0, 10000), Display(Name = "Display order")]
    public int SortOrder { get; set; }

    [Display(Name = "Required activity")]
    public bool IsMandatory { get; set; } = true;

    [Display(Name = "Available for new inductions")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<SelectListItem> SectionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ResponsibleRoleOptions { get; set; } = [];
}

public class WorkflowSettingsViewModel
{
    [Display(Name = "Complete required checklist activities before sign-off")]
    public bool RequireMandatoryTasksBeforeSignOff { get; set; }

    [Display(Name = "Require employee feedback before employee sign-off")]
    public bool RequireEmployeeFeedbackBeforeSignOff { get; set; }

    [Display(Name = "Enforce employee, line manager, then HRBP sign-off order")]
    public bool EnforceSequentialSignOff { get; set; }

    [Display(Name = "Lock the checklist after sign-off starts")]
    public bool LockChecklistWhenSignOffStarts { get; set; }

    [Required, StringLength(300), Display(Name = "Email subject")]
    public string EmailSubjectTemplate { get; set; } = string.Empty;

    [Required, StringLength(4000), Display(Name = "Email message")]
    public string EmailBodyTemplate { get; set; } = string.Empty;

    public bool SmtpConfigured { get; set; }
    public string SmtpConfigurationSummary { get; set; } = string.Empty;

    [Required, StringLength(1000), Display(Name = "Employee acknowledgement")]
    public string EmployeeAcknowledgement { get; set; } = string.Empty;

    [Required, StringLength(1000), Display(Name = "Line manager acknowledgement")]
    public string LineManagerAcknowledgement { get; set; } = string.Empty;

    [Required, StringLength(1000), Display(Name = "HR business partner acknowledgement")]
    public string HrBusinessPartnerAcknowledgement { get; set; } = string.Empty;
}
