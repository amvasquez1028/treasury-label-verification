using LabelVerification.Infrastructure.Data;
using LabelVerification.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Infrastructure;

public static class AuthDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seedDemoUsers = configuration["SEED_DEMO_USERS"];
        if (!IsTruthy(seedDemoUsers))
        {
            return;
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
        await seeder.SeedAsync();
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
}

public sealed class DemoUserSeeder
{
    private const string DemoAgentEmail = "demo.agent@label-verify.demo";
    private const string DemoParallelEmail = "demo.parallel@label-verify.demo";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoUserSeeder> _logger;

    public DemoUserSeeder(UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger<DemoUserSeeder> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureDemoUserAsync(DemoAgentEmail, "DEMO_AGENT_PASSWORD");
        await EnsureDemoUserAsync(DemoParallelEmail, "DEMO_PARALLEL_PASSWORD");
    }

    private async Task EnsureDemoUserAsync(string email, string passwordEnvVar)
    {
        var password = Environment.GetEnvironmentVariable(passwordEnvVar)
            ?? _configuration[passwordEnvVar];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Skipping demo user {Email}: {EnvVar} is not set", email, passwordEnvVar);
            return;
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await SyncExistingDemoUserAsync(existing, password);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ApprovalStatus = ApprovalStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            _logger.LogInformation("Seeded demo user {Email}", email);
        }
        else
        {
            _logger.LogWarning("Failed to seed demo user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SyncExistingDemoUserAsync(ApplicationUser user, string password)
    {
        var updated = false;

        if (user.ApprovalStatus != ApprovalStatus.Approved)
        {
            user.ApprovalStatus = ApprovalStatus.Approved;
            user.ApprovedAt = DateTimeOffset.UtcNow;
            updated = true;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            updated = true;
        }

        if (updated)
        {
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to update demo user {Email}: {Errors}",
                    user.Email,
                    string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (!hasPassword || !await _userManager.CheckPasswordAsync(user, password))
        {
            IdentityResult passwordResult;
            if (hasPassword)
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, password);
            }
            else
            {
                passwordResult = await _userManager.AddPasswordAsync(user, password);
            }

            if (passwordResult.Succeeded)
            {
                _logger.LogInformation("Reset demo password for {Email}", user.Email);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to reset demo password for {Email}: {Errors}",
                    user.Email,
                    string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
            }
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }
    }
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddLabelVerificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var isProduction = environment?.IsProduction()
            ?? string.Equals(
                configuration["ASPNETCORE_ENVIRONMENT"],
                Environments.Production,
                StringComparison.OrdinalIgnoreCase);
        services.Configure<Email.SendGridOptions>(configuration.GetSection(Email.SendGridOptions.SectionName));
        services.Configure<Email.SendGridOptions>(options =>
        {
            var apiKey = configuration["SENDGRID_API_KEY"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                options.ApiKey = apiKey;
            }
        });
        services.Configure<Email.ContactOptions>(configuration.GetSection(Email.ContactOptions.SectionName));
        services.Configure<Email.ContactOptions>(options =>
        {
            var envNotify = configuration["CONTACT_NOTIFY_EMAIL"];
            if (!string.IsNullOrWhiteSpace(envNotify))
            {
                options.NotifyEmail = envNotify;
            }
        });

        var authDbPath = configuration["AUTH_DB_PATH"];
        var connectionString = !string.IsNullOrWhiteSpace(authDbPath)
            ? $"Data Source={authDbPath}"
            : configuration.GetConnectionString("Auth") ?? "Data Source=auth.db";
        services.AddDbContext<AuthDbContext>(options => options.UseSqlite(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "label_verify_auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = isProduction
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddSingleton<SendGrid.ISendGridClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Email.SendGridOptions>>().Value;
            // SendGridClient rejects null keys at construction; email senders skip when ApiKey is unset.
            var apiKey = string.IsNullOrWhiteSpace(opts.ApiKey) ? "SG.local-dev-placeholder" : opts.ApiKey;
            return new SendGrid.SendGridClient(apiKey);
        });

        services.AddScoped<Email.IRegistrationApprovalEmailSender, Email.SendGridRegistrationApprovalEmailSender>();
        services.AddScoped<Email.IContactEmailSender, Email.SendGridContactEmailSender>();
        services.AddScoped<DemoUserSeeder>();
        return services;
    }
}
