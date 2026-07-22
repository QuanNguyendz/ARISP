using System;
using System.Text;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace ARI.API
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddWebServices(this WebApplicationBuilder builder)
        {
            // Bắt lỗi DI (thiếu registration, sai lifetime) ngay lúc boot thay vì request đầu tiên.
            if (builder.Environment.IsDevelopment())
            {
                builder.Host.UseDefaultServiceProvider(o =>
                {
                    o.ValidateScopes = true;
                    o.ValidateOnBuild = true;
                });
            }

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

            // Notification: SignalR thật (đẩy ReceiveQuestion/... tới SessionHub) + email qua IEmailService.
            builder.Services.AddScoped<INotificationService, Services.SignalRNotificationService>();

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
                    },
                    // Support SignalR authentication via token in query string
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
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

            // Hai origin FE tách biệt (ADR-046): StaffSite = AdminFrontendUrl, CandidateSite = Frontend:CandidateBaseUrl.
            // AllowCredentials() nên không dùng wildcard — phải liệt kê đủ mọi origin.
            var allowedOrigins = (builder.Configuration["Authentication:AdminFrontendUrl"] ?? "https://localhost:3000")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat((builder.Configuration["Frontend:CandidateBaseUrl"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Concat(new[] { "http://127.0.0.1:5500", "http://localhost:5500", "https://localhost:5001", "https://localhost:3000", "http://localhost:3000", "http://localhost:3001" })
                .Distinct()
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

            return builder;
        }
    }
}
