using System.ComponentModel.DataAnnotations;

namespace Namfisa.Induction.ViewModels;

public class TrainingEvaluationInputModel
{
    public int InductionCaseId { get; set; }

    [Required, StringLength(200), Display(Name = "Course title")]
    public string CourseTitle { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Course provider or trainer")]
    public string CourseProvider { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Course date")]
    public DateTime CourseDate { get; set; } = DateTime.Today;

    [Range(1, 4)] public int ObjectivesClearlyDefined { get; set; }
    [Range(1, 4)] public int ParticipationEncouraged { get; set; }
    [Range(1, 4)] public int ContentOrganized { get; set; }
    [Range(1, 4)] public int MaterialsHelpful { get; set; }
    [Range(1, 4)] public int UsefulInWork { get; set; }
    [Range(1, 4)] public int ObjectivesMet { get; set; }
    [Range(1, 4)] public int TimeSufficient { get; set; }
    [Range(1, 4)] public int FacilitiesAdequate { get; set; }

    [StringLength(2000), Display(Name = "Improvements for areas where you disagreed")]
    public string? DisagreementImprovements { get; set; }

    [StringLength(2000), Display(Name = "What did you like most?")]
    public string? LikedMost { get; set; }

    [StringLength(2000), Display(Name = "What could be improved?")]
    public string? CouldImprove { get; set; }

    [Range(1, 4)] public int OverallImpression { get; set; }

    [StringLength(2000), Display(Name = "Additional comments")]
    public string? AdditionalComments { get; set; }

    [Range(1, 5)] public int TrainerKnowledge { get; set; }
    [Range(1, 5)] public int TrainerExplanation { get; set; }
    [Range(1, 5)] public int TrainerAnswers { get; set; }

    [StringLength(1000), Display(Name = "Venue comments")]
    public string? VenueComments { get; set; }

    [StringLength(1000), Display(Name = "Meals and refreshments comments")]
    public string? MealsComments { get; set; }

    [Display(Name = "Official results or marks attached")]
    public bool OfficialResultsAttached { get; set; }

    [Display(Name = "Certificate attached")]
    public bool CertificateAttached { get; set; }

    [Display(Name = "Attendance register attached")]
    public bool AttendanceRegisterAttached { get; set; }
}
