using System;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using Microsoft.Extensions.Caching.Memory;
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
        => new(uow, rag, email, notif, new TestScopeFactory(uow), new MemoryCache(new MemoryCacheOptions()));

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
