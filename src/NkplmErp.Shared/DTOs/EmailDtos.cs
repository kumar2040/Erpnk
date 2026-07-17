namespace NkplmErp.Shared.DTOs;

// ============================================================================
// Email sending DTOs. EmailSetupModel mirrors dbo.tblEmailSetting (read via
// spEmailSetting @Flag = 'S'; Dapper maps by name). Every tblEmailSetting
// column is NVARCHAR — including Port — so they map as strings and
// EmailService parses what it needs. EmailModel is the message to send.
// ============================================================================

public class EmailModel
{
    public string Recipient { get; set; } = string.Empty;   // ';' or ',' separated list allowed
    public string? Cc { get; set; }                          // ';' or ',' separated list allowed
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; } = true;

    // Optional single attachment.
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public byte[]? AttachmentContent { get; set; }
}

public class EmailSetupModel
{
    public int Id { get; set; }
    public string? EmailType { get; set; }
    public string? MailServer { get; set; }
    public string? Port { get; set; }          // nvarchar in tblEmailSetting — parsed by EmailService
    public string? SenderName { get; set; }
    public string? SenderEmail { get; set; }
    public string? EmailFormat { get; set; }
    public string? Password { get; set; }
    public string? Username { get; set; }
}
