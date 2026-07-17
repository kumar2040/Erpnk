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
    private const string MailLogSp = "sp_ManageMailLog";
    private const int DefaultSmtpPort = 587;
    private const int BatchSize = 20;         // max outbox rows per job run
    private const int MaxRetryCount = 5;      // rows past this stay failed with error_msg
    private const int ThrottleMs = 200;       // pause between sends (SMTP rate-limit courtesy)

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

        var setup = await GetSetupAsync(emailSettingId);

        using var message = new MailMessage
        {
            From = new MailAddress(setup.SenderEmail!, setup.SenderName ?? setup.SenderEmail),
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

        using var client = CreateClient(setup);

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

    /// <summary>
    /// Outbox drain — Hangfire "send-email-job" calls this every minute.
    /// Authenticates once per batch, claims each row before sending so an
    /// overlapping run can't double-send, and records per-row outcomes in
    /// tblMailLog (is_sent/sent_date on success, retry_count/error_msg on failure).
    /// </summary>
    public async Task SendEmailTask(int emailSettingId)
    {
        var pending = await _repo.GetQueryResultAsync<MailLogDto>(MailLogSp,
            new { Flag = "PENDING", Top = BatchSize, MaxRetry = MaxRetryCount }, CommandType.StoredProcedure);
        if (pending is null || pending.Count == 0) return;   // empty outbox — no-op until next tick

        // Settings missing/incomplete → throw BEFORE claiming anything: the job
        // run fails (visible in the Hangfire dashboard), rows stay pending and
        // no retry_count is burned on a config problem.
        var setup = await GetSetupAsync(emailSettingId);
        using var client = CreateClient(setup);

        var sent = 0;
        foreach (var mail in pending)
        {
            var claimed = await _repo.GetQueryFirstOrDefaultResultAsync<int>(MailLogSp,
                new { Flag = "CLAIM", MailId = mail.MailId }, CommandType.StoredProcedure);
            if (claimed != 1) continue;   // another run already took this row

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(setup.SenderEmail!, setup.SenderName ?? setup.SenderEmail),
                    Subject = mail.Subject,
                    Body = mail.Body,
                    IsBodyHtml = true
                };
                foreach (var to in Split(mail.MailTo)) message.To.Add(to);
                foreach (var cc in Split(mail.MailCc)) message.CC.Add(cc);

                await client.SendMailAsync(message);
                await _repo.GetQueryFirstOrDefaultResultAsync<int>(MailLogSp,
                    new { Flag = "SENT", MailId = mail.MailId }, CommandType.StoredProcedure);
                sent++;
            }
            catch (Exception ex)
            {
                // Release the claim, bump retry_count, keep the reason on the row.
                await _repo.GetQueryFirstOrDefaultResultAsync<int>(MailLogSp,
                    new { Flag = "FAILED", MailId = mail.MailId, ErrorMsg = ex.Message }, CommandType.StoredProcedure);
                _logger.LogError(ex, "Outbox mail {MailId} ({MailType}) failed to send.", mail.MailId, mail.MailType);
            }

            await Task.Delay(ThrottleMs);
        }

        _logger.LogInformation("Outbox drain: {Sent}/{Batch} mail(s) sent via setting {SettingId}.",
            sent, pending.Count, emailSettingId);
    }

    private async Task<EmailSetupModel> GetSetupAsync(int emailSettingId)
    {
        var setup = await _repo.GetQueryFirstOrDefaultResultAsync<EmailSetupModel>(SettingSp,
            new { Flag = "S", EmailId = emailSettingId }, CommandType.StoredProcedure);

        if (setup is null || string.IsNullOrWhiteSpace(setup.MailServer) || string.IsNullOrWhiteSpace(setup.SenderEmail))
            throw new InvalidOperationException(
                $"Email setting {emailSettingId} is missing or incomplete (dbo.tblEmailSetting).");

        return setup;
    }

    private static SmtpClient CreateClient(EmailSetupModel setup)
    {
        var port = int.TryParse(setup.Port, out var parsed) ? parsed : DefaultSmtpPort;
        return new SmtpClient(setup.MailServer, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = port != 25,                 // 587/465 → STARTTLS/TLS; plain relay on 25 stays clear
            UseDefaultCredentials = false,          // MUST precede Credentials (setting it clears them)
            Credentials = new NetworkCredential(setup.Username, setup.Password)
        };
    }

    private static IEnumerable<string> Split(string? addresses) =>
        (addresses ?? string.Empty).Split(new[] { ';', ',' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
