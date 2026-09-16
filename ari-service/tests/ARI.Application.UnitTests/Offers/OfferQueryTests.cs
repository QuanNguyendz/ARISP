using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Offers;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Offers;

/// <summary>Hồ sơ + tin + thư mời dùng chung cho test đọc/sửa thư mời (ADR-061/063); người gọi mặc định là CHỦ TIN.</summary>
internal static class OfferQueryFixture
{
    public static readonly Guid OwnerId = Guid.NewGuid();

    public static (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app, Offer offer) Seed(
        string offerStatus = OfferStatus.Draft)
    {
        var job = new JobPosting { Id = Guid.NewGuid(), Title = "Backend Developer", CreatedByUserId = OwnerId, Status = "active" };
        var app = new ARI.Domain.Entities.Application
        {
            Id = Guid.NewGuid(), JobPostingId = job.Id, CandidateEmail = "cand@corp.io",
            CandidateName = "Nguyen Van A", Status = ApplicationStatuses.Pass,
        };
        var offer = new Offer
        {
            Id = Guid.NewGuid(), ApplicationId = app.Id, JobPostingId = job.Id, Status = offerStatus,
            Position = "Backend Developer", SalaryAmount = 25_000_000m, SalaryCurrency = "VND",
            WorkLocation = "Hà Nội", EmploymentType = "full_time",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(offer)
            .Seed(new User { Id = OwnerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true });
        return (uow, job, app, offer);
    }
}

/// <summary>
/// Danh sách thư mời (<see cref="GetOffersQueryHandler"/>, ADR-061): phạm vi theo tin do SERVER quyết định
/// (không nhận <c>?mine</c>), lọc trạng thái ở SQL; admin thấy toàn bộ.
/// </summary>
public class GetOffersQueryHandlerTests
{
    [Fact]
    public async Task UTCID01_Empty_when_no_offers()
    {
        var res = await new GetOffersQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetOffersQuery(OfferQueryFixture.OwnerId, AppRoles.Recruiter, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID02_Owner_sees_offer_on_own_job()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed();

        var res = await new GetOffersQueryHandler(uow)
            .Handle(new GetOffersQuery(OfferQueryFixture.OwnerId, AppRoles.Recruiter, null), CancellationToken.None);

        Assert.Equal(offer.Id, Assert.Single(res.Value).Id);
    }

    [Fact]
    public async Task UTCID03_Status_filter_applied()
    {
        var (uow, _, _, _) = OfferQueryFixture.Seed();

        var res = await new GetOffersQueryHandler(uow)
            .Handle(new GetOffersQuery(OfferQueryFixture.OwnerId, AppRoles.Recruiter, OfferStatus.Sent), CancellationToken.None);

        Assert.Empty(res.Value); // offer đang Draft, lọc theo Sent → không có
    }

    [Fact]
    public async Task UTCID04_Admin_sees_all_offers()
    {
        var (uow, _, _, _) = OfferQueryFixture.Seed();

        var res = await new GetOffersQueryHandler(uow)
            .Handle(new GetOffersQuery(Guid.NewGuid(), AppRoles.HrAdmin, null), CancellationToken.None);

        Assert.Single(res.Value); // admin: phạm vi null = toàn bộ
    }
}

/// <summary>
/// Xem một thư mời (<see cref="GetOfferByIdQueryHandler"/>, ADR-061): cổng đọc ở ngưỡng TeamMember.
/// </summary>
public class GetOfferByIdQueryHandlerTests
{
    [Fact]
    public async Task UTCID01_Not_found()
    {
        var res = await new GetOfferByIdQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetOfferByIdQuery(Guid.NewGuid(), OfferQueryFixture.OwnerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Outsider_forbidden()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed();

        var res = await new GetOfferByIdQueryHandler(uow)
            .Handle(new GetOfferByIdQuery(offer.Id, Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Owner_sees_offer()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed();

        var res = await new GetOfferByIdQueryHandler(uow)
            .Handle(new GetOfferByIdQuery(offer.Id, OfferQueryFixture.OwnerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(offer.Id, res.Value.Id);
    }
}

/// <summary>
/// Sửa bản nháp thư mời (<see cref="UpdateOfferCommandHandler"/>, ADR-063): cổng "soạn được" (chủ tin / HM chính),
/// chỉ khi còn Draft; PUT thiếu trường thì GIỮ giá trị cũ (không âm thầm xoá nơi làm việc / hình thức).
/// </summary>
public class UpdateOfferCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Not_found()
    {
        var res = await new UpdateOfferCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateOfferCommand(Guid.NewGuid(), new UpsertOfferRequest(), OfferQueryFixture.OwnerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Partial_update_keeps_other_fields()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed();
        var request = new UpsertOfferRequest { ApplicationId = offer.ApplicationId, SalaryAmount = 30_000_000m };

        var res = await new UpdateOfferCommandHandler(uow)
            .Handle(new UpdateOfferCommand(offer.Id, request, OfferQueryFixture.OwnerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(30_000_000m, offer.SalaryAmount);
        Assert.Equal("Hà Nội", offer.WorkLocation);      // PUT thiếu trường → giữ nguyên
        Assert.Equal("full_time", offer.EmploymentType);
    }

    [Fact]
    public async Task UTCID03_Forbidden_for_outsider()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed();

        var res = await new UpdateOfferCommandHandler(uow)
            .Handle(new UpdateOfferCommand(offer.Id, new UpsertOfferRequest(), Guid.NewGuid(), AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID04_Rejected_when_not_draft()
    {
        var (uow, _, _, offer) = OfferQueryFixture.Seed(offerStatus: OfferStatus.Sent);

        var res = await new UpdateOfferCommandHandler(uow)
            .Handle(new UpdateOfferCommand(offer.Id, new UpsertOfferRequest(), OfferQueryFixture.OwnerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("bản nháp", res.Error);
    }
}
