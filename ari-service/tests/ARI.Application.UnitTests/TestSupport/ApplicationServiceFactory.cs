using System;
using ARI.Application.Interfaces;
using ARI.Application.Services;
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
        => new(uow, rag, email, notif, new ThrowingScopeFactory());

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }
}
