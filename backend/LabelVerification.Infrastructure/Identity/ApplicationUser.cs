namespace LabelVerification.Infrastructure.Identity;

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}

public sealed class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser
{
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }
}
