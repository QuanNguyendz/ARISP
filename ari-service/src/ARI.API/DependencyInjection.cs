using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Serialization;
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

            // ------------------------------------------------------------------------------------
            //  Gốc URL của hai site frontend — quyết định mọi đường dẫn đặt vào EMAIL.
            //
            //  Đây là loại sai cấu hình **không bao giờ tự lộ ra**: thiếu biến thì thư vẫn gửi bình thường,
            //  chỉ là nút bấm dẫn về máy của lập trình viên. Không lỗi, không log — tới khi ứng viên báo
            //  link hỏng thì thiệt hại đã xảy ra rồi. Nên:
            //    • Development: nạp sẵn localhost để chạy máy cá nhân không phải khai gì.
            //    • Mọi môi trường khác: **chặn ngay ở boot** nếu thiếu — cùng cách đang làm với `JWT:Secret`
            //      và Google OAuth. Container không lên là lỗi thấy ngay lúc triển khai, có người đang nhìn.
            //
            //  KHÔNG suy đoán URL từ request đang đến (`Host` header): thư còn được gửi từ hosted service
            //  chạy nền (nhắc lịch, đóng offer quá hạn) — lúc đó không có request nào để mà đoán.
            // ------------------------------------------------------------------------------------
            if (builder.Environment.IsDevelopment())
            {
                var devDefaults = new Dictionary<string, string?>();
                if (string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.CandidateKey]))
                    devDefaults[FrontendUrls.CandidateKey] = FrontendUrls.DevCandidate;
                if (string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.StaffKey])
                    && string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.LegacyStaffKey]))
                    devDefaults[FrontendUrls.StaffKey] = FrontendUrls.DevStaff;

                if (devDefaults.Count > 0) builder.Configuration.AddInMemoryCollection(devDefaults);
            }
            else
            {
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.CandidateKey]))
                    missing.Add($"{FrontendUrls.CandidateKey} (env: Frontend__CandidateBaseUrl)");
                if (string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.StaffKey])
                    && string.IsNullOrWhiteSpace(builder.Configuration[FrontendUrls.LegacyStaffKey]))
                    missing.Add($"{FrontendUrls.StaffKey} (env: Authentication__AdminFrontendUrl)");

                if (missing.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Chưa cấu hình gốc URL frontend, mà mọi link trong email đều dựng từ đó:\n"
                        + string.Join("\n", missing.Select(m => "  • " + m)) + "\n\n"
                        + "Bắt buộc ở môi trường non-Development. Khai trong `docker/.env` (xem `.env.example`).\n"
                        + "Cố tình để trống thì thư gửi đi sẽ mang link về localhost — và không có gì báo.");
                }
            }

            // Mọi mốc thời gian nhận từ client được chuẩn hoá về UTC ngay tại ranh giới JSON.
            // Npgsql chỉ ghi được offset 0 vào `timestamptz`, mà ô <input type="date"> gửi lên chuỗi
            // trần "2026-09-19" nên ASP.NET hiểu theo giờ local của máy chủ → UTC+7 → 500 lúc lưu.
            // Chốt ở đây thay vì bắt từng biểu mẫu nhớ gọi `toISOString()`.
            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
                o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeOffsetConverter());
            });

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
            builder.Services.AddMemoryCache(); // cache danh sách applications để tránh truy vấn DB mỗi request
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

                    // Ảnh đại diện Google: scope "profile" mặc định đã trả trường "picture" trong
                    // userinfo, nhưng handler KHÔNG map sẵn thành claim. Đọc thẳng JSON rồi tự thêm
                    // claim — cách này không phụ thuộc bảng ClaimActions mặc định của từng phiên bản
                    // package. ExternalCandidateCallback đọc claim này làm ảnh đại diện ban đầu.
                    options.Events.OnCreatingTicket = context =>
                    {
                        if (context.User.TryGetProperty("picture", out var pictureElement)
                            && pictureElement.ValueKind == JsonValueKind.String)
                        {
                            var pictureUrl = pictureElement.GetString();
                            if (!string.IsNullOrWhiteSpace(pictureUrl))
                                context.Identity?.AddClaim(new Claim("picture", pictureUrl));
                        }
                        return Task.CompletedTask;
                    };
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

                // 3. Chính sách dành cho toàn bộ nhân viên nội bộ có quyền vào hệ thống quản lý chuyên môn.
                //    Hiring Manager nằm ở đây, NHƯNG policy này chỉ mở CỬA: phạm vi dữ liệu thật do
                //    JobAccess quyết định theo từng tin (Phase 1 của ADR-061). Một HM chưa được gán
                //    tin nào sẽ nhận danh sách rỗng ở mọi endpoint — đó là lý do việc siết phạm vi
                //    phải xong TRƯỚC khi thêm vai trò này vào đây.
                options.AddPolicy("InternalStaff", policy =>
                    policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin, AppRoles.Recruiter, AppRoles.HiringManager));

                // 4. Chính sách biệt lập dành riêng cho Ứng viên
                options.AddPolicy("CandidateOnly", policy =>
                    policy.RequireRole(AppRoles.Candidate));

                // 5. Người đang NGỒI TRONG phòng phỏng vấn: ứng viên đăng nhập (phỏng vấn thử) hoặc
                //    máy Kiosk mang token phạm vi 1 phiên (phỏng vấn thật — ADR-052). Phạm vi phiên
                //    được kiểm tra thêm ở controller/hub qua claim session_id.
                options.AddPolicy("InterviewParticipant", policy =>
                    policy.RequireRole(AppRoles.Candidate, AppRoles.KioskSession));

                // 6. Người có quyền RA QUYẾT ĐỊNH TUYỂN trên một tin: duyệt shortlist, ký duyệt JD,
                //    chốt kết quả phỏng vấn, duyệt offer (ADR-061). Vẫn phải qua JobAccess để kiểm
                //    người gọi có thuộc đội tuyển dụng của đúng tin đó không — policy chỉ lọc thô
                //    theo vai trò, không biết gì về tài nguyên.
                //
                //    HrManagement KHÔNG đổi: Hiring Manager không tạo tài khoản, không duyệt tin
                //    lên "active", không lưu trữ tin.
                // Soạn/sửa tin tuyển dụng — CỐ Ý KHÔNG có Hiring Manager.
                //
                // ADR-061: HM quyết định chứ không vận hành. Nhưng khi thêm HM vào `InternalStaff`,
                // `POST /api/jobs` mở luôn cho họ — mà người tạo tin trở thành `CreatedByUserId`,
                // tức `JobAccessLevel.Owner` trên tin đó: từ đó họ xếp ca, duyệt/loại hồ sơ, gửi thư
                // cho ứng viên, thêm bất kỳ ai vào đội, tạo và gửi thư mời nhận việc. Lập luận
                // "phạm vi do JobAccess quyết định theo từng tin" chỉ đúng với endpoint thao tác
                // trên tin CÓ SẴN — tạo tin thì chưa có tài nguyên nào để giới hạn.
                options.AddPolicy("JobAuthoring", policy =>
                    policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin, AppRoles.Recruiter));

                // Quyết định CHUYÊN MÔN về ứng viên: duyệt shortlist, ký duyệt JD, chốt Pass/Not Pass.
                // Đây là việc của Hiring Manager — người hiểu công việc cần tuyển.
                options.AddPolicy("HiringDecision", policy =>
                    policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin, AppRoles.HiringManager));

                // ADR-063: quyết định về THƯ MỜI NHẬN VIỆC tách hẳn khỏi `HiringDecision`.
                //
                // Trước đây một policy duy nhất gác cả bốn cổng, nên Hiring Manager vừa đề xuất mức
                // lương vừa tự duyệt chính đề xuất đó. Quy trình nhân sự tách hai vai vì đúng lý do
                // này: người có nhu cầu tuyển không phải người kiểm soát ngân sách lương. HM vẫn
                // soạn và gửi duyệt thư mời (controller ở mức `InternalStaff`), chỉ nút CHỐT là của
                // HR Leader.
                options.AddPolicy("OfferApproval", policy =>
                    policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin));

                // Lập phiếu yêu cầu tuyển dụng: HM là người có nhu cầu. Admin lập được để vận hành
                // hộ, nhưng khi đó cũng không tự duyệt phiếu của mình (chặn theo NGƯỜI trong handler).
                // Recruiter cố ý KHÔNG có: họ thực thi phiếu, không phát sinh nhu cầu tuyển.
                // CHỈ Hiring Manager. Quản trị viên không lập hộ: người lập phiếu trở thành HM của tin
                // sinh ra từ phiếu (ADR-063), nên HR Leader lập phiếu là tự đặt mình vào cả hai đầu của
                // các cổng mà ADR-061/063 dựng lên để tách nhau.
                options.AddPolicy("RecruitmentRequestAuthoring", policy =>
                    policy.RequireRole(AppRoles.HiringManager));

                options.AddPolicy("RecruitmentRequestReview", policy =>
                    policy.RequireRole(AppRoles.SuperAdmin, AppRoles.HrAdmin));
            });

            // Hai origin FE tách biệt (ADR-046): StaffSite = AdminFrontendUrl, CandidateSite = Frontend:CandidateBaseUrl.
            // AllowCredentials() nên không dùng wildcard — phải liệt kê đủ mọi origin.
            //
            // Danh sách localhost chỉ mở Ở DEVELOPMENT. Trước đây nó được nối vào ở MỌI môi trường,
            // nghĩa là production chấp nhận request kèm cookie/token từ một trang chạy trên máy bất
            // kỳ — `AllowCredentials()` khiến điều đó thành một lỗ thật, không chỉ là rác cấu hình.
            var localDevOrigins = builder.Environment.IsDevelopment()
                ? new[]
                  {
                      "http://127.0.0.1:5500", "http://localhost:5500", "https://localhost:5001",
                      "https://localhost:3000", "http://localhost:3000", "http://localhost:3001",
                  }
                : Array.Empty<string>();

            var allowedOrigins = FrontendUrls.Staff(builder.Configuration)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(FrontendUrls.Candidate(builder.Configuration)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Concat(localDevOrigins)
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
