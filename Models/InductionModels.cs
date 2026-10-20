using System.ComponentModel.DataAnnotations;

namespace Namfisa.Induction.Models;

public enum InductionStatus
{
    Planned,
    InProgress,
    AwaitingSignOff,
    Completed,
    OnHold
}

public enum InductionPhase
{
    PreStart,
    FirstDay,
    FirstTwoWeeks,
    Divisional
}

public enum InductionTaskStatus
{
    NotStarted,
    Completed,
    NotApplicable
}

public enum SignOffRole
{
    Employee,
    LineManager,
    HrBusinessPartner
}

public enum InductionProgrammeStatus
{
    Draft,
    Scheduled,
    Completed,
    Cancelled
}

public enum TimeAllocationResponse
{
    Appropriate = 1,
    TooShort = 2,
    TooLong = 3
}

public enum ProgrammeAttendanceStatus
{
    Pending,
    Present,
    Absent
}

public class InductionCase
{
    public int Id { get; set; }

    public Guid AccessToken { get; set; } = Guid.NewGuid();

    [Required, StringLength(30)]
    public string EmployeeTitle { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string EmployeeName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Gender { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Position { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string EmployeeUsername { get; set; } = string.Empty;

    [StringLength(254), EmailAddress]
    public string? EmployeeEmail { get; set; }

    [StringLength(40)]
    public string? EmployeeNumber { get; set; }

    [Required, StringLength(120)]
    public string Department { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Division { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime AppointmentDate { get; set; }

    [Required, StringLength(150)]
    public string LineManagerName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string LineManagerUsername { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string HrBusinessPartnerName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string HrBusinessPartnerUsername { get; set; } = string.Empty;

    public InductionStatus Status { get; set; } = InductionStatus.Planned;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(150)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? InvitationSentAtUtc { get; set; }

    [StringLength(150)]
    public string? InvitationSentBy { get; set; }

    [StringLength(254), EmailAddress]
    public string? InvitationSentTo { get; set; }

    public int? InductionProgrammeId { get; set; }
    public InductionProgramme? InductionProgramme { get; set; }
    public ProgrammeAttendance? ProgrammeAttendance { get; set; }

    public ICollection<InductionTask> Tasks { get; set; } = [];
    public ICollection<SignOff> SignOffs { get; set; } = [];
    public ICollection<TrainingEvaluation> TrainingEvaluations { get; set; } = [];
    public EmployeeFeedback? EmployeeFeedback { get; set; }
}

public class ChecklistTemplateItem
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Section { get; set; } = string.Empty;

    [Required, StringLength(400)]
    public string Title { get; set; } = string.Empty;

    public InductionPhase Phase { get; set; }

    [Required, StringLength(80)]
    public string ResponsibleRole { get; set; } = string.Empty;

    public int DueOffsetDays { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(150)]
    public string UpdatedBy { get; set; } = string.Empty;
}

public class InductionTask
{
    public int Id { get; set; }
    public int InductionCaseId { get; set; }
    public InductionCase InductionCase { get; set; } = null!;
    public int? ChecklistTemplateItemId { get; set; }

    [Required, StringLength(120)]
    public string Section { get; set; } = string.Empty;

    [Required, StringLength(400)]
    public string Title { get; set; } = string.Empty;

    public InductionPhase Phase { get; set; }

    [Required, StringLength(80)]
    public string ResponsibleRole { get; set; } = string.Empty;

    [StringLength(150)]
    public string? AssignedToUsername { get; set; }

    public DateTime DueDate { get; set; }
    public bool IsMandatory { get; set; }
    public bool ExcludedByProgramme { get; set; }
    public int SortOrder { get; set; }
    public InductionTaskStatus Status { get; set; } = InductionTaskStatus.NotStarted;

    [StringLength(150)]
    public string? CompletedBy { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class SignOff
{
    public int Id { get; set; }
    public int InductionCaseId { get; set; }
    public InductionCase InductionCase { get; set; } = null!;
    public SignOffRole Role { get; set; }

    [Required, StringLength(150)]
    public string SignedBy { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string ExpectedSignerUsername { get; set; } = string.Empty;

    [StringLength(184)]
    public string? SignedBySid { get; set; }

    [StringLength(100)]
    public string? AuthenticationType { get; set; }

    public DateTime SignedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(1000)]
    public string Acknowledgement { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Comments { get; set; }
}

public class EmployeeFeedback
{
    public int Id { get; set; }
    public int InductionCaseId { get; set; }
    public InductionCase InductionCase { get; set; } = null!;

    public bool UnderstandsResponsibilities { get; set; }
    public bool AwareOfDevelopmentOpportunities { get; set; }

    [Range(1, 5)]
    public int? ProgrammeUnderstandingRating { get; set; }

    public TimeAllocationResponse? TimeAllocationResponse { get; set; }

    [Range(1, 5)]
    public int? OverallProgrammeRating { get; set; }

    [StringLength(2000)]
    public string? MostValuableAspect { get; set; }

    [StringLength(2000)]
    public string? ImprovementRecommendations { get; set; }

    [StringLength(2000)]
    public string? MissingFromInduction { get; set; }

    [StringLength(2000)]
    public string? AdditionalComments { get; set; }

    [Required, StringLength(150)]
    public string SubmittedBy { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PresentationRating> PresentationRatings { get; set; } = [];
}

public class InductionProgramme
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime ProgrammeDate { get; set; }

    [StringLength(200)]
    public string? Venue { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public InductionProgrammeStatus Status { get; set; } = InductionProgrammeStatus.Draft;

    [Required, StringLength(150)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(150)]
    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<InductionProgrammeSession> Sessions { get; set; } = [];
    public ICollection<InductionProgrammeChecklistPhase> ChecklistPhases { get; set; } = [];
    public ICollection<InductionCase> Participants { get; set; } = [];
    public ICollection<ProgrammeAttendance> Attendances { get; set; } = [];
}

public class InductionProgrammeChecklistPhase
{
    public int InductionProgrammeId { get; set; }
    public InductionProgramme InductionProgramme { get; set; } = null!;
    public InductionPhase Phase { get; set; }
}

public class InductionProgrammeSession
{
    public int Id { get; set; }
    public int InductionProgrammeId { get; set; }
    public InductionProgramme InductionProgramme { get; set; } = null!;

    [Required, StringLength(120)]
    public string Division { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string PresenterName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string PresenterUsername { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SortOrder { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    [StringLength(150)]
    public string? CompletedBy { get; set; }

    public ICollection<PresentationRating> Ratings { get; set; } = [];
}

public class ProgrammeAttendance
{
    public int Id { get; set; }
    public int InductionProgrammeId { get; set; }
    public InductionProgramme InductionProgramme { get; set; } = null!;
    public int InductionCaseId { get; set; }
    public InductionCase InductionCase { get; set; } = null!;
    public ProgrammeAttendanceStatus Status { get; set; } = ProgrammeAttendanceStatus.Pending;

    [StringLength(150)]
    public string? ConfirmedBy { get; set; }

    [StringLength(184)]
    public string? ConfirmedBySid { get; set; }

    [StringLength(100)]
    public string? AuthenticationType { get; set; }

    [StringLength(50)]
    public string? ConfirmationSource { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? InvitationSentAtUtc { get; set; }

    [StringLength(150)]
    public string? InvitationSentBy { get; set; }

    [StringLength(254), EmailAddress]
    public string? InvitationSentTo { get; set; }

    public DateTime? FeedbackInvitationSentAtUtc { get; set; }

    [StringLength(150)]
    public string? FeedbackInvitationSentBy { get; set; }

    [StringLength(254), EmailAddress]
    public string? FeedbackInvitationSentTo { get; set; }
}

public class PresentationRating
{
    public int Id { get; set; }
    public int EmployeeFeedbackId { get; set; }
    public EmployeeFeedback EmployeeFeedback { get; set; } = null!;
    public int InductionProgrammeSessionId { get; set; }
    public InductionProgrammeSession InductionProgrammeSession { get; set; } = null!;

    [Range(1, 5)]
    public int Rating { get; set; }
}

public class TrainingEvaluation
{
    public int Id { get; set; }
    public int InductionCaseId { get; set; }
    public InductionCase InductionCase { get; set; } = null!;

    [Required, StringLength(200)]
    public string CourseTitle { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string CourseProvider { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime CourseDate { get; set; }

    [Range(1, 4)] public int ObjectivesClearlyDefined { get; set; }
    [Range(1, 4)] public int ParticipationEncouraged { get; set; }
    [Range(1, 4)] public int ContentOrganized { get; set; }
    [Range(1, 4)] public int MaterialsHelpful { get; set; }
    [Range(1, 4)] public int UsefulInWork { get; set; }
    [Range(1, 4)] public int ObjectivesMet { get; set; }
    [Range(1, 4)] public int TimeSufficient { get; set; }
    [Range(1, 4)] public int FacilitiesAdequate { get; set; }

    [StringLength(2000)] public string? DisagreementImprovements { get; set; }
    [StringLength(2000)] public string? LikedMost { get; set; }
    [StringLength(2000)] public string? CouldImprove { get; set; }
    [Range(1, 4)] public int OverallImpression { get; set; }
    [StringLength(2000)] public string? AdditionalComments { get; set; }
    [Range(1, 5)] public int TrainerKnowledge { get; set; }
    [Range(1, 5)] public int TrainerExplanation { get; set; }
    [Range(1, 5)] public int TrainerAnswers { get; set; }
    [StringLength(1000)] public string? VenueComments { get; set; }
    [StringLength(1000)] public string? MealsComments { get; set; }
    public bool OfficialResultsAttached { get; set; }
    public bool CertificateAttached { get; set; }
    public bool AttendanceRegisterAttached { get; set; }

    [Required, StringLength(150)]
    public string SubmittedBy { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}
