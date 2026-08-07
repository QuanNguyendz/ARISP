using System;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using ARI.Infrastructure.AI;
using ARI.Infrastructure.Data;
using ARI.Infrastructure.Repositories;
using ARI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database connection mapping — đọc từ user-secrets / env var, không hardcode
            var connectionString =
                configuration.GetConnectionString("DefaultConnection") is { Length: > 0 } cs ? cs :
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

            services.AddDbContext<AriDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("ef_migrations_history");
                    // Tự thử lại khi gặp lỗi mạng/thoáng qua (vd timeout auth tới Supabase).
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                }));

            services.AddScoped<AriDbContextInitialiser>();

            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Identity helpers — mint JWT + hash mật khẩu (tách khỏi controllers, ADR refactor CQRS).
            services.AddScoped<ITokenService, Identity.JwtTokenService>();
            services.AddSingleton<IPasswordHasher, Identity.BcryptPasswordHasher>();

            // AI provider switch (ADR-039): "rag" -> microservice Python (RagServiceProvider);
            // "openai" | "local" -> OpenAIProvider in-process (fallback, không khoá cứng vào Python).
            var aiProvider = configuration["AI:Provider"]
                ?? Environment.GetEnvironmentVariable("AI_PROVIDER")
                ?? "openai";

            if (string.Equals(aiProvider, "rag", StringComparison.OrdinalIgnoreCase))
            {
                var ragServiceUrl = configuration["RagService:Url"]
                    ?? Environment.GetEnvironmentVariable("RAG_SERVICE_URL")
                    ?? "http://rag-service:8000";

                // Typed HttpClient tới RAG service nội bộ (không qua Nginx).
                services.AddHttpClient<RagServiceProvider>(c =>
                {
                    c.BaseAddress = new Uri(ragServiceUrl);
                    c.Timeout = TimeSpan.FromSeconds(120); // sinh câu hỏi/đánh giá có thể lâu
                });

                services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<RagServiceProvider>());
                services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<RagServiceProvider>());
                services.AddScoped<IRagIngestionService>(sp => sp.GetRequiredService<RagServiceProvider>());
            }
            else
            {
                // OpenAIProvider implements cả IAIProvider lẫn IEmbeddingProvider.
                services.AddScoped<OpenAIProvider>();
                services.AddScoped<IAIProvider>(sp => sp.GetRequiredService<OpenAIProvider>());
                services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<OpenAIProvider>());
                // Ingestion chạy trong tiến trình (chunk+embed+INSERT) khi không dùng RAG service.
                services.AddScoped<IRagIngestionService, LocalRagIngestionService>();
            }

            services.AddScoped<IGeminiProvider, GeminiProvider>();

            // File storage — Local (dev) hoặc S3-compatible object storage như Cloudflare R2 (prod).
            // Chọn qua "Storage:Provider" = "Local" | "S3". Mặc định Local.
            var storageProvider = configuration["Storage:Provider"] ?? "Local";
            if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
            {
                var s3Options = new Storage.S3StorageOptions();
                configuration.GetSection("Storage:S3").Bind(s3Options);

                if (string.IsNullOrWhiteSpace(s3Options.Endpoint) ||
                    string.IsNullOrWhiteSpace(s3Options.AccessKeyId) ||
                    string.IsNullOrWhiteSpace(s3Options.SecretAccessKey) ||
                    string.IsNullOrWhiteSpace(s3Options.Bucket))
                {
                    throw new InvalidOperationException(
                        "Storage:Provider=S3 nhưng thiếu cấu hình Storage:S3 (Endpoint/AccessKeyId/SecretAccessKey/Bucket). " +
                        "Cấu hình qua user-secrets hoặc biến môi trường.");
                }

                services.AddSingleton(s3Options);
                services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
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
                services.AddScoped<IFileStorageService, Storage.S3FileStorageService>();
            }
            else
            {
                services.AddScoped<IFileStorageService, Storage.LocalFileStorageService>();
            }

            // === Media stack phỏng vấn realtime (ADR-043/044): real provider nếu có API key, else Mock ===
            var mediaOptions = new Media.MediaOptions();
            configuration.GetSection("Media").Bind(mediaOptions);
            services.AddSingleton(mediaOptions);

            // STT (Deepgram): BE mint ephemeral token cho FE live STT.
            services.AddScoped<ISTTProvider, MockSTTProvider>(); // server-side STT chưa dùng (FE stream trực tiếp)
            if (!string.IsNullOrWhiteSpace(mediaOptions.Deepgram.ApiKey))
                services.AddHttpClient<IDeepgramTokenService, Media.DeepgramTokenService>();
            else
                services.AddScoped<IDeepgramTokenService, MockDeepgramTokenService>();

            // TTS (ElevenLabs Flash v2.5).
            if (!string.IsNullOrWhiteSpace(mediaOptions.ElevenLabs.ApiKey))
                services.AddHttpClient<ITTSService, Media.ElevenLabsTTSService>();
            else
                services.AddScoped<ITTSService, MockTTSService>();

            // Avatar (HeyGen Streaming): BE mint session token; FE chạy @heygen/streaming-avatar.
            if (!string.IsNullOrWhiteSpace(mediaOptions.HeyGen.ApiKey))
                services.AddHttpClient<IAvatarService, Media.HeyGenAvatarService>();
            else
                services.AddScoped<IAvatarService, MockAvatarService>();

            services.AddScoped<IDocumentParserService, DocumentParserService>();
            services.AddScoped<IJdStampService, Documents.JdStampService>();

            services.AddTransient<IEmailService, EmailService>();

            // Hàng đợi email nền — enqueue không chặn request, BackgroundService gửi SMTP ở luồng nền
            services.AddSingleton<IEmailQueue, EmailBackgroundQueue>();
            services.AddHostedService<EmailQueueHostedService>();

            // Dọn video phỏng vấn thật quá hạn lưu (ADR-052) — quét 12h/lần.
            services.AddHostedService<RecordingRetentionHostedService>();

            // Auto-reject lịch ứng viên không xác nhận trong thời hạn (ADR-048) — quét 30'/lần.
            services.AddHostedService<ScheduleConfirmationHostedService>();

            return services;
        }
    }
}
