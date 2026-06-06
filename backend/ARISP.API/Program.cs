using System;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using ARISP.API.Hubs;
using ARISP.API.Middleware;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Constants;
using ARISP.Domain.Entities;
using ARISP.Infrastructure.Data;
using ARISP.Infrastructure.Repositories;
using ARISP.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/arisp_api_log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ARISP API", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer eyJhbGciOi...'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header
            },
            new List<string>()
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// Database connection mapping
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? "Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.mwdfddlmkfdmzdckfpgx;Password=d5fzV3?WQfZz9wA;SSL Mode=Require;Trust Server Certificate=true"; // Fallback to Test DB

builder.Services.AddDbContext<ARISPDbContext>(options =>
    options.UseNpgsql(connectionString)); // Simplified standard connection

// DI configurations
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// AI services mappings (OpenAI implements both Provider and Embedding)
builder.Services.AddScoped<OpenAIProvider>();
builder.Services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OpenAIProvider>());
builder.Services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OpenAIProvider>());

// STT, TTS, Avatar, Notification mock stubs
builder.Services.AddScoped<ISTTProvider, MockSTTProvider>();
builder.Services.AddScoped<ITTSService, MockTTSService>();
builder.Services.AddScoped<IAvatarService, MockAvatarService>();
builder.Services.AddScoped<INotificationService, MockNotificationService>();

builder.Services.AddTransient<IEmailService, EmailService>();

// Application Services
builder.Services.AddScoped<PlaybookService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<InterviewService>();
builder.Services.AddScoped<InterviewCodeService>();

// NOTE: In .NET 8, JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear() has no effect
// because AddJwtBearer uses JsonWebTokenHandler by default. Use MapInboundClaims = false instead.

// Configure JWT Authentication and external SSO
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "ARISP_SUPER_SECRET_JWT_KEY_MINIMUM_256_BITS_FOR_SECURITY";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Prevent mapping JWT short claim names (sub, role, email) to long XML namespace URIs
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"] ?? "ARISP",
        ValidAudience = builder.Configuration["JWT:Audience"] ?? "ARISP_Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = "role", // Map role claim using standard short name
        NameClaimType = "sub"  // Map name/ID claim using standard short name
    };
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
            logger.LogError(context.Exception, "JWT Auth Failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
            var claims = context.Principal?.Claims?.Select(c => $"{c.Type}={c.Value}") ?? Enumerable.Empty<string>();
            logger.LogInformation("JWT Token Validated. Claims: [{Claims}]", string.Join(", ", claims));
            return Task.CompletedTask;
        }
    };
})
.AddJwtBearer("Firebase", options =>
{
    var firebaseProjectId = builder.Configuration["Authentication:Firebase:ProjectId"]
        ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
        ?? "arisp-auth-service";

    options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
        ValidateAudience = true,
        ValidAudience = firebaseProjectId,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };
})
// External cookie to receive external provider claims
.AddCookie("External", options =>
{
    options.Cookie.Name = "ARISP.External";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
})
// Google OAuth2 (used for HR internal SSO)
.AddGoogle("Google", options =>
{
    options.SignInScheme = "External";
    options.CallbackPath = "/api/auth/external/google-callback";

    var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
    var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

    // 🛡️ Nếu trống, gán chuỗi Mock để tránh crash pipeline khi chạy Local/Swagger
    options.ClientId = string.IsNullOrEmpty(googleClientId) ? "MOCK_GOOGLE_CLIENT_ID_FOR_LOCAL" : googleClientId;
    options.ClientSecret = string.IsNullOrEmpty(googleSecret) ? "MOCK_GOOGLE_SECRET_FOR_LOCAL" : googleSecret;
});
//// Azure AD / Microsoft Entra (OpenID Connect)
//.AddOpenIdConnect("AzureAD", options =>
//{
//    options.SignInScheme = "External";

//    var azureAuthority = builder.Configuration["Authentication:AzureAd:Authority"] ?? Environment.GetEnvironmentVariable("AZURE_AD_AUTHORITY");
//    var azureClientId = builder.Configuration["Authentication:AzureAd:ClientId"] ?? Environment.GetEnvironmentVariable("AZURE_AD_CLIENT_ID");
//    var azureSecret = builder.Configuration["Authentication:AzureAd:ClientSecret"] ?? Environment.GetEnvironmentVariable("AZURE_AD_CLIENT_SECRET");

//    options.Authority = string.IsNullOrEmpty(azureAuthority) ? "https://login.microsoftonline.com/common/v2.0" : azureAuthority;
//    options.ClientId = string.IsNullOrEmpty(azureClientId) ? "00000000-0000-0000-0000-000000000000" : azureClientId;
//    options.ClientSecret = string.IsNullOrEmpty(azureSecret) ? "MOCK_AZURE_SECRET_FOR_LOCAL" : azureSecret;

//    options.ResponseType = "code";
//    options.SaveTokens = true;
//});

builder.Services.AddAuthorization(options =>
{
    // 1. Chính sách dành riêng cho cấp quản trị tối cao
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole(AppRoles.SuperAdmin));

    // 2. Chính sách dành cho quản lý nhân sự trở lên (Bao gồm cả SuperAdmin và HR Admin)
    options.AddPolicy("HrManagement", policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin));

    // 3. Chính sách dành cho toàn bộ nhân viên nội bộ có quyền vào hệ thống quản lý chuyên môn
    options.AddPolicy("InternalStaff", policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin, AppRoles.Recruiter));

    // 4. Chính sách biệt lập dành riêng cho Ứng viên
    options.AddPolicy("CandidateOnly", policy =>
        policy.RequireRole(AppRoles.Candidate));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || true) // always enable swagger for prototype ease of test
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseStaticFiles();

// Ensure local uploads directory exists and configure to serve static CV files
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");
app.MapHub<WebRTCSignalingHub>("/hubs/webrtc");

// Auto database migration & Seed Data execution
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ARISPDbContext>();
    Console.WriteLine("Applying migrations/checks...");
    
    // Check/create extensions and seed schema tables if needed
    // In Supabase, the public schema was already initialized completely via apply_schema.js, so we only need to perform Seeding!
    await SeedDataAsync(dbContext);
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to apply database seeding.");
}

app.Run();

async Task SeedDataAsync(ARISPDbContext db)
{
    Console.WriteLine("Seeding ARISP prototype databases...");

    var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var jobId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    var candidateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    var appId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // 1. HR User
    var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
    {
        user = new User
        {
            Id = userId,
            Email = "hr@arisp.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = AppRoles.HrAdmin,
            FullName = "Alex HR Admin",
            Department = "IT Recruitment",
            IsActive = true
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
    }

    // 2. Job Posting
    var job = await db.JobPostings.IgnoreQueryFilters().FirstOrDefaultAsync(j => j.Id == jobId);
    if (job == null)
    {
        job = new JobPosting { Id = jobId };
        await db.JobPostings.AddAsync(job);
    }

    job.CreatedByUserId = userId;
    job.Title = "Senior Backend Engineer (.NET & AI)";
    job.Department = "IT Engineering";
    job.JobDescription = "We are looking for a Senior .NET Backend Engineer to build AI-driven recruiting services. Requires 5+ years of C# ASP.NET Core experience, Postgres database optimizations, and integrating with OpenAI APIs. English speaking capability is a plus.";
    job.InterviewMode = "both";
    job.Status = "active";
    job.IsPublicListing = true;
    job.DetectedLanguage = "en";
    job.LanguageRequirement = "TOEIC > 700 hoặc IELTS > 6.5";
    job.LanguageConfirmed = true;
    job.Location = "Ho Chi Minh City";
    job.WorkMode = "hybrid";
    job.SalaryMin = 2000;
    job.SalaryMax = 3500;
    job.SalaryCurrency = "USD";
    job.SalaryIsNegotiable = false;
    job.EmploymentType = "full_time";
    job.ExperienceLevel = "senior";
    job.Skills = new System.Collections.Generic.List<string> { "C#", ".NET Core", "PostgreSQL", "OpenAI" };
    job.JobCategory = "Backend";
    job.ApplicationDeadline = DateTimeOffset.UtcNow.AddDays(30);
    job.IsUrgent = true;
    job.ScoringRubric = "[{\"criterion\":\"C# Knowledge\",\"weight\":40},{\"criterion\":\"System Design\",\"weight\":30},{\"criterion\":\"Communication\",\"weight\":30}]";

    await db.SaveChangesAsync();

    // Round Config
    var config = await db.InterviewRoundConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.JobPostingId == jobId && r.RoundNumber == 1);
    if (config == null)
    {
        config = new InterviewRoundConfig
        {
            JobPostingId = jobId,
            RoundNumber = 1
        };
        await db.InterviewRoundConfigs.AddAsync(config);
    }
    config.RoundType = "screening";
    config.MaxDurationMinutes = 30;
    await db.SaveChangesAsync();

    // 3. Candidate Account
    var candidate = await db.CandidateAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == candidateId);
    if (candidate == null)
    {
        candidate = new CandidateAccount
        {
            Id = candidateId,
            Email = "candidate@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            FullName = "John Doe Candidate",
            Phone = "0987654321",
            Headline = "C# .NET Backend Developer | AI Enthusiast"
        };
        await db.CandidateAccounts.AddAsync(candidate);
        await db.SaveChangesAsync();
    }

    // 4. Application
    var appRecord = await db.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == appId);
    if (appRecord == null)
    {
        appRecord = new Application
        {
            Id = appId,
            JobPostingId = jobId,
            CandidateAccountId = candidateId,
            CandidateEmail = "candidate@example.com",
            CandidateName = "John Doe Candidate",
            CandidatePhone = "0987654321",
            Source = "job_board",
            Status = "cv_submitted",
            CvText = "John Doe is a C# .NET Backend developer with 4 years experience optimizing SQL queries and developing scalable Web APIs."
        };
        await db.Applications.AddAsync(appRecord);
        await db.SaveChangesAsync();
    }

    Console.WriteLine("Database seeding completed successfully!");
}
