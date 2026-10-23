using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it in configuration or user secrets.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<SessionService>();

builder.Services.AddDbContext<HrmsDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Hrms")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<TenantClock>();
builder.Services.AddScoped<ApprovalPolicy>();
builder.Services.AddScoped<ApprovalEngine>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<AttendanceRequestService>();
builder.Services.AddScoped<CompOffService>();
builder.Services.AddScoped<PayRequestService>();
builder.Services.AddScoped<ProfileChangeService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<ShiftRequestService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<HelpdeskService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<PerformanceService>();
builder.Services.AddScoped<RecruitmentService>();
builder.Services.AddScoped<TravelService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<ScheduledService>();
builder.Services.AddScoped<DeskService>();
builder.Services.AddScoped<ReportCatalog>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions());
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailBackgroundService>();
builder.Services.AddHostedService<HelpdeskReminderService>();
builder.Services.AddHostedService<ScheduledWorkService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<LetterService>();
builder.Services.AddScoped<SeparationService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<CompanySetupService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TenantAccessChecker>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.SuperAdmin, p => p.RequireRole(nameof(UserRole.SuperAdmin)))
    .AddPolicy(Policies.CompanyUser, p => p.RequireClaim(HrmsClaims.TenantId))
    // SuperAdmin passes these only with a company-scoped token (see PlatformController.Enter), because
    // every controller using them also requires CompanyUser (a tenant_id claim).
    .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin)))
    .AddPolicy(Policies.Hr, p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager)))
    .AddPolicy(Policies.Approver, p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager), nameof(UserRole.Manager)));

// Slows password guessing: by default 10 sign-in or registration attempts per minute per IP.
var authPerMinute = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // Public job applications: a few per visitor every 10 minutes.
    o.AddPolicy("careers", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Qaventra API"));
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();

// In production the built web app is served from wwwroot by this same site.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RequestAuditMiddleware>();
app.UseMiddleware<TenantAccessMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Client-side routes (/dashboard, /employees/...) load the web app; unknown /api paths stay 404.
app.MapFallbackToFile("index.html").AllowAnonymous();
app.MapFallback("/api/{**rest}", () => Results.NotFound()).AllowAnonymous();

await SeedSuperAdminAsync(app);
app.Run();

// Creates the platform SuperAdmin from configuration on first start. Change the password after the first sign-in.
static async Task SeedSuperAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
        await db.Database.MigrateAsync();

    var email = app.Configuration["SuperAdmin:Email"]?.Trim().ToLowerInvariant();
    var password = app.Configuration["SuperAdmin:Password"];
    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        return;
    if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.SuperAdmin))
        return;

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var user = new User { TenantId = Guid.Empty, Email = email, FullName = "Super Admin", Role = UserRole.SuperAdmin };
    user.PasswordHash = hasher.HashPassword(user, password);
    db.Users.Add(user);
    await db.SaveChangesAsync();
}

public partial class Program;
