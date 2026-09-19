using System.Reflection;
using ARI.Application.Common.Behaviours;
using ARI.Application.Options;
using ARI.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        {
            // Cấu hình nghiệp vụ phỏng vấn (số lượt practice/vòng...) — Development đặt
            // PracticeAttemptsPerRound = 0 (không giới hạn) để test lặp lại không vướng gating.
            var interviewOptions = new InterviewOptions();
            configuration.GetSection("Interview").Bind(interviewOptions);
            services.AddSingleton(interviewOptions);

            // Cấu hình xếp lịch (hạn xác nhận lịch trước khi hệ thống tự Reject) — bind section "Scheduling".
            var schedulingOptions = new SchedulingOptions();
            configuration.GetSection("Scheduling").Bind(schedulingOptions);
            services.AddSingleton(schedulingOptions);

            // Realtime tầng database (ADR-057) — bind section "Realtime" (LISTEN/NOTIFY của Postgres).
            var realtimeOptions = new RealtimeOptions();
            configuration.GetSection("Realtime").Bind(realtimeOptions);
            services.AddSingleton(realtimeOptions);

            // CQRS: MediatR pipeline (thứ tự đăng ký = thứ tự chạy) + FluentValidation validators.
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
                cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
                cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
                cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            });

            // Application Services dùng chung (nhiều consumer hoặc hub gọi trực tiếp).
            // Service 1-consumer đang được absorb dần vào handlers theo từng wave CQRS.
            services.AddScoped<ApplicationService>();
            services.AddScoped<Interfaces.IApplicationService>(sp => sp.GetRequiredService<ApplicationService>());
            // Chấm CV theo bộ tiêu chí bắt buộc (ADR-070). Khoá "đang chấm" là singleton vì nó phải
            // chung cho mọi request và cho hàng đợi nền trong cùng tiến trình.
            services.AddSingleton<CvScoring.CvScoringInFlight>();
            services.AddScoped<CvScoring.ICvScoringService, CvScoring.CvScoringService>();
            services.AddScoped<CvScoring.CvRubricService>();
            services.AddScoped<CvScoring.CvApplicationScorer>();
            // Bộ chấm báo cáo phỏng vấn (ADR-073) — hàng đợi nền và đường chấm lại cùng gọi vào đây.
            services.AddScoped<Evaluations.InterviewEvaluator>();
            services.AddScoped<InterviewRubrics.InterviewRubricService>();
            services.AddScoped<InterviewService>();
            services.AddScoped<Interfaces.IInterviewService>(sp => sp.GetRequiredService<InterviewService>());
            services.AddScoped<InterviewCodeService>();
            services.AddScoped<Interfaces.IInterviewCodeService>(sp => sp.GetRequiredService<InterviewCodeService>());

            // Trình soạn thảo thư gửi ứng viên (ADR-061) — dựng bản xem trước bằng CHÍNH builder
            // mà lệnh gửi thật dùng, nên xem trước không thể lệch khỏi thư nhận được.
            services.AddScoped<Emails.IEmailTemplateRenderer, Emails.EmailTemplateRenderer>();

            return services;
        }
    }
}
