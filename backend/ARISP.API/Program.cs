using System;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using ARISP.API.Hubs;
using ARISP.API.Middleware;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Constants;
using ARISP.Infrastructure.Data;
using ARISP.Infrastructure.Repositories;
using ARISP.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
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

// Database connection mapping — đọc từ user-secrets / env var, không hardcode
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") is { Length: > 0 } cs ? cs :
    Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") is { Length: > 0 } env ? env :
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection chưa được cấu hình.\n" +
        "Chạy: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<connection-string>\"");

// Bật connection pooling + giữ pool ấm: kết nối tới Supabase ở xa nên mỗi lần mở mới
// phải TLS + auth (~1–1.5s). Pooling cho tái dùng connection → nhanh hơn nhiều và tránh
// timeout auth lúc cao điểm. Override các tham số pool nhưng giữ nguyên host/credential.
var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
{
    Pooling = true,
    MinPoolSize = 2,            // giữ sẵn vài connection ấm
    MaxPoolSize = 20,
    ConnectionIdleLifetime = 300,
    KeepAlive = 30,            // ping giữ connection sống qua idle timeout của Supabase/NAT
    Timeout = 30,             // timeout mở connection (giây)
    CommandTimeout = 60,      // timeout thực thi lệnh (giây)
};
connectionString = csb.ConnectionString;

builder.Services.AddDbContext<ARISPDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsHistoryTable("ef_migrations_history");
        // Tự thử lại khi gặp lỗi mạng/thoáng qua (vd timeout auth tới Supabase).
        npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
    }));

// DI configurations
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// AI services mappings (OpenAI implements both Provider and Embedding)
builder.Services.AddScoped<OpenAIProvider>();
builder.Services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OpenAIProvider>());
builder.Services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OpenAIProvider>());

builder.Services.AddScoped<IGeminiProvider, ARISP.Infrastructure.AI.GeminiProvider>();

// File storage — Local (dev) hoặc S3-compatible object storage như Cloudflare R2 (prod).
// Chọn qua "Storage:Provider" = "Local" | "S3". Mặc định Local.
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
{
    var s3Options = new ARISP.Infrastructure.Storage.S3StorageOptions();
    builder.Configuration.GetSection("Storage:S3").Bind(s3Options);

    if (string.IsNullOrWhiteSpace(s3Options.Endpoint) ||
        string.IsNullOrWhiteSpace(s3Options.AccessKeyId) ||
        string.IsNullOrWhiteSpace(s3Options.SecretAccessKey) ||
        string.IsNullOrWhiteSpace(s3Options.Bucket))
    {
        throw new InvalidOperationException(
            "Storage:Provider=S3 nhưng thiếu cấu hình Storage:S3 (Endpoint/AccessKeyId/SecretAccessKey/Bucket). " +
            "Cấu hình qua user-secrets hoặc biến môi trường.");
    }

    builder.Services.AddSingleton(s3Options);
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
    {
        var config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = s3Options.Endpoint,
            ForcePathStyle = true,            // R2/MinIO ưu tiên path-style
            AuthenticationRegion = s3Options.Region
        };
        var creds = new Amazon.Runtime.BasicAWSCredentials(s3Options.AccessKeyId, s3Options.SecretAccessKey);
        return new Amazon.S3.AmazonS3Client(creds, config);
    });
    builder.Services.AddScoped<IFileStorageService, ARISP.Infrastructure.Storage.S3FileStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, ARISP.Infrastructure.Storage.LocalFileStorageService>();
}

// STT, TTS, Avatar, Notification mock stubs
builder.Services.AddScoped<ISTTProvider, MockSTTProvider>();
builder.Services.AddScoped<ITTSService, MockTTSService>();
builder.Services.AddScoped<IAvatarService, MockAvatarService>();
builder.Services.AddScoped<INotificationService, MockNotificationService>();
builder.Services.AddScoped<IDocumentParserService, DocumentParserService>();
builder.Services.AddScoped<IJdStampService, ARISP.Infrastructure.Documents.JdStampService>();

builder.Services.AddTransient<IEmailService, EmailService>();

// Hàng đợi email nền — enqueue không chặn request, BackgroundService gửi SMTP ở luồng nền
builder.Services.AddSingleton<IEmailQueue, EmailBackgroundQueue>();
builder.Services.AddHostedService<EmailQueueHostedService>();

// Application Services
builder.Services.AddScoped<PlaybookService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<CvJdAnalysisService>();
builder.Services.AddScoped<InterviewService>();
builder.Services.AddScoped<InterviewCodeService>();
builder.Services.AddScoped<EvaluationService>();

// NOTE: In .NET 8, JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear() has no effect
// because AddJwtBearer uses JsonWebTokenHandler by default. Use MapInboundClaims = false instead.

// Configure JWT Authentication and external SSO — đọc từ user-secrets / env var, không hardcode
var jwtSecret =
    builder.Configuration["JWT:Secret"] is { Length: > 0 } s ? s :
    Environment.GetEnvironmentVariable("JWT_SECRET") is { Length: > 0 } envJwt ? envJwt :
    throw new InvalidOperationException(
        "JWT:Secret chưa được cấu hình.\n" +
        "Chạy: dotnet user-secrets set \"JWT:Secret\" \"<secret-key>\"");

// Google OAuth credentials — đọc từ user-secrets / env var, không hardcode.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
var googleAuthConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleSecret);

// Production/Staging: BẮT BUỘC có credentials thật — fail-fast thay vì âm thầm chạy bằng giá trị giả
// (giá trị giả khiến app boot OK nhưng Google login fail runtime với lỗi khó debug).
if (!googleAuthConfigured && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Authentication:Google:ClientId/ClientSecret chưa được cấu hình.\n" +
        "Bắt buộc ở môi trường non-Development.\n" +
        "Chạy: dotnet user-secrets set \"Authentication:Google:ClientId\" \"<client-id>\"\n" +
        "      dotnet user-secrets set \"Authentication:Google:ClientSecret\" \"<client-secret>\"");
}

var authBuilder = builder.Services.AddAuthentication(options =>
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
// External cookie to receive external provider claims
.AddCookie("External", options =>
{
    options.Cookie.Name = "ARISP.External";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Secure flag when HTTPS
    // Lax works for OAuth redirect (top-level GET) and does NOT require the Secure flag,
    // so it functions over plain HTTP in local dev. (SameSite=None would be dropped on HTTP.)
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
});

// Google OAuth2 — dùng chung cho Candidate self-service Sign-In và HR internal SSO.
// Chỉ đăng ký provider khi đã có credentials thật:
//   - Production/Staging: thiếu credentials đã fail-fast ở trên.
//   - Development: thiếu credentials → bỏ qua provider + cảnh báo (tắt mềm), KHÔNG nhồi giá trị giả.
if (googleAuthConfigured)
{
    authBuilder.AddGoogle("Google", options =>
    {
        options.SignInScheme = "External";
        options.CallbackPath = "/api/auth/external/google-callback";

        // Correlation cookie defaults to SameSite=None, which the browser drops over plain HTTP.
        // Use Lax so the OAuth round-trip works in local dev without HTTPS.
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.ClientId = googleClientId!;
        options.ClientSecret = googleSecret!;
    });
}
else
{
    Log.Warning("Google OAuth provider DISABLED — Authentication:Google:ClientId/ClientSecret chưa cấu hình. " +
                "Google Sign-In (Candidate & HR) sẽ không khả dụng. Chỉ chấp nhận ở môi trường Development.");
}

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

var allowedOrigins = (builder.Configuration["Authentication:AdminFrontendUrl"] ?? "https://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Concat(new[] { "http://127.0.0.1:5500", "http://localhost:5500", "https://localhost:5001", "https://localhost:3000" })
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Trust X-Forwarded-Proto / X-Forwarded-For from Nginx reverse proxy.
// Without this, ASP.NET Core sees http:// internally and generates wrong OAuth redirect_uri.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Docker internal network — clear default network restrictions so Nginx container is trusted
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || true) // always enable swagger for prototype ease of test
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must be first — rewrites Request.Scheme/Host before any middleware reads them
app.UseForwardedHeaders();

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

// Auto database migration on startup — thử lại vài lần vì kết nối Supabase đôi khi
// chậm/timeout auth lúc khởi động (lỗi thoáng qua, không phải sai migration).
for (var attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ARISPDbContext>();
        Console.WriteLine($"Applying EF Core migrations (attempt {attempt}/3)...");
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied.");
        break;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply migrations (attempt {Attempt}/3).", attempt);
        if (attempt < 3)
            await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

// Đảm bảo index cho các cột lọc nóng (idempotent, CREATE INDEX IF NOT EXISTS). InitialCreate
// gần như không tạo index FK → mọi truy vấn theo candidate/application bị seq scan (gây timeout).
// Biến seq scan → index seek. Mỗi câu chạy riêng để 1 lỗi không chặn các index còn lại.
try
{
    using var idxScope = app.Services.CreateScope();
    var db = idxScope.ServiceProvider.GetRequiredService<ARISPDbContext>();
    var indexStatements = new[]
    {
        "CREATE INDEX IF NOT EXISTS ix_applications_candidate_account_id ON applications (candidate_account_id)",
        "CREATE INDEX IF NOT EXISTS ix_applications_candidate_email ON applications (candidate_email)",
        "CREATE INDEX IF NOT EXISTS ix_applications_job_posting_id ON applications (job_posting_id)",
        "CREATE INDEX IF NOT EXISTS ix_applications_cv_jd_analysis_id ON applications (cv_jd_analysis_id)",
        "CREATE INDEX IF NOT EXISTS ix_notifications_candidate_account_id ON notifications (candidate_account_id)",
        "CREATE INDEX IF NOT EXISTS ix_saved_jobs_candidate_account_id ON saved_jobs (candidate_account_id)",
        "CREATE INDEX IF NOT EXISTS ix_interview_sessions_application_id ON interview_sessions (application_id)",
        "CREATE INDEX IF NOT EXISTS ix_evaluations_session_id ON evaluations (session_id)",
        "CREATE INDEX IF NOT EXISTS ix_evaluations_application_id ON evaluations (application_id)",
        "CREATE INDEX IF NOT EXISTS ix_hr_reviews_evaluation_id ON hr_reviews (evaluation_id)",
        "CREATE INDEX IF NOT EXISTS ix_interview_codes_application_id ON interview_codes (application_id)",
        "CREATE INDEX IF NOT EXISTS ix_interview_bookings_application_id ON interview_bookings (application_id)",
        "CREATE INDEX IF NOT EXISTS ix_job_postings_created_by_user_id ON job_postings (created_by_user_id)",
    };
    foreach (var stmt in indexStatements)
    {
        try { await db.Database.ExecuteSqlRawAsync(stmt); }
        catch (Exception exIdx) { Log.Warning(exIdx, "Could not create index: {Stmt}", stmt); }
    }
    Console.WriteLine("Performance indexes ensured.");

    // Cột phê duyệt HR Leader + file JD đã đóng dấu (idempotent ADD COLUMN IF NOT EXISTS).
    var columnStatements = new[]
    {
        "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_by_user_id uuid",
        "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approved_at timestamptz",
        "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS approver_name text",
        "ALTER TABLE job_postings ADD COLUMN IF NOT EXISTS signed_jd_file_url text",
    };
    foreach (var stmt in columnStatements)
    {
        try { await db.Database.ExecuteSqlRawAsync(stmt); }
        catch (Exception exCol) { Log.Warning(exCol, "Could not add column: {Stmt}", stmt); }
    }
    Console.WriteLine("Job approval columns ensured.");

    // Bảng lời mời phỏng vấn theo vòng (InterviewInvite) — idempotent CREATE TABLE IF NOT EXISTS.
    var tableStatements = new[]
    {
        @"CREATE TABLE IF NOT EXISTS interview_invites (
            id uuid PRIMARY KEY,
            application_id uuid NOT NULL,
            round_number integer NOT NULL DEFAULT 1,
            token_hash text NOT NULL,
            expires_at timestamptz NOT NULL,
            scheduled_at timestamptz NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        )",
        "CREATE INDEX IF NOT EXISTS ix_interview_invites_application_id ON interview_invites (application_id)",
        "CREATE INDEX IF NOT EXISTS ix_interview_invites_token_hash ON interview_invites (token_hash)",
        // Chống đặt trùng: tối đa 1 booking 'scheduled' / (hồ sơ, vòng).
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_interview_bookings_app_round_scheduled ON interview_bookings (application_id, round_number) WHERE status = 'scheduled'",
    };
    foreach (var stmt in tableStatements)
    {
        try { await db.Database.ExecuteSqlRawAsync(stmt); }
        catch (Exception exTbl) { Log.Warning(exTbl, "Could not ensure table/index: {Stmt}", stmt); }
    }
    Console.WriteLine("Interview invite table ensured.");
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not ensure performance indexes / approval columns.");
}

app.Run();
