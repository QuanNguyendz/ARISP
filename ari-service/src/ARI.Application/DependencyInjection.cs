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
            services.AddScoped<CvJdAnalysisService>();
            services.AddScoped<Interfaces.ICvJdAnalysisService>(sp => sp.GetRequiredService<CvJdAnalysisService>());
            services.AddScoped<InterviewService>();
            services.AddScoped<Interfaces.IInterviewService>(sp => sp.GetRequiredService<InterviewService>());
            services.AddScoped<InterviewCodeService>();
            services.AddScoped<Interfaces.IInterviewCodeService>(sp => sp.GetRequiredService<InterviewCodeService>());

            return services;
        }
    }
}
