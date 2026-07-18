using ARI.Application.Options;
using ARI.Application.Services;
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

            // Application Services
            services.AddScoped<PlaybookService>();
            services.AddScoped<ApplicationService>();
            services.AddScoped<CvJdAnalysisService>();
            services.AddScoped<InterviewService>();
            services.AddScoped<InterviewCodeService>();
            services.AddScoped<EvaluationService>();

            return services;
        }
    }
}
