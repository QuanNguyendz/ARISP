using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Emails;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Emails;

/// <summary>Renderer thư giả — trả bản dựng cố định và ghi lại tham số, để test đường quyền của PreviewEmail.</summary>
internal sealed class FakeEmailTemplateRenderer : IEmailTemplateRenderer
{
    public Result<RenderedEmail> Result_ { get; set; } =
        Result.Success(new RenderedEmail("Chủ đề", "<p>Xin chào</p>", "cand@corp.io", "Nguyen Van A"));
    public (string Key, Guid ContextId, Guid? SecondaryId)? LastCall { get; private set; }

    public Task<Result<RenderedEmail>> RenderAsync(string templateKey, Guid contextId, Guid? secondaryId, CancellationToken ct)
    {
        LastCall = (templateKey, contextId, secondaryId);
        return Task.FromResult(Result_);
    }
}

/// <summary>
/// Xem trước thư gửi ứng viên (<see cref="PreviewEmailQueryHandler"/>, ADR-061 Phase 4): mẫu lạ → NotFound;
/// đây là đường ĐỌC dữ liệu hồ sơ nên gác quyền y hệt endpoint đọc hồ sơ (Owner), chỉ khi qua cổng mới render.
/// </summary>
public class PreviewEmailQueryHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static (InMemoryUnitOfWork uow, ARI.Domain.Entities.Application app) Seed()
    {
        var job = new JobPosting { Id = Guid.NewGuid(), Title = "Backend Developer", CreatedByUserId = OwnerId, Status = "active" };
        var app = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "cand@corp.io", CandidateName = "Nguyen Van A",
            Status = ApplicationStatuses.Interview,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new User { Id = OwnerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true });
        return (uow, app);
    }

    private static Task<Result<RenderedEmail>> Run(
        InMemoryUnitOfWork uow, FakeEmailTemplateRenderer renderer, string key, Guid contextId, Guid actor, string role)
        => new PreviewEmailQueryHandler(uow, renderer)
            .Handle(new PreviewEmailQuery(key, contextId, null, actor, role), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Unknown_template_not_found()
    {
        var (uow, app) = Seed();
        var renderer = new FakeEmailTemplateRenderer();

        var res = await Run(uow, renderer, "khong_ton_tai", app.Id, OwnerId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Null(renderer.LastCall);            // chặn trước khi render
    }

    [Fact]
    public async Task UTCID02_Application_not_found()
    {
        var renderer = new FakeEmailTemplateRenderer();

        var res = await Run(new InMemoryUnitOfWork(), renderer,
            EmailTemplateKeys.ApplicationRejected, Guid.NewGuid(), OwnerId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Outsider_forbidden()
    {
        var (uow, app) = Seed();
        var renderer = new FakeEmailTemplateRenderer();

        var res = await Run(uow, renderer, EmailTemplateKeys.ApplicationRejected, app.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(renderer.LastCall);
    }

    [Fact]
    public async Task UTCID04_Owner_gets_rendered_email()
    {
        var (uow, app) = Seed();
        var renderer = new FakeEmailTemplateRenderer();

        var res = await Run(uow, renderer, EmailTemplateKeys.ApplicationRejected, app.Id, OwnerId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal("Chủ đề", res.Value.Subject);
        Assert.Equal(app.Id, renderer.LastCall!.Value.ContextId);
        Assert.Equal(EmailTemplateKeys.ApplicationRejected, renderer.LastCall.Value.Key);
    }
}
