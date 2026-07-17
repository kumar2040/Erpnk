using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// SMTP email sending. Connection settings live in dbo.tblEmailSetting and are
/// read per send via spEmailSetting (@Flag = 'S'). Direct send only for now —
/// the tblMailLog outbox drain / recurring Hangfire job is a later phase.
/// </summary>
public interface IEmailService
{
    /// <summary>Send one message using the tblEmailSetting row identified by <paramref name="emailSettingId"/>.</summary>
    Task SendEmailAsync(EmailModel email, int emailSettingId);

    /// <summary>All configured sender addresses (spEmailSetting @Flag = 'G').</summary>
    Task<List<string>> GetSenderEmailsAsync();
}
