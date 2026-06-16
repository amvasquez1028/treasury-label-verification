namespace LabelVerification.Infrastructure.Email;

using Microsoft.Extensions.Logging;

public interface IRegistrationApprovalEmailSender
{
    Task SendApprovalRequestAsync(string approverEmail, string userEmail, string approvalLink, CancellationToken cancellationToken = default);
    Task SendApprovedNoticeAsync(string userEmail, CancellationToken cancellationToken = default);
    Task SendDeniedNoticeAsync(string userEmail, CancellationToken cancellationToken = default);
}

public sealed class SendGridRegistrationApprovalEmailSender : IRegistrationApprovalEmailSender
{
    private readonly SendGrid.ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridRegistrationApprovalEmailSender> _logger;

    public SendGridRegistrationApprovalEmailSender(
        SendGrid.ISendGridClient client,
        Microsoft.Extensions.Options.IOptions<SendGridOptions> options,
        ILogger<SendGridRegistrationApprovalEmailSender> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendApprovalRequestAsync(string approverEmail, string userEmail, string approvalLink, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key missing; skipping approval email to {Approver}", approverEmail);
            return;
        }

        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
            new SendGrid.Helpers.Mail.EmailAddress(_options.FromEmail, _options.FromName),
            new SendGrid.Helpers.Mail.EmailAddress(approverEmail),
            "Label Verification registration approval",
            $"User {userEmail} requested access. Approve: {approvalLink}",
            $"<p>User <strong>{userEmail}</strong> requested access.</p><p><a href=\"{approvalLink}\">Approve or deny registration</a></p>");

        await _client.SendEmailAsync(msg, cancellationToken);
    }

    public async Task SendApprovedNoticeAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key missing; skipping approved notice to {User}", userEmail);
            return;
        }

        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
            new SendGrid.Helpers.Mail.EmailAddress(_options.FromEmail, _options.FromName),
            new SendGrid.Helpers.Mail.EmailAddress(userEmail),
            "Label Verification account approved",
            "Your account has been approved. You may sign in.",
            "<p>Your account has been approved. You may sign in.</p>");

        await _client.SendEmailAsync(msg, cancellationToken);
    }

    public async Task SendDeniedNoticeAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("SendGrid API key missing; skipping denied notice to {User}", userEmail);
            return;
        }

        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
            new SendGrid.Helpers.Mail.EmailAddress(_options.FromEmail, _options.FromName),
            new SendGrid.Helpers.Mail.EmailAddress(userEmail),
            "Label Verification account denied",
            "Your registration request was denied.",
            "<p>Your registration request was denied.</p>");

        await _client.SendEmailAsync(msg, cancellationToken);
    }
}

public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@label-verify.demo";
    public string FromName { get; set; } = "Treasury Label Verification";
    public string ApproverEmail { get; set; } = "admin@label-verify.demo";
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
}
