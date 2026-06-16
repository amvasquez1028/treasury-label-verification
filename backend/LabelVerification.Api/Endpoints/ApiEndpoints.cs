using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using LabelVerification.Api.Services;
using LabelVerification.Core.Cola;
using LabelVerification.Core.Extraction;
using LabelVerification.Core.Models;
using LabelVerification.Core.Ocr;
using LabelVerification.Core.Services;
using LabelVerification.Infrastructure;
using LabelVerification.Infrastructure.Data;
using LabelVerification.Infrastructure.Email;
using LabelVerification.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace LabelVerification.Api.Endpoints;

public static class ApiEndpoints
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg"
    };

    private static readonly JsonSerializerOptions ExpectedFieldsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void MapApiEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").RequireRateLimiting("auth");
        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        auth.MapGet("/approve", ProcessApprovalGetAsync);
        auth.MapPost("/approve", ProcessApprovalPostAsync);

        var verify = app.MapGroup("/api/v1/verify").RequireAuthorization().RequireRateLimiting("verify");
        verify.MapPost("/", VerifySingleAsync);
        verify.MapPost("/batch", VerifyBatchAsync);
        verify.MapPost("/extract", ExtractLabelAsync);
        verify.MapPost("/autonomous", VerifyAutonomousAsync);
        verify.MapPost("/batch/autonomous", VerifyAutonomousBatchAsync);

        var cola = app.MapGroup("/api/v1/cola").RequireAuthorization();
        cola.MapGet("/{ttbId}/expected-fields", GetColaExpectedFieldsAsync);

        var contact = app.MapGroup("/api/v1/contact").RequireAuthorization().RequireRateLimiting("contact");
        contact.MapPost("/", ContactAsync);

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", (ITesseractEngineProvider engine) =>
            engine.IsReady
                ? Results.Ok(new { status = "ready", ocr = "warmed" })
                : Results.StatusCode(503));
    }

    private static bool IsPublicRegistrationDisabled(IConfiguration config) =>
        config.GetValue("DISABLE_PUBLIC_REGISTRATION", false);

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        AuthDbContext db,
        IRegistrationApprovalEmailSender emailSender,
        IOptions<SendGridOptions> sendGridOptions,
        IConfiguration configuration,
        IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        if (IsPublicRegistrationDisabled(configuration))
        {
            return Results.Problem("Public registration is disabled.", statusCode: 403);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Results.Conflict(new { message = "Email already registered." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            ApprovalStatus = ApprovalStatus.Pending
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });
        }

        var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenTtlHours = configuration.GetValue("Auth:ApprovalTokenTtlHours", 1);
        db.RegistrationApprovalTokens.Add(new RegistrationApprovalToken
        {
            UserId = user.Id,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(tokenTtlHours)
        });
        await db.SaveChangesAsync(cancellationToken);

        var approvalLink = $"{sendGridOptions.Value.PublicBaseUrl.TrimEnd('/')}/approve?token={tokenValue}";
        await emailSender.SendApprovalRequestAsync(sendGridOptions.Value.ApproverEmail, request.Email, approvalLink, cancellationToken);

        return Results.Accepted(value: new { message = "Registration pending approval." });
    }

    private static async Task<IResult> ProcessApprovalGetAsync(
        string token,
        string? action,
        IConfiguration configuration,
        AuthDbContext db,
        UserManager<ApplicationUser> userManager,
        IRegistrationApprovalEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Auth:AllowGetApprovalAction", false))
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return Results.BadRequest(new { message = "Action must be approve or deny." });
            }

            return await ProcessApprovalCoreAsync(token, action, db, userManager, emailSender, cancellationToken);
        }

        return Results.Json(
            new
            {
                message = "Use POST /api/v1/auth/approve with JSON body { token, action } or open the approval page from the email link.",
            },
            statusCode: StatusCodes.Status405MethodNotAllowed);
    }

    private static async Task<IResult> ProcessApprovalPostAsync(
        ApprovalActionRequest request,
        AuthDbContext db,
        UserManager<ApplicationUser> userManager,
        IRegistrationApprovalEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Action))
        {
            return Results.BadRequest(new { message = "Token and action are required." });
        }

        return await ProcessApprovalCoreAsync(request.Token, request.Action, db, userManager, emailSender, cancellationToken);
    }

    private static async Task<IResult> ProcessApprovalCoreAsync(
        string token,
        string action,
        AuthDbContext db,
        UserManager<ApplicationUser> userManager,
        IRegistrationApprovalEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        var record = await db.RegistrationApprovalTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed, cancellationToken);

        if (record is null || record.ExpiresAt < DateTimeOffset.UtcNow || record.User is null)
        {
            return Results.BadRequest(new { message = "Invalid or expired token." });
        }

        var approve = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);
        var deny = string.Equals(action, "deny", StringComparison.OrdinalIgnoreCase);
        if (!approve && !deny)
        {
            return Results.BadRequest(new { message = "Action must be approve or deny." });
        }

        record.IsUsed = true;
        record.User.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Denied;
        record.User.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (approve)
        {
            await emailSender.SendApprovedNoticeAsync(record.User.Email!, cancellationToken);
        }
        else
        {
            await emailSender.SendDeniedNoticeAsync(record.User.Email!, cancellationToken);
        }

        return Results.Ok(new { message = approve ? "User approved." : "User denied." });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IValidator<LoginRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Ok(new { email = user.Email, approvalStatus = user.ApprovalStatus.ToString() });
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok(new { message = "Signed out." });
    }

    private static async Task<IResult> MeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new { email = user.Email, approvalStatus = user.ApprovalStatus.ToString() });
    }


    private static IResult GetColaExpectedFieldsAsync(string ttbId, IColaPublicCache cache)
    {
        if (!cache.TryGetEntry(ttbId, out var entry))
        {
            return Results.Json(
                new
                {
                    error = "TTB ID not found in public registry cache",
                    availableTtbIds = cache.ListIds(),
                },
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(ColaExpectedFieldsResponse.FromEntry(entry));
    }

    private static async Task<IResult> ContactAsync(
        ContactRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        IContactEmailSender contactEmailSender,
        IValidator<ContactRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user?.Email is null)
        {
            return Results.Unauthorized();
        }

        await contactEmailSender.SendContactMessageAsync(
            request.Name,
            user.Email,
            request.Subject,
            request.Message,
            cancellationToken);

        return Results.Ok(new { message = "Your message has been sent." });
    }

    private static async Task<IResult> VerifySingleAsync(
        HttpContext context,
        IVerificationService verificationService,
        OcrConcurrencyGate concurrencyGate,
        OcrPerUserLimiter perUserLimiter,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var image = form.Files.GetFile("image");
        var expectedJson = form["expected"].ToString();

        if (image is null || image.Length == 0)
        {
            return Results.BadRequest(new { message = "Image file is required." });
        }

        var maxBytes = configuration.GetValue("MAX_UPLOAD_BYTES", 5 * 1024 * 1024);
        if (image.Length > maxBytes)
        {
            return Results.BadRequest(new { message = $"Image exceeds {maxBytes} bytes." });
        }

        if (!AllowedContentTypes.Contains(image.ContentType))
        {
            return Results.BadRequest(new { message = "Only PNG and JPEG uploads are supported." });
        }

        ExpectedLabelFields? expected;
        try
        {
            expected = JsonSerializer.Deserialize<ExpectedLabelFields>(expectedJson, ExpectedFieldsJsonOptions);
        }
        catch
        {
            expected = null;
        }

        if (expected is null)
        {
            return Results.BadRequest(new { message = "Expected label JSON is required." });
        }

        using var userLease = await perUserLimiter.AcquireAsync(userId, cancellationToken);
        using var lease = await concurrencyGate.AcquireAsync(cancellationToken);

        await using var stream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var result = await verificationService.VerifyAsync(
            ms.ToArray(),
            expected,
            cancellationToken: cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> VerifyBatchAsync(
        HttpContext context,
        IVerificationService verificationService,
        OcrConcurrencyGate concurrencyGate,
        OcrPerUserLimiter perUserLimiter,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var files = form.Files.Where(f => f.Name == "images" || f.Name == "images[]").ToList();

        if (files.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one image is required." });
        }

        var expectedList = ParseExpectedListFromForm(form, files.Count);
        if (expectedList is null)
        {
            return Results.BadRequest(new { message = "Expected label JSON (expectedList array or expected object) is required." });
        }

        if (expectedList.Count != files.Count)
        {
            return Results.BadRequest(new
            {
                message = $"Image count ({files.Count}) must match expected parameter count ({expectedList.Count})."
            });
        }

        var ocrTextList = ParseStringListFromForm(form, "ocrTextList", files.Count);
        var useClientOcr = string.Equals(form["useClientOcr"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

        var maxBytes = configuration.GetValue("MAX_UPLOAD_BYTES", 5 * 1024 * 1024);
        var items = new List<BatchVerificationRequestItem>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length > maxBytes)
            {
                return Results.BadRequest(new { message = $"File {file.FileName} exceeds upload limit." });
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                return Results.BadRequest(new { message = $"File {file.FileName} has unsupported content type." });
            }

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);

            var expected = expectedList[i];

            items.Add(new BatchVerificationRequestItem(
                file.FileName,
                ms.ToArray(),
                expected,
                i < ocrTextList.Count ? ocrTextList[i] : null,
                useClientOcr));
        }

        using var userLease = await perUserLimiter.AcquireAsync(userId, cancellationToken);

        var batch = await verificationService.VerifyBatchAsync(items, cancellationToken);
        return Results.Ok(batch);
    }

    private static List<string> ParseStringListFromForm(IFormCollection form, string fieldName, int fileCount)
    {
        var raw = form[fieldName].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Enumerable.Repeat(string.Empty, fileCount).ToList();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            if (list is null || list.Count == 0)
            {
                return Enumerable.Repeat(string.Empty, fileCount).ToList();
            }

            while (list.Count < fileCount)
            {
                list.Add(string.Empty);
            }

            return list;
        }
        catch
        {
            return Enumerable.Repeat(string.Empty, fileCount).ToList();
        }
    }

    private static List<ExpectedLabelFields>? ParseExpectedListFromForm(IFormCollection form, int fileCount)
    {
        var jsonOptions = ExpectedFieldsJsonOptions;
        var expectedListJson = form["expectedList"].ToString();
        var expectedJson = form["expected"].ToString();
        var applicationsJson = form["applications"].ToString();

        if (!string.IsNullOrWhiteSpace(expectedListJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ExpectedLabelFields>>(expectedListJson, jsonOptions);
                return list is { Count: > 0 } ? list : null;
            }
            catch
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(applicationsJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ExpectedLabelFields>>(applicationsJson, jsonOptions);
                return list is { Count: > 0 } ? list : null;
            }
            catch
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedJson))
        {
            try
            {
                if (expectedJson.TrimStart().StartsWith('['))
                {
                    var list = JsonSerializer.Deserialize<List<ExpectedLabelFields>>(expectedJson, jsonOptions);
                    return list is { Count: > 0 } ? list : null;
                }

                var single = JsonSerializer.Deserialize<ExpectedLabelFields>(expectedJson, jsonOptions);
                if (single is null)
                {
                    return null;
                }

                return Enumerable.Repeat(single, fileCount).ToList();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static async Task<IResult> ExtractLabelAsync(
        HttpContext context,
        ILabelFieldExtractor extractor,
        OcrConcurrencyGate concurrencyGate,
        OcrPerUserLimiter perUserLimiter,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadSingleImageAsync(context, configuration, cancellationToken);
        if (bytes is null)
        {
            return Results.BadRequest(new { message = "Image file is required." });
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        using var userLease = await perUserLimiter.AcquireAsync(userId, cancellationToken);
        using var lease = await concurrencyGate.AcquireAsync(cancellationToken);

        var result = await extractor.ExtractAsync(bytes, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> VerifyAutonomousAsync(
        HttpContext context,
        IAutonomousVerificationService autonomousService,
        OcrConcurrencyGate concurrencyGate,
        OcrPerUserLimiter perUserLimiter,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var image = form.Files.GetFile("image");
        if (image is null || image.Length == 0)
        {
            return Results.BadRequest(new { message = "Image file is required." });
        }

        var maxBytes = configuration.GetValue("MAX_UPLOAD_BYTES", 5 * 1024 * 1024);
        if (image.Length > maxBytes)
        {
            return Results.BadRequest(new { message = $"Image exceeds {maxBytes} bytes." });
        }

        if (!AllowedContentTypes.Contains(image.ContentType))
        {
            return Results.BadRequest(new { message = "Only PNG and JPEG uploads are supported." });
        }

        await using var stream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var ttbIdHint = form["ttbId"].ToString();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        using var userLease = await perUserLimiter.AcquireAsync(userId, cancellationToken);
        using var lease = await concurrencyGate.AcquireAsync(cancellationToken);

        var result = await autonomousService.VerifyAsync(
            ms.ToArray(),
            string.IsNullOrWhiteSpace(ttbIdHint) ? null : ttbIdHint,
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> VerifyAutonomousBatchAsync(
        HttpContext context,
        IAutonomousVerificationService autonomousService,
        OcrConcurrencyGate concurrencyGate,
        OcrPerUserLimiter perUserLimiter,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var files = form.Files.Where(f => f.Name == "images" || f.Name == "images[]").ToList();
        if (files.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one image is required." });
        }

        var ttbIdList = ParseStringListFromForm(form, "ttbIdList", files.Count);
        var maxBytes = configuration.GetValue("MAX_UPLOAD_BYTES", 5 * 1024 * 1024);
        var items = new List<(string FileName, byte[] Bytes, string? TtbIdHint)>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length > maxBytes)
            {
                return Results.BadRequest(new { message = $"File {file.FileName} exceeds upload limit." });
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                return Results.BadRequest(new { message = $"File {file.FileName} has unsupported content type." });
            }

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var hint = i < ttbIdList.Count && !string.IsNullOrWhiteSpace(ttbIdList[i]) ? ttbIdList[i] : null;
            items.Add((file.FileName, ms.ToArray(), hint));
        }

        using var userLease = await perUserLimiter.AcquireAsync(userId, cancellationToken);
        using var lease = await concurrencyGate.AcquireAsync(cancellationToken);

        var results = await autonomousService.VerifyBatchAsync(items, cancellationToken);
        return Results.Ok(new
        {
            items = results.Select((r, i) => new
            {
                fileName = items[i].FileName,
                result = r,
            }),
            successCount = results.Count(r => r.Verification.OverallStatus != VerificationOutcome.Timeout),
            failureCount = results.Count(r => r.Verification.OverallStatus == VerificationOutcome.Timeout),
        });
    }

    private static async Task<byte[]?> ReadSingleImageAsync(
        HttpContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var image = form.Files.GetFile("image");
        if (image is null || image.Length == 0)
        {
            return null;
        }

        var maxBytes = configuration.GetValue("MAX_UPLOAD_BYTES", 5 * 1024 * 1024);
        if (image.Length > maxBytes || !AllowedContentTypes.Contains(image.ContentType))
        {
            return null;
        }

        await using var stream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record ApprovalActionRequest(string Token, string Action);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).MinimumLength(10);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}




public sealed record ContactRequest(string Name, string Subject, string Message);

public sealed class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(5000);
    }
}
