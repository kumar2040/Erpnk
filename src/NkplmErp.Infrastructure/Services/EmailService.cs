using System.Data;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Repositories.Interface;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// SMTP sender. Settings come from dbo.tblEmailSetting via spEmailSetting
/// (flag-dispatched, same pattern as the PoTask procs) — this service just
/// shapes the MailMessage and hands it to System.Net.Mail.SmtpClient.
/// Intended to be called from Hangfire background jobs; it throws on failure
/// so Hangfire's automatic retries apply.
/// </summary>
public class EmailService : IEmailService
{
    private const string SettingSp = "spEmailSetting";
    private const int DefaultSmtpPort = 587;

    private readonly IDapperRepository _repo;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IDapperRepository repo, ILogger<EmailService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailModel email, int emailSettingId)
    {
        if (string.IsNullOrWhiteSpace(email.Recipient))
            throw new ArgumentException("Recipient is required.", nameof(email));

        var setup = await _repo.GetQueryFirstOrDefaultResultAsync<EmailSetupModel>(SettingSp,
            new { Flag = "S", EmailId = emailSettingId }, CommandType.StoredProcedure);

        if (setup is null || string.IsNullOrWhiteSpace(setup.MailServer) || string.IsNullOrWhiteSpace(setup.SenderEmail))
            throw new InvalidOperationException(
                $"Email setting {emailSettingId} is missing or incomplete (dbo.tblEmailSetting).");

        var port = int.TryParse(setup.Port, out var parsed) ? parsed : DefaultSmtpPort;

        using var message = new MailMessage
        {
            From = new MailAddress(setup.SenderEmail, setup.SenderName ?? setup.SenderEmail),
            Subject = email.Subject,
            Body = email.Body,
            IsBodyHtml = email.IsHtml
        };

        foreach (var to in Split(email.Recipient)) message.To.Add(to);
        foreach (var cc in Split(email.Cc)) message.CC.Add(cc);

        if (email.AttachmentContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(email.AttachmentFileName))
        {
            // MailMessage.Dispose() disposes the attachment and its stream.
            message.Attachments.Add(new Attachment(
                new MemoryStream(email.AttachmentContent), email.AttachmentFileName, email.AttachmentContentType));
        }

        using var client = new SmtpClient(setup.MailServer, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = port != 25,                 // 587/465 → STARTTLS/TLS; plain relay on 25 stays clear
            UseDefaultCredentials = false,          // MUST precede Credentials (setting it clears them)
            Credentials = new NetworkCredential(setup.Username, setup.Password)
        };

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent via setting {SettingId} to {Recipient}.", emailSettingId, email.Recipient);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP send failed via setting {SettingId} to {Recipient}.", emailSettingId, email.Recipient);
            throw;
        }
    }

    public Task<List<string>> GetSenderEmailsAsync() =>
        _repo.GetQueryResultAsync<string>(SettingSp, new { Flag = "G" }, CommandType.StoredProcedure);

    private static IEnumerable<string> Split(string? addresses) =>
        (addresses ?? string.Empty).Split(new[] { ';', ',' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
