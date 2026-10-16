using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.ViewModels;

public class InductionProgrammeEditorViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "Programme title")]
    public string Title { get; set; } = "Corporate Induction Programme";

    [Required, DataType(DataType.Date), Display(Name = "Programme date")]
    public DateTime ProgrammeDate { get; set; } = DateTime.Today;

    [StringLength(200)]
    public string? Venue { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public InductionProgrammeStatus Status { get; set; } = InductionProgrammeStatus.Draft;

    [Required, MinLength(1, ErrorMessage = "Add at least one divisional presentation.")]
    public List<InductionProgrammeSessionInputModel> Sessions { get; set; } = [new()];

    public IReadOnlyList<SelectListItem> DivisionOptions { get; set; } = [];
    public bool ScheduleLocked { get; set; }
}

public class InductionProgrammeSessionInputModel
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Division { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Presenter")]
    public string PresenterName { get; set; } = string.Empty;

    [Required, StringLength(150), RegularExpression(@"^[^\\]+\\[^\\]+$", ErrorMessage = "Use the AD ID format DOMAIN\\username."), Display(Name = "Presenter AD ID")]
    public string PresenterUsername { get; set; } = string.Empty;

    [Required, DataType(DataType.Time), Display(Name = "Start time")]
    public TimeSpan? StartTime { get; set; }

    [Required, DataType(DataType.Time), Display(Name = "End time")]
    public TimeSpan? EndTime { get; set; }
}

public class InductionProgrammeReportViewModel
{
    public required InductionProgramme Programme { get; set; }
    public int ParticipantCount { get; set; }
    public int ResponseCount { get; set; }
    public decimal ResponseRate { get; set; }
    public double AverageOverallRating { get; set; }
    public double AveragePresentationRating { get; set; }
    public IReadOnlyList<DivisionRatingSummary> DivisionRatings { get; set; } = [];
    public IReadOnlyList<RatingCountSummary> OverallRatingDistribution { get; set; } = [];
    public IReadOnlyList<NamedCountSummary> TimeAllocationDistribution { get; set; } = [];
}

public record DivisionRatingSummary(string Division, string Presenter, double Average, int Responses);
public record RatingCountSummary(int Rating, int Count);
public record NamedCountSummary(string Label, int Count);

public class PresenterProgrammeViewModel
{
    public required InductionProgramme Programme { get; set; }
    public required IReadOnlyList<PresenterAttendeeViewModel> Attendees { get; set; }
    public bool SmtpConfigured { get; set; }
    public string SmtpSummary { get; set; } = string.Empty;
}

public class PresenterAttendeeViewModel
{
    public required InductionCase Induction { get; set; }
    public ProgrammeAttendanceStatus Status { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    public string? ConfirmationSource { get; set; }
    public DateTime? InvitationSentAtUtc { get; set; }
}

public class SendAttendanceInvitationsViewModel
{
    [Required]
    public int ProgrammeId { get; set; }

    [Required, MinLength(1)]
    public int[] InductionCaseIds { get; set; } = [];
}

public class UpdateProgrammeAttendanceViewModel
{
    [Required]
    public int ProgrammeId { get; set; }

    [Required]
    public int InductionCaseId { get; set; }

    [Required]
    public ProgrammeAttendanceStatus Status { get; set; }
}
