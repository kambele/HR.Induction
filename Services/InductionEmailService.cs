using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;
using Namfisa.Induction.Models;

namespace Namfisa.Induction.Services;

public sealed class InductionEmailOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "NAMFISA Human Resources";
    public string? PublicBaseUrl { get; set; }
    public int TimeoutMilliseconds { get; set; } = 30_000;
}

public sealed record SmtpConfigurationStatus(bool IsConfigured, string Summary);

public interface IInductionEmailService
{
    SmtpConfigurationStatus ConfigurationStatus { get; }
    string? PublicBaseUrl { get; }

    Task SendInvitationAsync(
        InductionCase inductionCase,
        string inductionUrl,
        string subjectTemplate,
        string bodyTemplate,
        CancellationToken cancellationToken = default);

    Task SendAttendanceInvitationAsync(
        InductionCase inductionCase,
        InductionProgramme programme,
        string attendanceUrl,
        CancellationToken cancellationToken = default);
}

public sealed class InductionEmailService(IOptionsMonitor<InductionEmailOptions> options) : IInductionEmailService
{
    private const string FeedbackGuideResourceName = "Namfisa.Induction.EmailAttachments.EmployeeFeedbackGuide.pdf";
    private const string FeedbackGuideFileName = "NAMFISA Employee Induction Feedback Guide.pdf";

    public SmtpConfigurationStatus ConfigurationStatus => GetConfigurationStatus(options.CurrentValue);
    public string? PublicBaseUrl => string.IsNullOrWhiteSpace(options.CurrentValue.PublicBaseUrl)
        ? null
        : options.CurrentValue.PublicBaseUrl.Trim();

    public async Task SendInvitationAsync(
        InductionCase inductionCase,
        string inductionUrl,
        string subjectTemplate,
        string bodyTemplate,
        CancellationToken cancellationToken = default)
    {
        var emailOptions = options.CurrentValue;
        var configurationStatus = GetConfigurationStatus(emailOptions);
        if (!configurationStatus.IsConfigured)
        {
            throw new InvalidOperationException(configurationStatus.Summary);
        }
        if (string.IsNullOrWhiteSpace(inductionCase.EmployeeEmail))
        {
            throw new InvalidOperationException("The employee does not have an email address recorded.");
        }

        var subject = ApplyTokens(subjectTemplate, inductionCase, inductionUrl);
        var messageText = ApplyTokens(bodyTemplate, inductionCase, inductionUrl).Trim();
        if (!messageText.Contains(inductionUrl, StringComparison.OrdinalIgnoreCase))
        {
            messageText = $"{messageText}\r\n\r\nOpen induction plan: {inductionUrl}";
        }
        var plainText = $"{messageText}\r\n\r\nA step-by-step employee feedback guide is attached to this email.";

        using var message = new MailMessage
        {
            From = new MailAddress(emailOptions.FromAddress!, emailOptions.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(inductionCase.EmployeeEmail, inductionCase.EmployeeName));
        message.Headers.Add("X-NAMFISA-Induction-Id", inductionCase.Id.ToString(CultureInfo.InvariantCulture));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            plainText,
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            BuildHtmlBody(inductionCase, inductionUrl, messageText),
            Encoding.UTF8,
            MediaTypeNames.Text.Html));
        AddEmployeeFeedbackGuide(message);

        using var smtpClient = new SmtpClient(emailOptions.SmtpHost!, emailOptions.SmtpPort)
        {
            EnableSsl = emailOptions.UseSsl,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(emailOptions.Username),
            Timeout = Math.Max(5_000, emailOptions.TimeoutMilliseconds)
        };

        if (!string.IsNullOrWhiteSpace(emailOptions.Username))
        {
            smtpClient.Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message, cancellationToken);
    }

    public async Task SendAttendanceInvitationAsync(
        InductionCase inductionCase,
        InductionProgramme programme,
        string attendanceUrl,
        CancellationToken cancellationToken = default)
    {
        var emailOptions = options.CurrentValue;
        var configurationStatus = GetConfigurationStatus(emailOptions);
        if (!configurationStatus.IsConfigured)
        {
            throw new InvalidOperationException(configurationStatus.Summary);
        }
        if (string.IsNullOrWhiteSpace(inductionCase.EmployeeEmail))
        {
            throw new InvalidOperationException("The employee does not have an email address recorded.");
        }

        var venue = string.IsNullOrWhiteSpace(programme.Venue) ? "Venue to be confirmed" : programme.Venue;
        var subject = $"Attendance confirmation: {programme.Title} - {programme.ProgrammeDate:dd MMMM yyyy}";
        var messageText = $"Hello {inductionCase.FirstName},\r\n\r\nYou are scheduled to attend {programme.Title} on {programme.ProgrammeDate:dddd, dd MMMM yyyy} at {venue}. Please use the link below and sign in with your NAMFISA Windows account to confirm whether you are present or absent.\r\n\r\n{attendanceUrl}\r\n\r\nA step-by-step employee feedback guide is attached to this email.";

        using var message = new MailMessage
        {
            From = new MailAddress(emailOptions.FromAddress!, emailOptions.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(inductionCase.EmployeeEmail, inductionCase.EmployeeName));
        message.Headers.Add("X-NAMFISA-Induction-Id", inductionCase.Id.ToString(CultureInfo.InvariantCulture));
        message.Headers.Add("X-NAMFISA-Programme-Id", programme.Id.ToString(CultureInfo.InvariantCulture));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(messageText, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            BuildAttendanceHtmlBody(inductionCase, programme, attendanceUrl),
            Encoding.UTF8,
            MediaTypeNames.Text.Html));
        AddEmployeeFeedbackGuide(message);

        using var smtpClient = new SmtpClient(emailOptions.SmtpHost!, emailOptions.SmtpPort)
        {
            EnableSsl = emailOptions.UseSsl,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(emailOptions.Username),
            Timeout = Math.Max(5_000, emailOptions.TimeoutMilliseconds)
        };
        if (!string.IsNullOrWhiteSpace(emailOptions.Username))
        {
            smtpClient.Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message, cancellationToken);
    }

    private static SmtpConfigurationStatus GetConfigurationStatus(InductionEmailOptions emailOptions)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(emailOptions.SmtpHost)) missing.Add("SMTP server");
        if (emailOptions.SmtpPort is < 1 or > 65_535) missing.Add("valid SMTP port");
        if (string.IsNullOrWhiteSpace(emailOptions.FromAddress)) missing.Add("sender email address");
        else if (!MailAddress.TryCreate(emailOptions.FromAddress, out _)) missing.Add("valid sender email address");
        if (!string.IsNullOrWhiteSpace(emailOptions.Username) && string.IsNullOrWhiteSpace(emailOptions.Password))
        {
            missing.Add("SMTP password");
        }
        if (!string.IsNullOrWhiteSpace(emailOptions.PublicBaseUrl)
            && (!Uri.TryCreate(emailOptions.PublicBaseUrl, UriKind.Absolute, out var publicUri)
                || (publicUri.Scheme != Uri.UriSchemeHttp && publicUri.Scheme != Uri.UriSchemeHttps)))
        {
            missing.Add("valid public application URL");
        }

        if (missing.Count > 0)
        {
            return new SmtpConfigurationStatus(false, $"SMTP is not ready. Configure {string.Join(", ", missing)} on the application server.");
        }

        var security = emailOptions.UseSsl ? "TLS enabled" : "TLS disabled";
        return new SmtpConfigurationStatus(
            true,
            $"SMTP delivery is ready through {emailOptions.SmtpHost}:{emailOptions.SmtpPort} ({security}) from {emailOptions.FromAddress}.");
    }

    private static void AddEmployeeFeedbackGuide(MailMessage message)
    {
        var guideStream = typeof(InductionEmailService).Assembly
            .GetManifestResourceStream(FeedbackGuideResourceName)
            ?? throw new InvalidOperationException("The employee feedback guide attachment is not available.");

        var attachment = new Attachment(guideStream, FeedbackGuideFileName, MediaTypeNames.Application.Pdf);
        attachment.ContentDisposition!.DispositionType = DispositionTypeNames.Attachment;
        message.Attachments.Add(attachment);
    }

    private static string ApplyTokens(string template, InductionCase inductionCase, string inductionUrl) =>
        template
            .Replace("{EmployeeName}", inductionCase.EmployeeName, StringComparison.OrdinalIgnoreCase)
            .Replace("{FirstName}", inductionCase.FirstName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Position}", inductionCase.Position, StringComparison.OrdinalIgnoreCase)
            .Replace("{Department}", inductionCase.Department, StringComparison.OrdinalIgnoreCase)
            .Replace("{Division}", inductionCase.Division, StringComparison.OrdinalIgnoreCase)
            .Replace("{AppointmentDate}", inductionCase.AppointmentDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{InductionUrl}", inductionUrl, StringComparison.OrdinalIgnoreCase);

    private static string BuildHtmlBody(InductionCase inductionCase, string inductionUrl, string messageText)
    {
        var safeEmployeeName = WebUtility.HtmlEncode(inductionCase.EmployeeName);
        var safeDepartment = WebUtility.HtmlEncode(inductionCase.Department);
        var safeAppointmentDate = WebUtility.HtmlEncode(inductionCase.AppointmentDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));
        var safeUrl = WebUtility.HtmlEncode(inductionUrl);
        var safeMessage = WebUtility.HtmlEncode(messageText)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);

        return $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#f3f6f2;font-family:Segoe UI,Arial,sans-serif;color:#17211c;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f2;padding:28px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="width:100%;max-width:620px;background:#ffffff;border:1px solid #dce4dd;border-radius:14px;overflow:hidden;">
                    <tr><td style="background:#123b2b;border-bottom:4px solid #91cf49;padding:24px 30px;color:#ffffff;">
                      <div style="font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;color:#cfe9d7;">NAMFISA Human Resources</div>
                      <div style="font-size:26px;font-weight:700;margin-top:6px;">Employee induction invitation</div>
                    </td></tr>
                    <tr><td style="padding:30px;">
                      <div style="font-size:16px;line-height:1.65;">{{safeMessage}}</div>
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:24px 0;background:#f7faf7;border:1px solid #dce4dd;border-radius:10px;">
                        <tr><td style="padding:16px 18px;">
                          <div style="font-size:12px;color:#66736c;text-transform:uppercase;letter-spacing:.7px;font-weight:700;">Induction plan</div>
                          <div style="font-size:18px;font-weight:700;margin-top:4px;">{{safeEmployeeName}}</div>
                          <div style="font-size:14px;color:#66736c;margin-top:4px;">{{safeDepartment}} · Appointment {{safeAppointmentDate}}</div>
                        </td></tr>
                      </table>
                      <table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="background:#19513a;border-radius:8px;">
                        <a href="{{safeUrl}}" style="display:inline-block;padding:13px 22px;color:#ffffff;text-decoration:none;font-weight:700;">Open induction plan</a>
                      </td></tr></table>
                      <div style="margin-top:22px;padding:14px 16px;background:#f7faf7;border-left:4px solid #91cf49;font-size:13px;line-height:1.55;color:#4f5f56;"><strong style="color:#17211c;">Feedback guide attached</strong><br>The attached one-page guide explains how to confirm attendance, rate every presentation and submit your feedback.</div>
                      <p style="margin:24px 0 0;font-size:13px;line-height:1.55;color:#66736c;">Sign in with your NAMFISA Windows account. If the button does not open, copy this address into your browser:<br><a href="{{safeUrl}}" style="color:#19513a;word-break:break-all;">{{safeUrl}}</a></p>
                    </td></tr>
                    <tr><td style="padding:17px 30px;background:#edf3ee;color:#66736c;font-size:12px;">This invitation was sent by the NAMFISA Employee Induction system.</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildAttendanceHtmlBody(
        InductionCase inductionCase,
        InductionProgramme programme,
        string attendanceUrl)
    {
        var safeEmployeeName = WebUtility.HtmlEncode(inductionCase.EmployeeName);
        var safeProgrammeTitle = WebUtility.HtmlEncode(programme.Title);
        var safeProgrammeDate = WebUtility.HtmlEncode(programme.ProgrammeDate.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture));
        var safeVenue = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(programme.Venue) ? "Venue to be confirmed" : programme.Venue);
        var safeUrl = WebUtility.HtmlEncode(attendanceUrl);

        return $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#f3f6f2;font-family:Segoe UI,Arial,sans-serif;color:#17211c;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f2;padding:28px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="width:100%;max-width:620px;background:#ffffff;border:1px solid #dce4dd;border-radius:14px;overflow:hidden;">
                    <tr><td style="background:#123b2b;border-bottom:4px solid #91cf49;padding:24px 30px;color:#ffffff;">
                      <div style="font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;color:#cfe9d7;">NAMFISA Human Resources</div>
                      <div style="font-size:26px;font-weight:700;margin-top:6px;">Confirm induction attendance</div>
                    </td></tr>
                    <tr><td style="padding:30px;">
                      <p style="font-size:16px;line-height:1.65;margin-top:0;">Hello {{safeEmployeeName}}, please confirm whether you are present or absent for this induction programme.</p>
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:24px 0;background:#f7faf7;border:1px solid #dce4dd;border-radius:10px;">
                        <tr><td style="padding:16px 18px;">
                          <div style="font-size:18px;font-weight:700;">{{safeProgrammeTitle}}</div>
                          <div style="font-size:14px;color:#66736c;margin-top:5px;">{{safeProgrammeDate}} · {{safeVenue}}</div>
                        </td></tr>
                      </table>
                      <table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="background:#19513a;border-radius:8px;">
                        <a href="{{safeUrl}}" style="display:inline-block;padding:13px 22px;color:#ffffff;text-decoration:none;font-weight:700;">Confirm attendance</a>
                      </td></tr></table>
                      <div style="margin-top:22px;padding:14px 16px;background:#f7faf7;border-left:4px solid #91cf49;font-size:13px;line-height:1.55;color:#4f5f56;"><strong style="color:#17211c;">Feedback guide attached</strong><br>The attached one-page guide explains how to continue from attendance confirmation to programme feedback.</div>
                      <p style="margin:24px 0 0;font-size:13px;line-height:1.55;color:#66736c;">Sign in with your NAMFISA Windows account. If the button does not open, copy this address into your browser:<br><a href="{{safeUrl}}" style="color:#19513a;word-break:break-all;">{{safeUrl}}</a></p>
                    </td></tr>
                    <tr><td style="padding:17px 30px;background:#edf3ee;color:#66736c;font-size:12px;">Attendance confirmations are recorded against the authenticated employee AD account.</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
