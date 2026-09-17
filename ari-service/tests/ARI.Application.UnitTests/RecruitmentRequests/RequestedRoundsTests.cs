using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using ARI.Application.RecruitmentRequests;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.RecruitmentRequests;

/// <summary>
/// Phiếu khai LUÔN các vòng phỏng vấn vị trí này cần (ADR-063 mở rộng).
///
/// Vì sao nằm trên phiếu chứ không chỉ ở màn dựng tin: quy trình tuyển của một vị trí là quyết định
/// CHUYÊN MÔN — trưởng bộ phận biết vị trí này cần thi trắc nghiệm trước hay phỏng vấn thẳng, cần
/// mấy vòng chuyên môn. Recruiter dựng tin là thi hành quyết định đó; không hỏi ở phiếu thì họ phải
/// tự đoán hoặc đi hỏi lại bằng tay.
/// </summary>
public class RequestedRoundsTests
{
    private static readonly Guid HmId = Guid.Parse("88000000-0000-0000-0000-000000000001");
    private static readonly Department Engineering = new() { Id = Guid.NewGuid(), Name = "Engineering" };

    private static RecruitmentRequestInput Input(IReadOnlyList<string>? rounds) =>
        new("Backend Developer", 2, RecruitmentPriority.Medium, "Mở rộng đội",
            "Cần kỹ sư .NET", "Thành thạo C#", rounds,
            "full_time", "onsite", "Hà Nội", "senior",
            DateTimeOffset.UtcNow.AddMonths(1), 20_000_000, 30_000_000, "VND", false, ARI.Application.UnitTests.CvScoring.CvScoringKit.SampleRubric());

    private static InMemoryUnitOfWork Seed()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Engineering);
        // ADR-065: HM phải được gán đội, không thì lập phiếu bị chặn hẳn.
        uow.Seed(new User
        {
            Id = HmId, Email = "hm@x.io", Role = RoleNames.HiringManager, FullName = "HM",
            IsActive = true, DepartmentId = Engineering.Id,
        });
        return uow;
    }

    /// <summary>
    /// Lập phiếu qua CHÍNH lệnh nghiệp vụ — seam công khai, không gọi thẳng helper nội bộ. Test đi
    /// vòng qua cửa sau thì nó không chứng minh được đường thật có áp luật hay không.
    /// </summary>
    private static Task<Result<Guid>> Create(InMemoryUnitOfWork uow, IReadOnlyList<string>? rounds) =>
        new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(Input(rounds), HmId, RoleNames.HiringManager),
                CancellationToken.None);

    // ---------- Validate ----------

    [Fact]
    public async Task Phai_chon_it_nhat_mot_vong()
    {
        // Một ô tuỳ chọn hầu như luôn trống thì không phục vụ được mục đích nào — nên đây là ô
        // BẮT BUỘC, cùng nhóm với lý do / mô tả / yêu cầu ứng viên.
        var res = await Create(Seed(), Array.Empty<string>());

        Assert.True(res.IsFailure);
        Assert.Contains("ít nhất một vòng", res.Error);
    }

    [Fact]
    public async Task Bo_trong_cung_bi_chan()
    {
        var res = await Create(Seed(), null);

        Assert.True(res.IsFailure);
        Assert.Contains("ít nhất một vòng", res.Error);
    }

    [Fact]
    public async Task Gia_tri_la_bi_loai_nen_danh_sach_toan_rac_thi_coi_nhu_rong()
    {
        var res = await Create(Seed(), new[] { "phong_van_nhom", "" });

        Assert.True(res.IsFailure);
        Assert.Contains("ít nhất một vòng", res.Error);
    }

    [Theory]
    [InlineData("technical", "technical")]
    [InlineData("screening", "technical", "SCREENING ")]
    [InlineData("online_test", "screening", "technical", "online_test")]
    public async Task Moi_loai_vong_chi_chon_mot_lan(params string[] rounds)
    {
        // Danh sách chọn trên phiếu chỉ cho thêm mỗi loại một lần; request tự dựng gửi trùng thì báo lỗi
        // chứ không âm thầm bỏ bớt một vòng người dùng đã chọn.
        var res = await Create(Seed(), rounds);

        Assert.True(res.IsFailure);
        Assert.Contains("một lần", res.Error);
    }

    [Fact]
    public async Task Ba_vong_hop_le_thi_di_qua()
    {
        var res = await Create(Seed(),
            new[] { InterviewRoundTypes.OnlineTest, InterviewRoundTypes.Screening, InterviewRoundTypes.Technical });

        Assert.True(res.IsSuccess);
    }

    // ---------- Ghi và đọc lại ----------

    [Fact]
    public async Task Apply_giu_nguyen_THU_TU_vi_thu_tu_chinh_la_so_vong()
    {
        var uow = Seed();

        Assert.True((await Create(uow, new[] { InterviewRoundTypes.Technical, InterviewRoundTypes.OnlineTest })).IsSuccess);

        var entity = Assert.Single(uow.Repo<ARI.Domain.Entities.RecruitmentRequest>().Items);
        var read = RequestedRounds.Parse(entity.RequestedRounds);
        Assert.Equal(new[] { InterviewRoundTypes.Technical, InterviewRoundTypes.OnlineTest }, read);
    }

    [Fact]
    public async Task Apply_chuan_hoa_hoa_thuong_va_khoang_trang()
    {
        var uow = Seed();

        Assert.True((await Create(uow, new[] { "  Online_Test ", "TECHNICAL" })).IsSuccess);

        var entity = Assert.Single(uow.Repo<ARI.Domain.Entities.RecruitmentRequest>().Items);
        Assert.Equal(
            new[] { InterviewRoundTypes.OnlineTest, InterviewRoundTypes.Technical },
            RequestedRounds.Parse(entity.RequestedRounds));
    }

    // ---------- Đọc phòng thủ ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khong-phai-json")]
    [InlineData("{\"a\":1}")]
    public void Cot_hong_thi_coi_nhu_chua_khai_vong_nao(string? raw)
    {
        // Một giá trị hỏng KHÔNG được làm chết cả màn chi tiết phiếu — cùng cách `ParseCriterionScores`
        // xử lý dữ liệu điểm cũ (ADR-060).
        Assert.Empty(RequestedRounds.Parse(raw));
    }

    [Fact]
    public void Doc_lai_thi_loai_bo_gia_tri_la_con_sot_trong_du_lieu_cu()
    {
        Assert.Equal(
            new[] { InterviewRoundTypes.Screening },
            RequestedRounds.Parse("[\"screening\",\"group_interview\"]"));
    }

    // ---------- Vị từ "vòng này có cần Hiring Manager không" ----------

    [Fact]
    public void Vong_trac_nghiem_khong_can_Hiring_Manager()
    {
        // Đây là vị từ đứng sau cả ba luật xếp lịch của ADR-067 lẫn điều kiện "duyệt shortlist phải
        // kèm lịch rảnh". Sai ở đây là sai ở bốn chỗ cùng lúc.
        Assert.False(InterviewRoundTypes.NeedsHiringManager(InterviewRoundTypes.OnlineTest));
        Assert.False(InterviewRoundTypes.NeedsHiringManager("  ONLINE_TEST "));

        Assert.True(InterviewRoundTypes.NeedsHiringManager(InterviewRoundTypes.Screening));
        Assert.True(InterviewRoundTypes.NeedsHiringManager(InterviewRoundTypes.Technical));

        // Không tra được cấu hình vòng → giữ luật CHẶT, không âm thầm nới lỏng.
        Assert.True(InterviewRoundTypes.NeedsHiringManager(null));
    }
}
