using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.RecruitmentRequests;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Departments;

/// <summary>
/// Đội/bộ phận (ADR-065) — quản lý danh sách, và **luật cốt lõi**: Hiring Manager không thể lập
/// phiếu ghi đội khác của mình.
///
/// Ranh giới cần giữ: đây là toàn vẹn dữ liệu ("phiếu này của đội nào"), KHÔNG phải phân quyền
/// ("ai được quyết định về tin này" — vẫn do `job_hiring_team_members` trả lời, ADR-061).
/// </summary>
public class DepartmentScopeTests
{
    private readonly Guid _hmId = Guid.NewGuid();
    private readonly Guid _hrLeaderId = Guid.NewGuid();

    private static readonly Department Backend = new() { Id = Guid.NewGuid(), Name = "Backend Team" };
    private static readonly Department Qa = new() { Id = Guid.NewGuid(), Name = "QA Team" };

    private InMemoryUnitOfWork Seed(Guid? hmDepartment = null)
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend, Qa);
        uow.Seed(
            new User
            {
                Id = _hmId, Email = "hm@x.io", Role = RoleNames.HiringManager, FullName = "HM",
                IsActive = true, DepartmentId = hmDepartment ?? Backend.Id,
            },
            new User
            {
                Id = _hrLeaderId, Email = "hr@x.io", Role = RoleNames.HrAdmin, FullName = "HR Leader",
                IsActive = true,
            });
        return uow;
    }

    private static RecruitmentRequestInput Input() =>
        new("Backend Developer", 2, RecruitmentPriority.High, "Mở rộng đội",
            "Cần kỹ sư .NET", "Thành thạo C#", "full_time", "onsite", "Hà Nội", "senior",
            DateTimeOffset.UtcNow.AddMonths(1), 20_000_000, 30_000_000, "VND");

    private Task<Result<Guid>> Create(InMemoryUnitOfWork uow, RecruitmentRequestInput input, Guid actor, string role) =>
        new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(input, actor, role), CancellationToken.None);

    // ---------- Luật cốt lõi ----------

    [Fact]
    public async Task Hm_lap_phieu_thi_doi_LAY_TU_TAI_KHOAN()
    {
        // Toàn bộ mục đích của ADR-065: đội ghi trên phiếu do TÀI KHOẢN quyết định. Biểu mẫu không
        // còn ô nào để khai đội, và lệnh cũng không còn NHẬN tham số đó — nên một request tự dựng
        // cũng không có đường nào khai đội khác.
        var uow = Seed();

        var res = await Create(uow, Input(), _hmId, RoleNames.HiringManager);

        Assert.True(res.IsSuccess);
        Assert.Equal(Backend.Id, Assert.Single(uow.Repo<RecruitmentRequest>().Items).DepartmentId);
    }

    [Fact]
    public async Task Hm_chua_duoc_gan_doi_thi_khong_lap_duoc_phieu()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend);
        uow.Seed(new User
        {
            Id = _hmId, Email = "hm@x.io", Role = RoleNames.HiringManager, IsActive = true,
            DepartmentId = null,
        });

        var res = await Create(uow, Input(), _hmId, RoleNames.HiringManager);

        Assert.True(res.IsFailure);
        // Thông điệp phải chỉ rõ phải nhờ AI, chứ không phải một ô trống không giải thích.
        Assert.Contains("Super Admin", res.Error);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }

    [Theory]
    [InlineData(RoleNames.HrAdmin)]
    [InlineData(RoleNames.SuperAdmin)]
    [InlineData(RoleNames.Recruiter)]
    public async Task Chi_Hiring_Manager_lap_duoc_phieu(string role)
    {
        // Không phải chuyện phân quyền thuần tuý: `CreateJobCommand` gán **người lập phiếu làm Hiring
        // Manager của tin** (ADR-063). HR Leader lập phiếu nghĩa là chính họ thành HM của tin — một
        // người vừa giữ cổng chuyên môn vừa giữ cổng ngân sách, đúng hai vai ADR-061/063 tách ra.
        var uow = Seed();

        var res = await Create(uow, Input(), _hrLeaderId, role);

        Assert.True(res.IsFailure);
        Assert.Contains("Hiring Manager", res.Error);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }

    [Fact]
    public async Task Phieu_cu_KHONG_co_doi_thi_duoc_dien_lai_khi_chu_phieu_sua()
    {
        // Migration cố ý không đoán để backfill từ chuỗi cũ, nên những phiếu đó có `DepartmentId` rỗng.
        // Ô đội trên biểu mẫu chỉ đọc và `Apply` không chạm cột này — không điền lại thì chúng hiện "—" vĩnh viễn.
        var uow = Seed();
        var legacy = new RecruitmentRequest
        {
            RequestedByUserId = _hmId, Title = "Phiếu cũ", Headcount = 1,
            Priority = RecruitmentPriority.Medium, Status = RecruitmentRequestStatus.Pending,
            DepartmentId = null,
        };
        uow.Seed(legacy);

        var res = await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(legacy.Id, Input(), _hmId, RoleNames.HiringManager),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(Backend.Id, legacy.DepartmentId);
    }

    [Fact]
    public async Task Sua_phieu_KHONG_ghi_de_doi_da_co()
    {
        // Chỉ điền vào chỗ trống. Ghi đè là mở lại đúng lỗ hổng ADR-065 bịt: lập phiếu đúng đội rồi sửa sang đội khác.
        var uow = Seed();
        var req = new RecruitmentRequest
        {
            RequestedByUserId = _hmId, Title = "Phiếu của đội QA", Headcount = 1,
            Priority = RecruitmentPriority.Medium, Status = RecruitmentRequestStatus.Pending,
            DepartmentId = Qa.Id,
        };
        uow.Seed(req);

        await new UpdateRecruitmentRequestCommandHandler(uow).Handle(
            new UpdateRecruitmentRequestCommand(req.Id, Input(), _hmId, RoleNames.HiringManager),
            CancellationToken.None);

        Assert.Equal(Qa.Id, req.DepartmentId);
    }

    [Fact]
    public async Task Ten_doi_trung_KHONG_phan_biet_hoa_thuong_thi_bi_chan()
    {
        // Postgres so sánh phân biệt hoa thường, nên chỉ dựa vào unique index thì "Backend Team" và
        // "backend team" là hai đội khác nhau — mọi thống kê theo đội sẽ tách đôi mà không ai để ý.
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend);

        var res = await new CreateDepartmentCommandHandler(uow).Handle(
            new CreateDepartmentCommand(new DepartmentInput("  backend team  ", null, null, true), _hrLeaderId),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    [Fact]
    public async Task Doi_da_TAT_van_tra_duoc_TEN()
    {
        // Đội giải thể thì tắt chứ không xoá — chính là để phiếu và tài khoản cũ vẫn hiện đúng tên,
        // thay vì một ô trống không ai hiểu.
        var uow = new InMemoryUnitOfWork();
        var closed = new Department { Id = Guid.NewGuid(), Name = "Đội cũ", IsActive = false };
        uow.Seed(closed);

        var names = await DepartmentLookup.NamesAsync(uow, new[] { closed.Id }, CancellationToken.None);

        Assert.Equal("Đội cũ", names[closed.Id]);
    }

    [Fact]
    public async Task Doi_ten_thanh_ten_cua_doi_khac_thi_bi_chan()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend, Qa);

        var res = await new UpdateDepartmentCommandHandler(uow).Handle(
            new UpdateDepartmentCommand(Qa.Id, new DepartmentInput("Backend Team", null, null, true), _hrLeaderId),
            CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    [Fact]
    public async Task Doi_giu_nguyen_ten_cua_chinh_no_thi_van_sua_duoc()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend);

        var res = await new UpdateDepartmentCommandHandler(uow).Handle(
            new UpdateDepartmentCommand(Backend.Id, new DepartmentInput("Backend Team", "BE", "Đội backend", true), _hrLeaderId),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("BE", uow.Repo<Department>().Items[0].Code);
    }
}
