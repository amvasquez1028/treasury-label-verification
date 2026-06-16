using Microsoft.Extensions.Logging;

namespace LabelVerification.Infrastructure.Email;

public sealed class ContactOptions
{
    public const string SectionName = "Contact";
    public string NotifyEmail { get; set; } = string.Empty;
}

public interface IContactEmailSender
{
    Task SendContactMessageAsync(
        string senderName,
        string senderEmail,
        string subject,
        string message,
        CancellationToken cancellationToken = default);
}

public sealed class SendGridContactEmailSender : IContactEmailSender
{
    private readonly SendGrid.ISendGridClient _client;
    private readonly SendGridOptions _sendGridOptions;
    private readonly ContactOptions _contactOptions;
    private readonly Microsoft.Extensions.Logging.ILogger<SendGridContactEmailSender> _logger;

    public SendGridContactEmailSender(
        SendGrid.ISendGridClient client,
        Microsoft.Extensions.Options.IOptions<SendGridOptions> sendGridOptions,
        Microsoft.Extensions.Options.IOptions<ContactOptions> contactOptions,
        Microsoft.Extensions.Logging.ILogger<SendGridContactEmailSender> logger)
    {
        _client = client;
        _sendGridOptions = sendGridOptions.Value;
        _contactOptions = contactOptions.Value;
        _logger = logger;
    }

    public async Task SendContactMessageAsync(
        string senderName,
        string senderEmail,
        string subject,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey))
        {
            _logger.LogWarning("SendGrid API key missing; skipping contact email");
            return;
        }

        if (string.IsNullOrWhiteSpace(_contactOptions.NotifyEmail))
        {
            _logger.LogWarning("Contact notify email not configured; skipping contact email");
            return;
        }

        var plainBody = $"From: {senderName} ({senderEmail})\nSubject: {subject}\n\n{message}";
        var htmlBody = $"<p><strong>From:</strong> {senderName} ({senderEmail})</p><p><strong>Subject:</strong> {subject}</p><p>{message.Replace("\n", "<br/>")}</p>";

        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
            new SendGrid.Helpers.Mail.EmailAddress(_sendGridOptions.FromEmail, _sendGridOptions.FromName),
            new SendGrid.Helpers.Mail.EmailAddress(_contactOptions.NotifyEmail),
            $"[Label Verification Contact] {subject}",
            plainBody,
            htmlBody);

        msg.ReplyTo = new SendGrid.Helpers.Mail.EmailAddress(senderEmail, senderName);

        await _client.SendEmailAsync(msg, cancellationToken);
    }
}

