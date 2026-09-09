using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Wrapper CQRS đổi trạng thái hồ sơ (<see cref="UpdateApplicationStatusCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdateApplication" (UTCID01–05): forward Id/Status tới service và trả nguyên kết quả (success/failure),
/// tôn trọng cancellation, propagate exception. (Logic nghiệp vụ nằm ở service — test riêng.)
///
/// <b>ADR-061:</b> wrapper không còn "mỏng" hoàn toàn — nó gác quyền TRƯỚC khi gọi service (kéo trạng
/// thái hồ sơ bằng tay là thao tác vận hành phễu). Nên mọi ca đều phải gieo hồ sơ + tin và truyền
/// danh tính; hai ca cuối khoá chính cổng đó.
/// </summary>
public class UpdateApplicationStatusCommandHandlerTests
{
    private static readonly Guid AppId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid JobId = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("32000000-0000-0000-0000-000000000001");

    /// <summary>Hồ sơ + tin để cổng <c>JobAccess</c> có gì mà xét; người gọi mặc định là CHỦ TIN.</summary>
    private static InMemoryUnitOfWork Seeded()
        => new InMemoryUnitOfWork()
            .Seed(new JobPosting { Id = JobId, CreatedByUserId = OwnerId, Title = "Backend Developer" })
            .Seed(new ARI.Domain.Entities.Application
            {
                Id = AppId, JobPostingId = JobId, Status = "cv_submitted",
            });

    private static UpdateApplicationStatusCommandHandler Handler(StubApplicationService svc, InMemoryUnitOfWork uow)
        => new(svc, uow);

    private static UpdateApplicationStatusCommand Cmd(string status, Guid? userId = null, string? role = null)
        => new(AppId, status, userId ?? OwnerId, role ?? AppRoles.Recruiter);

    // UTCID01 — service trả app đã cập nhật → Success, forward đúng Id/Status
    [Fact]
    public async Task UTCID01_Forwards_and_returns_updated()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Success(new ApplicationResponse { Id = AppId, Status = "interview" }) };

        var res = await Handler(svc, Seeded()).Handle(Cmd("interview"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("interview", res.Value.Status);
        Assert.Equal(AppId, svc.LastUpdateId);
        Assert.Equal("interview", svc.LastUpdateStatus);
    }

    // UTCID02 — service trả not-found failure → trả nguyên
    [Fact]
    public async Task UTCID02_Not_found_failure_passthrough()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Failure<ApplicationResponse>("Application not found.") };

        var res = await Handler(svc, Seeded()).Handle(Cmd("interview"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Application not found.", res.Error);
    }

    // UTCID03 — Status rỗng; service trả validation failure → trả nguyên
    [Fact]
    public async Task UTCID03_Validation_failure_passthrough()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Failure<ApplicationResponse>("Status cannot be empty.") };

        var res = await Handler(svc, Seeded()).Handle(Cmd(""), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Status cannot be empty.", res.Error);
        Assert.Equal("", svc.LastUpdateStatus);
    }

    // UTCID04 — token đã huỷ → OperationCanceledException
    [Fact]
    public async Task UTCID04_Cancelled_token_throws()
    {
        var svc = new StubApplicationService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Handler(svc, Seeded()).Handle(Cmd("interview"), cts.Token));
    }

    // UTCID05 — service ném lỗi → propagate
    [Fact]
    public async Task UTCID05_Service_throws()
    {
        var svc = new StubApplicationService { UpdateThrows = new Exception("Service Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(svc, Seeded()).Handle(Cmd("interview"), CancellationToken.None));
        Assert.Equal("Service Error", ex.Message);
    }

    // ---------- ADR-061: cổng quyền chạy TRƯỚC service ----------

    [Fact]
    public async Task Ho_so_khong_ton_tai_thi_khong_goi_service()
    {
        var svc = new StubApplicationService();

        var res = await Handler(svc, new InMemoryUnitOfWork()).Handle(Cmd("interview"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Null(svc.LastUpdateId);
    }

    [Fact]
    public async Task Thanh_vien_doi_tuyen_dung_khong_tu_doi_trang_thai()
    {
        // Hiring Manager ĐỌC được hồ sơ của tin mình nhưng không kéo trạng thái bằng tay; họ tác
        // động qua các cổng quyết định riêng (duyệt shortlist, chốt verdict).
        var hmId = Guid.NewGuid();
        var uow = Seeded().Seed(new JobHiringTeamMember
        {
            JobPostingId = JobId, UserId = hmId,
            RoleOnJob = JobTeamRoles.HiringManager, IsPrimary = true,
        });
        var svc = new StubApplicationService();

        var res = await Handler(svc, uow).Handle(
            Cmd("interview", userId: hmId, role: AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(svc.LastUpdateId);
    }
}
