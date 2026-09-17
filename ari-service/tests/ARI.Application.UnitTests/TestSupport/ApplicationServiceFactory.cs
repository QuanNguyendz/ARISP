using System;
using System.Collections.Generic;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Dựng <see cref="ApplicationService"/> cho unit test luồng Application. <c>IServiceScopeFactory</c> chỉ
/// dùng cho tác vụ nền phân tích CV (fire-and-forget) — test luôn chọn input né nhánh đó nên cắm stub ném lỗi.
/// </summary>
internal static class ApplicationServiceFactory
{
    public static ApplicationService Create(
        IUnitOfWork uow, INotificationService notif, IEmailService email, IRagIngestionService rag)
        => Create(uow, notif, email, rag, new ARI.Application.UnitTests.CvScoring.RecordingCvScoringQueue());

    public static ApplicationService Create(
        IUnitOfWork uow, INotificationService notif, IEmailService email, IRagIngestionService rag, ICvScoringQueue queue)
        => Create(uow, notif, email, rag, queue, new ARI.Application.CvScoring.CvScoringInFlight());

    public static ApplicationService Create(
        IUnitOfWork uow, INotificationService notif, IEmailService email, IRagIngestionService rag, ICvScoringQueue queue,
        ARI.Application.CvScoring.CvScoringInFlight inFlight)
        => new(uow, rag, email, notif, new TestScopeFactory(uow), new MemoryCache(new MemoryCacheOptions()), Configuration(), queue, inFlight);

    /// <summary>Chỉ cần base URL portal để link trong email không trỏ về máy dev.</summary>
    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:CandidateBaseUrl"] = "https://arisp.test",
            })
            .Build();

    private sealed class TestScopeFactory : IServiceScopeFactory, IServiceScope
    {
        private readonly IServiceProvider _provider;
        public TestScopeFactory(IUnitOfWork uow)
        {
            var services = new ServiceCollection();
            services.AddSingleton(uow);
            _provider = services.BuildServiceProvider();
        }
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => _provider;
        public void Dispose() { }
    }
}
