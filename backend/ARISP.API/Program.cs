using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ARISP.API.Hubs;
using ARISP.API.Middleware;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Entities;
using ARISP.Infrastructure.Data;
using ARISP.Infrastructure.Repositories;
using ARISP.Infrastructure.Services;

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

// Application Services
builder.Services.AddScoped<PlaybookService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<InterviewService>();

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "ARISP_SUPER_SECRET_JWT_KEY_MINIMUM_256_BITS_FOR_SECURITY";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"] ?? "ARISP",
        ValidAudience = builder.Configuration["JWT:Audience"] ?? "ARISP_Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
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

    var orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var jobId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    var candidateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    var appId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // 1. Organization
    var org = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == orgId);
    if (org == null)
    {
        org = new Organization
        {
            Id = orgId,
            Name = "ARISP Enterprise Ltd",
            Slug = "arisp-enterprise",
            Plan = "professional",
            IsActive = true
        };
        await db.Organizations.AddAsync(org);
        await db.SaveChangesAsync();
    }

    // 2. HR User
    var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
    {
        user = new User
        {
            Id = userId,
            OrganizationId = orgId,
            Email = "hr@arisp.com",
            PasswordHash = "password", // simple for prototype
            Role = "hr_admin",
            FullName = "Alex HR Admin",
            Department = "IT Recruitment",
            IsActive = true
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
    }

    // 3. Job Posting
    var job = await db.JobPostings.IgnoreQueryFilters().FirstOrDefaultAsync(j => j.Id == jobId);
    if (job == null)
    {
        job = new JobPosting
        {
            Id = jobId,
            OrganizationId = orgId,
            CreatedByUserId = userId,
            Title = "Senior Backend Engineer (.NET & AI)",
            Department = "IT Engineering",
            JobDescription = "We are looking for a Senior .NET Backend Engineer to build AI-driven recruiting services. Requires 5+ years of C# ASP.NET Core experience, Postgres database optimizations, and integrating with OpenAI APIs. English speaking capability is a plus.",
            InterviewMode = "both",
            Status = "active",
            IsPublicListing = true,
            DetectedLanguage = "en",
            LanguageRequirement = "TOEIC > 700 hoặc IELTS > 6.5",
            LanguageConfirmed = true
        };
        await db.JobPostings.AddAsync(job);
        await db.SaveChangesAsync();

        // Round Config
        var config = new InterviewRoundConfig
        {
            JobPostingId = jobId,
            RoundNumber = 1,
            RoundType = "screening",
            MaxDurationMinutes = 30
        };
        await db.InterviewRoundConfigs.AddAsync(config);
        await db.SaveChangesAsync();
    }

    // 4. Candidate Account
    var candidate = await db.CandidateAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == candidateId);
    if (candidate == null)
    {
        candidate = new CandidateAccount
        {
            Id = candidateId,
            Email = "candidate@example.com",
            PasswordHash = "password",
            FullName = "John Doe Candidate",
            Phone = "0987654321",
            Headline = "C# .NET Backend Developer | AI Enthusiast"
        };
        await db.CandidateAccounts.AddAsync(candidate);
        await db.SaveChangesAsync();
    }

    // 5. Application
    var appRecord = await db.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == appId);
    if (appRecord == null)
    {
        appRecord = new Application
        {
            Id = appId,
            OrganizationId = orgId,
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
