using Namfisa.Induction.Helpers;
using Namfisa.Induction.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Namfisa.Induction.Services;

public interface IInductionReportService
{
    byte[] Generate(InductionCase inductionCase);
    byte[] GenerateProgramme(InductionProgramme programme);
}

public sealed class InductionReportService(IWebHostEnvironment environment) : IInductionReportService
{
    private static readonly XColor NamfisaGreen = XColor.FromArgb(0, 91, 72);
    private static readonly XColor NamfisaGold = XColor.FromArgb(214, 168, 55);
    private static readonly XColor SoftGreen = XColor.FromArgb(235, 244, 241);
    private static readonly XColor SoftGray = XColor.FromArgb(244, 246, 247);
    private static readonly XColor TextColor = XColor.FromArgb(36, 48, 45);
    private static readonly XColor MutedColor = XColor.FromArgb(96, 108, 104);

    public byte[] Generate(InductionCase inductionCase)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Induction report - {inductionCase.EmployeeName}";
        document.Info.Author = "NAMFISA HR";
        document.Info.Subject = "Employee induction record";

        var logoPath = Path.Combine(environment.WebRootPath, "images", "namfisa-logo.png");
        using var logo = File.Exists(logoPath) ? XImage.FromFile(logoPath) : null;
        using var report = new ReportWriter(document, logo);

        report.AddDocumentTitle("EMPLOYEE INDUCTION REPORT", inductionCase.EmployeeName);
        report.AddSection("EMPLOYEE AND INDUCTION DETAILS");
        report.AddDetailsGrid([
            ("Employee", inductionCase.EmployeeName),
            ("Employee number", inductionCase.EmployeeNumber ?? "Not recorded"),
            ("Gender", inductionCase.Gender),
            ("Position", inductionCase.Position),
            ("Employee AD ID", inductionCase.EmployeeUsername),
            ("Employee email", inductionCase.EmployeeEmail ?? "Not recorded"),
            ("Department", inductionCase.Department),
            ("Division", inductionCase.Division),
            ("Appointment date", inductionCase.AppointmentDate.ToString("dd MMMM yyyy")),
            ("Current status", DisplayText.For(inductionCase.Status)),
            ("Line manager", inductionCase.LineManagerName),
            ("HR business partner", inductionCase.HrBusinessPartnerName),
            ("Induction programme", inductionCase.InductionProgramme?.Title ?? "Not assigned"),
            ("Programme date", inductionCase.InductionProgramme?.ProgrammeDate.ToString("dd MMMM yyyy") ?? "Not assigned"),
            ("Invitation", inductionCase.InvitationSentAtUtc.HasValue
                ? $"Sent to {inductionCase.InvitationSentTo ?? inductionCase.EmployeeEmail} on {inductionCase.InvitationSentAtUtc.Value.ToLocalTime():dd MMM yyyy HH:mm} by {inductionCase.InvitationSentBy}"
                : "Not sent")
        ]);

        var applicableTasks = inductionCase.Tasks.Where(x => !x.ExcludedByProgramme).ToList();
        var completed = applicableTasks.Count(x =>
            x.Status is InductionTaskStatus.Completed or InductionTaskStatus.NotApplicable);
        var percentage = applicableTasks.Count == 0
            ? 0
            : (int)Math.Round(completed * 100d / applicableTasks.Count);
        report.AddProgress(completed, applicableTasks.Count, percentage);

        if (inductionCase.InductionProgramme is not null)
        {
            report.AddSection("CORPORATE INDUCTION PROGRAMME SCHEDULE");
            foreach (var session in inductionCase.InductionProgramme.Sessions.OrderBy(x => x.SortOrder))
            {
                report.AddLabelValue(
                    $"{session.StartTime:hh\\:mm}-{session.EndTime:hh\\:mm}  {session.Division}",
                    session.PresenterName);
            }
        }

        report.AddSection("INDUCTION CHECKLIST");
        foreach (var phaseGroup in applicableTasks.OrderBy(x => x.SortOrder).GroupBy(x => x.Phase))
        {
            report.AddSubheading(DisplayText.For(phaseGroup.Key));
            foreach (var task in phaseGroup)
            {
                report.AddChecklistItem(task);
            }
        }

        report.AddSection("EMPLOYEE FEEDBACK");
        if (inductionCase.EmployeeFeedback is null)
        {
            report.AddNotice("No employee feedback has been submitted.");
        }
        else
        {
            var feedback = inductionCase.EmployeeFeedback;
            if (feedback.OverallProgrammeRating.HasValue)
            {
                report.AddLabelValue("Programme understanding", RatingText(feedback.ProgrammeUnderstandingRating));
                report.AddLabelValue("Presentation time allocation", TimeAllocationText(feedback.TimeAllocationResponse));
                report.AddLabelValue("Overall programme rating", RatingText(feedback.OverallProgrammeRating));
                foreach (var rating in feedback.PresentationRatings.OrderBy(x => x.InductionProgrammeSession.SortOrder))
                {
                    report.AddLabelValue(
                        rating.InductionProgrammeSession.Division,
                        $"{rating.Rating}/5 - {rating.InductionProgrammeSession.PresenterName}");
                }
                report.AddLabelValue("Most valuable aspect", feedback.MostValuableAspect ?? "Not provided");
                report.AddLabelValue("Recommended improvements", feedback.ImprovementRecommendations ?? "Not provided");
            }
            else
            {
                report.AddLabelValue("Understands responsibilities", feedback.UnderstandsResponsibilities ? "Yes" : "No");
                report.AddLabelValue("Aware of development opportunities", feedback.AwareOfDevelopmentOpportunities ? "Yes" : "No");
                report.AddLabelValue("What was missing", feedback.MissingFromInduction ?? "Nothing reported");
                report.AddLabelValue("Additional comments", feedback.AdditionalComments ?? "None");
            }
            report.AddAudit($"Submitted by {feedback.SubmittedBy} on {feedback.SubmittedAtUtc:dd MMM yyyy HH:mm} UTC");
        }

        report.AddSection("TRAINING EVALUATIONS");
        if (inductionCase.TrainingEvaluations.Count == 0)
        {
            report.AddNotice("No training evaluations have been submitted.");
        }
        else
        {
            foreach (var evaluation in inductionCase.TrainingEvaluations.OrderBy(x => x.CourseDate))
            {
                report.AddTrainingEvaluation(evaluation);
            }
        }

        report.AddSection("SEQUENTIAL SIGN-OFF");
        foreach (var role in Enum.GetValues<SignOffRole>())
        {
            var signOff = inductionCase.SignOffs.SingleOrDefault(x => x.Role == role);
            var expectedSigner = role switch
            {
                SignOffRole.Employee => inductionCase.EmployeeUsername,
                SignOffRole.LineManager => inductionCase.LineManagerUsername,
                SignOffRole.HrBusinessPartner => inductionCase.HrBusinessPartnerUsername,
                _ => string.Empty
            };
            report.AddSignOff(role, expectedSigner, signOff);
        }

        report.AddAudit($"Report generated on {DateTime.Now:dd MMM yyyy HH:mm} by the NAMFISA Induction application.");
        report.Finish();

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    public byte[] GenerateProgramme(InductionProgramme programme)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Corporate induction programme report - {programme.ProgrammeDate:dd MMMM yyyy}";
        document.Info.Author = "NAMFISA HR";
        document.Info.Subject = "Corporate induction programme evaluation report";

        var logoPath = Path.Combine(environment.WebRootPath, "images", "namfisa-logo.png");
        using var logo = File.Exists(logoPath) ? XImage.FromFile(logoPath) : null;
        using var report = new ReportWriter(document, logo);

        var responses = programme.Participants
            .Select(x => x.EmployeeFeedback)
            .Where(x => x?.OverallProgrammeRating is not null)
            .Select(x => x!)
            .ToList();
        var presentationRatings = programme.Sessions.SelectMany(x => x.Ratings).ToList();
        var responseRate = programme.Participants.Count == 0
            ? 0
            : (int)Math.Round(responses.Count * 100d / programme.Participants.Count);
        var checklistPhases = programme.ChecklistPhases.Count == 0
            ? Enum.GetValues<InductionPhase>()
            : programme.ChecklistPhases.Select(x => x.Phase).OrderBy(x => x).ToArray();

        report.AddDocumentTitle("CORPORATE INDUCTION PROGRAMME REPORT", programme.Title);
        report.AddSection("PROGRAMME DETAILS");
        report.AddDetailsGrid([
            ("Programme date", programme.ProgrammeDate.ToString("dd MMMM yyyy")),
            ("Venue", programme.Venue ?? "Not specified"),
            ("Status", programme.Status.ToString()),
            ("Divisional presentations", programme.Sessions.Count.ToString()),
            ("Checklist phases", string.Join(", ", checklistPhases.Select(DisplayText.For))),
            ("Employees assigned", programme.Participants.Count.ToString()),
            ("Feedback responses", $"{responses.Count} ({responseRate}%)")
        ]);

        report.AddSection("PROGRAMME SCHEDULE AND PRESENTATION RATINGS");
        foreach (var session in programme.Sessions.OrderBy(x => x.SortOrder))
        {
            var average = session.Ratings.Count == 0 ? "No ratings" : $"Average {session.Ratings.Average(x => x.Rating):0.00}/5 from {session.Ratings.Count} response(s)";
            report.AddLabelValue(
                $"{session.StartTime:hh\\:mm}-{session.EndTime:hh\\:mm}  {session.Division}",
                $"Presenter: {session.PresenterName}. {average}.");
        }

        report.AddSection("EVALUATION SUMMARY");
        report.AddLabelValue("Average overall programme rating", responses.Count == 0 ? "No responses" : $"{responses.Average(x => x.OverallProgrammeRating!.Value):0.00}/5");
        report.AddLabelValue("Average divisional presentation rating", presentationRatings.Count == 0 ? "No responses" : $"{presentationRatings.Average(x => x.Rating):0.00}/5");
        report.AddLabelValue("Programme understanding", responses.Count == 0 ? "No responses" : $"{responses.Average(x => x.ProgrammeUnderstandingRating ?? 0):0.00}/5");
        foreach (var allocation in Enum.GetValues<TimeAllocationResponse>())
        {
            report.AddLabelValue(TimeAllocationText(allocation), responses.Count(x => x.TimeAllocationResponse == allocation).ToString());
        }

        report.AddSection("EMPLOYEE COMMENTS");
        if (responses.Count == 0)
        {
            report.AddNotice("No employee feedback has been submitted for this programme.");
        }
        else
        {
            foreach (var feedback in responses.OrderBy(x => x.SubmittedAtUtc))
            {
                report.AddSubheading($"Response submitted {feedback.SubmittedAtUtc:dd MMM yyyy HH:mm} UTC");
                report.AddLabelValue("Most valuable aspect", feedback.MostValuableAspect ?? "Not provided");
                report.AddLabelValue("Recommended improvements", feedback.ImprovementRecommendations ?? "Not provided");
            }
        }

        report.AddAudit($"Report generated on {DateTime.Now:dd MMM yyyy HH:mm} by the NAMFISA Induction application.");
        report.Finish();
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static string RatingText(int? rating) => rating switch
    {
        1 => "Poor (1/5)",
        2 => "Fair (2/5)",
        3 => "Good (3/5)",
        4 => "Very good (4/5)",
        5 => "Excellent (5/5)",
        _ => "Not recorded"
    };

    private static string TimeAllocationText(TimeAllocationResponse? response) => response switch
    {
        TimeAllocationResponse.Appropriate => "Appropriate",
        TimeAllocationResponse.TooShort => "Too short",
        TimeAllocationResponse.TooLong => "Too long",
        _ => "Not recorded"
    };

    private sealed class ReportWriter : IDisposable
    {
        private const double Margin = 42;
        private const double HeaderHeight = 62;
        private const double FooterHeight = 28;
        private readonly PdfDocument _document;
        private readonly XImage? _logo;
        private readonly XFont _titleFont = new("Arial", 18, XFontStyleEx.Bold);
        private readonly XFont _subtitleFont = new("Arial", 10, XFontStyleEx.Regular);
        private readonly XFont _sectionFont = new("Arial", 10, XFontStyleEx.Bold);
        private readonly XFont _subheadingFont = new("Arial", 10, XFontStyleEx.Bold);
        private readonly XFont _bodyFont = new("Arial", 8.5, XFontStyleEx.Regular);
        private readonly XFont _bodyBoldFont = new("Arial", 8.5, XFontStyleEx.Bold);
        private readonly XFont _smallFont = new("Arial", 7.3, XFontStyleEx.Regular);
        private PdfPage _page = null!;
        private XGraphics _graphics = null!;
        private double _y;

        public ReportWriter(PdfDocument document, XImage? logo)
        {
            _document = document;
            _logo = logo;
            AddPage();
        }

        private double ContentWidth => _page.Width.Point - (Margin * 2);
        private double ContentBottom => _page.Height.Point - FooterHeight - Margin;

        public void AddDocumentTitle(string title, string employeeName)
        {
            EnsureSpace(72);
            _graphics.DrawString(title, _titleFont, new XSolidBrush(NamfisaGreen),
                new XRect(Margin, _y, ContentWidth, 24), XStringFormats.TopLeft);
            _y += 28;
            _graphics.DrawString(employeeName, _subtitleFont, new XSolidBrush(MutedColor),
                new XRect(Margin, _y, ContentWidth, 16), XStringFormats.TopLeft);
            _y += 28;
        }

        public void AddSection(string title)
        {
            EnsureSpace(36);
            _y += 8;
            _graphics.DrawRectangle(new XSolidBrush(NamfisaGreen), Margin, _y, ContentWidth, 24);
            _graphics.DrawString(title, _sectionFont, XBrushes.White,
                new XRect(Margin + 9, _y + 6, ContentWidth - 18, 14), XStringFormats.TopLeft);
            _y += 32;
        }

        public void AddSubheading(string title)
        {
            EnsureSpace(28);
            _graphics.DrawRectangle(new XSolidBrush(SoftGreen), Margin, _y, ContentWidth, 22);
            _graphics.DrawString(title, _subheadingFont, new XSolidBrush(NamfisaGreen),
                new XRect(Margin + 8, _y + 5, ContentWidth - 16, 14), XStringFormats.TopLeft);
            _y += 27;
        }

        public void AddDetailsGrid(IReadOnlyList<(string Label, string Value)> details)
        {
            var columnWidth = (ContentWidth - 12) / 2;
            for (var index = 0; index < details.Count; index += 2)
            {
                var leftLines = Wrap(details[index].Value, _bodyBoldFont, columnWidth).ToList();
                var rightLines = index + 1 < details.Count
                    ? Wrap(details[index + 1].Value, _bodyBoldFont, columnWidth).ToList()
                    : [];
                var rowHeight = 25 + (Math.Max(leftLines.Count, rightLines.Count) * 11);
                EnsureSpace(rowHeight);
                DrawDetailCell(Margin, _y, columnWidth, details[index].Label, leftLines, rowHeight);
                if (index + 1 < details.Count)
                {
                    DrawDetailCell(Margin + columnWidth + 12, _y, columnWidth,
                        details[index + 1].Label, rightLines, rowHeight);
                }
                _y += rowHeight;
            }
            _y += 3;
        }

        public void AddProgress(int completed, int total, int percentage)
        {
            EnsureSpace(46);
            _graphics.DrawString("Overall progress", _bodyBoldFont, new XSolidBrush(TextColor),
                new XRect(Margin, _y, ContentWidth - 90, 14), XStringFormats.TopLeft);
            _graphics.DrawString($"{completed} of {total} tasks - {percentage}%", _bodyBoldFont, new XSolidBrush(TextColor),
                new XRect(Margin + ContentWidth - 145, _y, 145, 14), XStringFormats.TopRight);
            _y += 18;
            _graphics.DrawRoundedRectangle(new XSolidBrush(SoftGray), Margin, _y, ContentWidth, 9, 4, 4);
            if (percentage > 0)
            {
                _graphics.DrawRoundedRectangle(new XSolidBrush(NamfisaGold), Margin, _y,
                    ContentWidth * Math.Min(percentage, 100) / 100d, 9, 4, 4);
            }
            _y += 20;
        }

        public void AddChecklistItem(InductionTask task)
        {
            var noteText = string.IsNullOrWhiteSpace(task.Notes) ? null : $"Notes: {task.Notes}";
            var titleLines = Wrap(task.Title, _bodyBoldFont, ContentWidth - 18).ToList();
            var noteLines = noteText is null ? [] : Wrap(noteText, _smallFont, ContentWidth - 18).ToList();
            var height = 35 + (titleLines.Count * 11) + (noteLines.Count * 9);
            EnsureSpace(height + 6);

            _graphics.DrawRectangle(new XPen(XColor.FromArgb(215, 222, 220), 0.7),
                new XSolidBrush(XColors.White), Margin, _y, ContentWidth, height);
            var textY = _y + 7;
            foreach (var line in titleLines)
            {
                _graphics.DrawString(line, _bodyBoldFont, new XSolidBrush(TextColor),
                    new XRect(Margin + 9, textY, ContentWidth - 18, 11), XStringFormats.TopLeft);
                textY += 11;
            }

            var detail = $"{task.Section} | {task.ResponsibleRole} | Due {task.DueDate:dd MMM yyyy} | {DisplayText.For(task.Status)}";
            _graphics.DrawString(detail, _smallFont, new XSolidBrush(MutedColor),
                new XRect(Margin + 9, textY + 2, ContentWidth - 18, 10), XStringFormats.TopLeft);
            textY += 14;
            if (task.IsMandatory)
            {
                _graphics.DrawString("Required", _smallFont, new XSolidBrush(NamfisaGreen),
                    new XRect(Margin + 9, textY, 55, 10), XStringFormats.TopLeft);
            }
            if (!string.IsNullOrWhiteSpace(task.CompletedBy))
            {
                _graphics.DrawString($"Completed by {task.CompletedBy} {task.CompletedAtUtc:dd MMM yyyy HH:mm} UTC", _smallFont,
                    new XSolidBrush(MutedColor), new XRect(Margin + 70, textY, ContentWidth - 79, 10), XStringFormats.TopLeft);
            }
            textY += 11;
            foreach (var line in noteLines)
            {
                _graphics.DrawString(line, _smallFont, new XSolidBrush(TextColor),
                    new XRect(Margin + 9, textY, ContentWidth - 18, 9), XStringFormats.TopLeft);
                textY += 9;
            }
            _y += height + 5;
        }

        public void AddLabelValue(string label, string value)
        {
            var safeValue = string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
            var lines = Wrap(safeValue, _bodyFont, ContentWidth - 165).ToList();
            var height = Math.Max(19, (lines.Count * 11) + 8);
            EnsureSpace(height);
            _graphics.DrawString(label, _bodyBoldFont, new XSolidBrush(TextColor),
                new XRect(Margin, _y + 3, 150, 12), XStringFormats.TopLeft);
            var lineY = _y + 3;
            foreach (var line in lines)
            {
                _graphics.DrawString(line, _bodyFont, new XSolidBrush(TextColor),
                    new XRect(Margin + 160, lineY, ContentWidth - 160, 11), XStringFormats.TopLeft);
                lineY += 11;
            }
            _y += height;
        }

        public void AddTrainingEvaluation(TrainingEvaluation evaluation)
        {
            var titleLines = Wrap(evaluation.CourseTitle, _bodyBoldFont, ContentWidth - 18).ToList();
            var titleExtra = Math.Max(0, titleLines.Count - 1) * 11;
            var height = 86 + titleExtra;
            EnsureSpace(height + 6);
            _graphics.DrawRectangle(new XPen(XColor.FromArgb(215, 222, 220), 0.7),
                new XSolidBrush(SoftGray), Margin, _y, ContentWidth, height);
            var titleY = _y + 8;
            foreach (var line in titleLines)
            {
                _graphics.DrawString(line, _bodyBoldFont, new XSolidBrush(NamfisaGreen),
                    new XRect(Margin + 9, titleY, ContentWidth - 18, 11), XStringFormats.TopLeft);
                titleY += 11;
            }
            _graphics.DrawString($"{evaluation.CourseProvider} | {evaluation.CourseDate:dd MMM yyyy}", _smallFont,
                new XSolidBrush(MutedColor), new XRect(Margin + 9, _y + 23 + titleExtra, ContentWidth - 18, 10), XStringFormats.TopLeft);

            var contentAverage = new[]
            {
                evaluation.ObjectivesClearlyDefined, evaluation.ParticipationEncouraged,
                evaluation.ContentOrganized, evaluation.MaterialsHelpful, evaluation.UsefulInWork,
                evaluation.ObjectivesMet, evaluation.TimeSufficient, evaluation.FacilitiesAdequate
            }.Average();
            var trainerAverage = new[]
            {
                evaluation.TrainerKnowledge, evaluation.TrainerExplanation, evaluation.TrainerAnswers
            }.Average();
            _graphics.DrawString($"Overall: {evaluation.OverallImpression}/4   Content average: {contentAverage:0.0}/4   Trainer average: {trainerAverage:0.0}/5",
                _bodyFont, new XSolidBrush(TextColor), new XRect(Margin + 9, _y + 39 + titleExtra, ContentWidth - 18, 12), XStringFormats.TopLeft);

            var attachments = new List<string>();
            if (evaluation.OfficialResultsAttached) attachments.Add("official results");
            if (evaluation.CertificateAttached) attachments.Add("certificate");
            if (evaluation.AttendanceRegisterAttached) attachments.Add("attendance register");
            _graphics.DrawString($"Attachments: {(attachments.Count == 0 ? "None recorded" : string.Join(", ", attachments))}",
                _smallFont, new XSolidBrush(MutedColor), new XRect(Margin + 9, _y + 55 + titleExtra, ContentWidth - 18, 10), XStringFormats.TopLeft);
            _graphics.DrawString($"Submitted by {evaluation.SubmittedBy} on {evaluation.SubmittedAtUtc:dd MMM yyyy HH:mm} UTC",
                _smallFont, new XSolidBrush(MutedColor), new XRect(Margin + 9, _y + 68 + titleExtra, ContentWidth - 18, 10), XStringFormats.TopLeft);
            _y += height + 5;
        }

        public void AddSignOff(SignOffRole role, string expectedSigner, SignOff? signOff)
        {
            var acknowledgementLines = signOff is null
                ? []
                : Wrap(signOff.Acknowledgement, _smallFont, ContentWidth - 18).ToList();
            var comments = signOff?.Comments;
            var commentLines = string.IsNullOrWhiteSpace(comments)
                ? []
                : Wrap($"Comments: {comments}", _smallFont, ContentWidth - 18).ToList();
            var height = signOff is null
                ? 52
                : 65 + (acknowledgementLines.Count * 9) + (commentLines.Count * 9);
            EnsureSpace(height + 5);
            _graphics.DrawRectangle(new XPen(XColor.FromArgb(215, 222, 220), 0.7),
                new XSolidBrush(signOff is null ? SoftGray : SoftGreen), Margin, _y, ContentWidth, height);
            _graphics.DrawString(DisplayText.For(role), _bodyBoldFont, new XSolidBrush(NamfisaGreen),
                new XRect(Margin + 9, _y + 7, 170, 12), XStringFormats.TopLeft);
            _graphics.DrawString(signOff is null ? "Pending" : "Signed", _bodyBoldFont,
                new XSolidBrush(signOff is null ? MutedColor : NamfisaGreen),
                new XRect(Margin + ContentWidth - 90, _y + 7, 80, 12), XStringFormats.TopRight);
            _graphics.DrawString($"Assigned AD ID: {expectedSigner}", _smallFont, new XSolidBrush(MutedColor),
                new XRect(Margin + 9, _y + 23, ContentWidth - 18, 10), XStringFormats.TopLeft);
            if (signOff is not null)
            {
                _graphics.DrawString($"{signOff.SignedBy} | {signOff.SignedAtUtc:dd MMM yyyy HH:mm} UTC", _smallFont,
                    new XSolidBrush(TextColor), new XRect(Margin + 9, _y + 37, ContentWidth - 18, 10), XStringFormats.TopLeft);
                var auditIdentity = $"SID: {signOff.SignedBySid ?? "Not available"} | Authentication: {signOff.AuthenticationType ?? "Not recorded"}";
                _graphics.DrawString(auditIdentity, _smallFont, new XSolidBrush(MutedColor),
                    new XRect(Margin + 9, _y + 50, ContentWidth - 18, 10), XStringFormats.TopLeft);
                var lineY = _y + 63;
                foreach (var line in acknowledgementLines)
                {
                    _graphics.DrawString(line, _smallFont, new XSolidBrush(MutedColor),
                        new XRect(Margin + 9, lineY, ContentWidth - 18, 9), XStringFormats.TopLeft);
                    lineY += 9;
                }
                foreach (var line in commentLines)
                {
                    _graphics.DrawString(line, _smallFont, new XSolidBrush(TextColor),
                        new XRect(Margin + 9, lineY, ContentWidth - 18, 9), XStringFormats.TopLeft);
                    lineY += 9;
                }
            }
            _y += height + 5;
        }

        public void AddNotice(string text)
        {
            EnsureSpace(32);
            _graphics.DrawRectangle(new XSolidBrush(SoftGray), Margin, _y, ContentWidth, 27);
            _graphics.DrawString(text, _bodyFont, new XSolidBrush(MutedColor),
                new XRect(Margin + 9, _y + 7, ContentWidth - 18, 12), XStringFormats.TopLeft);
            _y += 34;
        }

        public void AddAudit(string text)
        {
            var lines = Wrap(text, _smallFont, ContentWidth).ToList();
            EnsureSpace((lines.Count * 9) + 14);
            _y += 5;
            foreach (var line in lines)
            {
                _graphics.DrawString(line, _smallFont, new XSolidBrush(MutedColor),
                    new XRect(Margin, _y, ContentWidth, 9), XStringFormats.TopLeft);
                _y += 9;
            }
            _y += 5;
        }

        public void Finish()
        {
            _graphics.Dispose();
            for (var index = 0; index < _document.Pages.Count; index++)
            {
                var page = _document.Pages[index];
                using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var y = page.Height.Point - Margin + 5;
                graphics.DrawLine(new XPen(NamfisaGold, 1), Margin, y - 8, page.Width.Point - Margin, y - 8);
                graphics.DrawString("NAMFISA Employee Induction", _smallFont, new XSolidBrush(MutedColor),
                    new XRect(Margin, y, 220, 10), XStringFormats.TopLeft);
                graphics.DrawString($"Page {index + 1} of {_document.Pages.Count}", _smallFont, new XSolidBrush(MutedColor),
                    new XRect(page.Width.Point - Margin - 100, y, 100, 10), XStringFormats.TopRight);
            }
            _graphics = null!;
        }

        private void AddPage()
        {
            _graphics?.Dispose();
            _page = _document.AddPage();
            _page.Size = PdfSharp.PageSize.A4;
            _graphics = XGraphics.FromPdfPage(_page);
            DrawHeader();
            _y = Margin + HeaderHeight;
        }

        private void DrawHeader()
        {
            if (_logo is not null)
            {
                var ratio = _logo.PixelWidth == 0 ? 1 : (double)_logo.PixelHeight / _logo.PixelWidth;
                var width = 112d;
                var height = Math.Min(42, width * ratio);
                _graphics.DrawImage(_logo, Margin, Margin - 3, width, height);
            }
            else
            {
                _graphics.DrawString("NAMFISA", _titleFont, new XSolidBrush(NamfisaGreen),
                    new XRect(Margin, Margin, 150, 24), XStringFormats.TopLeft);
            }

            _graphics.DrawString("Human Resources", _bodyBoldFont, new XSolidBrush(NamfisaGreen),
                new XRect(_page.Width.Point - Margin - 180, Margin, 180, 12), XStringFormats.TopRight);
            _graphics.DrawString("Employee induction record", _smallFont, new XSolidBrush(MutedColor),
                new XRect(_page.Width.Point - Margin - 180, Margin + 16, 180, 10), XStringFormats.TopRight);
            _graphics.DrawLine(new XPen(NamfisaGold, 2), Margin, Margin + 47, _page.Width.Point - Margin, Margin + 47);
        }

        private void DrawDetailCell(
            double x,
            double y,
            double width,
            string label,
            IReadOnlyList<string> valueLines,
            double height)
        {
            _graphics.DrawString(label, _smallFont, new XSolidBrush(MutedColor),
                new XRect(x, y, width, 10), XStringFormats.TopLeft);
            var lineY = y + 13;
            foreach (var line in valueLines)
            {
                _graphics.DrawString(line, _bodyBoldFont, new XSolidBrush(TextColor),
                    new XRect(x, lineY, width, 11), XStringFormats.TopLeft);
                lineY += 11;
            }
            _graphics.DrawLine(new XPen(XColor.FromArgb(224, 229, 227), 0.6), x, y + height - 5, x + width, y + height - 5);
        }

        private void EnsureSpace(double requiredHeight)
        {
            if (_y + requiredHeight > ContentBottom)
            {
                AddPage();
            }
        }

        private IEnumerable<string> Wrap(string? text, XFont font, double maxWidth)
        {
            var normalized = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length == 0)
            {
                yield return "Not recorded";
                yield break;
            }

            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = string.Empty;
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (_graphics.MeasureString(candidate, font).Width <= maxWidth || line.Length == 0)
                {
                    line = candidate;
                }
                else
                {
                    yield return line;
                    line = word;
                }
            }
            if (line.Length > 0) yield return line;
        }

        public void Dispose()
        {
            _graphics?.Dispose();
        }
    }
}
