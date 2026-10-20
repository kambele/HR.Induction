using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.ViewModels;

public class CreateInductionViewModel
{
    [Required, StringLength(30), Display(Name = "Title")]
    public string EmployeeTitle { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Gender { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Position { get; set; } = string.Empty;

    [Required, StringLength(150), RegularExpression(@"^[^\\]+\\[^\\]+$", ErrorMessage = "Use the AD ID format DOMAIN\\username."), Display(Name = "Employee AD ID")]
    public string EmployeeUsername { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254), Display(Name = "Employee email address")]
    public string EmployeeEmail { get; set; } = string.Empty;

    [StringLength(40), Display(Name = "Employee number")]
    public string? EmployeeNumber { get; set; }

    [Required, StringLength(120)]
    public string Department { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Division { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Appointment date")]
    public DateTime AppointmentDate { get; set; } = DateTime.Today;

    [Display(Name = "Induction programme")]
    public int? InductionProgrammeId { get; set; }

    [Required, StringLength(150), Display(Name = "Line manager name")]
    public string LineManagerName { get; set; } = string.Empty;

    [Required, StringLength(150), RegularExpression(@"^[^\\]+\\[^\\]+$", ErrorMessage = "Use the AD ID format DOMAIN\\username."), Display(Name = "Line manager AD ID")]
    public string LineManagerUsername { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "HR business partner name")]
    public string HrBusinessPartnerName { get; set; } = string.Empty;

    [Required, StringLength(150), RegularExpression(@"^[^\\]+\\[^\\]+$", ErrorMessage = "Use the AD ID format DOMAIN\\username."), Display(Name = "HR business partner AD ID")]
    public string HrBusinessPartnerUsername { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> EmployeeTitleOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> GenderOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> DivisionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ProgrammeOptions { get; set; } = [];
}

public class EditInductionViewModel : CreateInductionViewModel
{
    [Required]
    public int Id { get; set; }

    public Guid AccessToken { get; set; }
}

public class InductionImportResult
{
    public bool WasProcessed { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<InductionImportRowResult> Rows { get; set; } = [];

    public static InductionImportResult Invalid(string message) => new() { Message = message };
}

public record InductionImportRowResult(int RowNumber, string Employee, string Status, string Message);

public class InductionDetailsViewModel
{
    public required InductionCase Induction { get; set; }
    public int CompletedTaskCount { get; set; }
    public int TotalTaskCount { get; set; }
    public int ProgressPercentage => TotalTaskCount == 0 ? 0 : (int)Math.Round(CompletedTaskCount * 100d / TotalTaskCount);
    public bool CanManage { get; set; }
    public bool CanSubmitFeedback { get; set; }
    public bool CanSignEmployee { get; set; }
    public bool CanSignLineManager { get; set; }
    public bool CanSignHrBusinessPartner { get; set; }
    public bool LockChecklistWhenSignOffStarts { get; set; }
    public bool RequireMandatoryTasksBeforeSignOff { get; set; }
    public bool RequireEmployeeFeedbackBeforeSignOff { get; set; }
    public bool SmtpConfigured { get; set; }
    public IReadOnlyList<InductionPhase> ProgrammeChecklistPhases { get; set; } = [];
}

public class EmployeeFeedbackInputModel
{
    public int InductionCaseId { get; set; }

    [Required, Range(1, 5), Display(Name = "Understanding of the organisation and its divisions")]
    public int? ProgrammeUnderstandingRating { get; set; }

    [Required, Display(Name = "Presentation time allocation")]
    public TimeAllocationResponse? TimeAllocationResponse { get; set; }

    [Required, Range(1, 5), Display(Name = "Overall corporate induction programme rating")]
    public int? OverallProgrammeRating { get; set; }

    [Required, StringLength(2000), Display(Name = "Most valuable aspect of the induction programme")]
    public string MostValuableAspect { get; set; } = string.Empty;

    [Required, StringLength(2000), Display(Name = "Recommended improvements for future induction programmes")]
    public string ImprovementRecommendations { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<PresentationRatingInputModel> PresentationRatings { get; set; } = [];

    [Display(Name = "I understand my responsibilities and how I fit into the department or team")]
    public bool UnderstandsResponsibilities { get; set; }

    [Display(Name = "I am aware of the training and development opportunities available to me")]
    public bool AwareOfDevelopmentOpportunities { get; set; }

    [StringLength(2000), Display(Name = "What was missing from your induction?")]
    public string? MissingFromInduction { get; set; }

    [StringLength(2000), Display(Name = "Additional comments")]
    public string? AdditionalComments { get; set; }
}

public class EmployeeFeedbackPageViewModel
{
    public required InductionCase Induction { get; set; }
    public EmployeeFeedbackInputModel Feedback { get; set; } = new();
    public ProgrammeAttendanceStatus AttendanceStatus { get; set; }
}

public class PresentationRatingInputModel
{
    [Required]
    public int ProgrammeSessionId { get; set; }

    [Required, Range(1, 5)]
    public int? Rating { get; set; }
}

public class SignOffInputModel
{
    public int InductionCaseId { get; set; }
    public SignOffRole Role { get; set; }

    [StringLength(1000)]
    public string? Comments { get; set; }
}

public class SendInductionInvitationsViewModel
{
    [Required, MinLength(1)]
    public int[] InductionCaseIds { get; set; } = [];

    public int? ReturnToInductionId { get; set; }

    public Guid? ReturnToInductionToken { get; set; }
}

public class UpdateEmployeeEmailViewModel
{
    [Required]
    public int InductionCaseId { get; set; }

    [Required, EmailAddress, StringLength(254), Display(Name = "Employee email address")]
    public string EmployeeEmail { get; set; } = string.Empty;
}
