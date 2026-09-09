using System;
using System.Linq;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Domain.UnitTests.Constants;

/// <summary>
/// <see cref="ApplicationStatuses"/> là nơi DUY NHẤT trả lời "hồ sơ đã đóng chưa". Trước khi có
/// lớp này câu hỏi đó bị chép thành 7 danh sách khác nhau, mỗi bản thiếu một giá trị — cách một
/// trạng thái mới lặng lẽ bị tác vụ nền ghi đè. Các bài test dưới đây chốt đúng những bất biến
/// mà 7 chỗ gọi đang dựa vào.
/// </summary>
public class ApplicationStatusesTests
{
    [Fact]
    public void Terminal_covers_every_closed_outcome_including_the_new_offer_states()
    {
        // "hired" và "offer_declined" phải nằm trong Terminal NGAY TỪ ĐẦU, trước khi có mã nào
        // ghi ra chúng: tác vụ quét no-show đọc danh sách này để quyết định có đánh trượt hồ sơ
        // hay không. Thiếu "hired" ở đây là ứng viên đã tuyển bị đánh trượt vì một lịch cũ.
        Assert.Contains(ApplicationStatuses.Hired, ApplicationStatuses.Terminal);
        Assert.Contains(ApplicationStatuses.OfferDeclined, ApplicationStatuses.Terminal);

        Assert.Contains(ApplicationStatuses.Pass, ApplicationStatuses.Terminal);
        Assert.Contains(ApplicationStatuses.NotPass, ApplicationStatuses.Terminal);
        Assert.Contains(ApplicationStatuses.CvRejected, ApplicationStatuses.Terminal);
        Assert.Contains(ApplicationStatuses.Withdrawn, ApplicationStatuses.Terminal);
    }

    [Theory]
    [InlineData("hired")]
    [InlineData("offer_declined")]
    [InlineData("pass")]
    [InlineData("not_pass")]
    [InlineData("cv_rejected")]
    [InlineData("withdrawn")]
    [InlineData("failed")] // giá trị cũ, không còn nơi nào ghi nhưng vẫn phải coi là đã đóng
    public void IsTerminal_true_for_closed_applications(string status)
        => Assert.True(ApplicationStatuses.IsTerminal(status));

    [Theory]
    [InlineData("invited")]
    [InlineData("cv_submitted")]
    [InlineData("hm_review")]
    [InlineData("screening")]
    [InlineData("interview")]
    [InlineData("offer")] // đã GỬI offer nhưng ứng viên chưa trả lời → hồ sơ vẫn đang chạy
    public void IsTerminal_false_while_the_application_is_still_moving(string status)
        => Assert.False(ApplicationStatuses.IsTerminal(status));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsTerminal_false_for_missing_status(string? status)
        => Assert.False(ApplicationStatuses.IsTerminal(status));

    [Fact]
    public void Status_comparisons_ignore_case_and_padding()
    {
        // Cột status là text tự do, không có CHECK ở DB — dữ liệu lệch hoa thường vẫn phải
        // được nhận diện đúng thay vì âm thầm rơi vào nhánh "chưa đóng".
        Assert.True(ApplicationStatuses.IsTerminal("  HIRED "));
        Assert.True(ApplicationStatuses.Is("Pass", ApplicationStatuses.Pass));
        Assert.False(ApplicationStatuses.Is("passed", ApplicationStatuses.Pass));
    }

    [Fact]
    public void CvPhase_includes_hm_review_so_rejection_lands_on_cv_rejected()
    {
        // Từ chối trong giai đoạn CV ghi "cv_rejected"; sau giai đoạn đó ghi "not_pass".
        // Hồ sơ đang chờ Hiring Manager duyệt vẫn CHƯA vào quy trình phỏng vấn, nên bị loại ở
        // đó phải tính là loại từ vòng CV.
        Assert.True(ApplicationStatuses.IsCvPhase(ApplicationStatuses.HmReview));
        Assert.True(ApplicationStatuses.IsCvPhase(ApplicationStatuses.CvSubmitted));
        Assert.True(ApplicationStatuses.IsCvPhase(ApplicationStatuses.Invited));

        Assert.False(ApplicationStatuses.IsCvPhase(ApplicationStatuses.Screening));
        Assert.False(ApplicationStatuses.IsCvPhase(ApplicationStatuses.Interview));
    }

    [Fact]
    public void CvPassed_excludes_everyone_still_waiting_on_the_cv_decision()
    {
        // Điều kiện làm bài thi trắc nghiệm (ADR-049): phải qua vòng duyệt CV đã.
        Assert.False(ApplicationStatuses.IsCvPassed(ApplicationStatuses.CvSubmitted));
        Assert.False(ApplicationStatuses.IsCvPassed(ApplicationStatuses.HmReview));
        Assert.False(ApplicationStatuses.IsCvPassed(ApplicationStatuses.CvRejected));
        Assert.False(ApplicationStatuses.IsCvPassed(ApplicationStatuses.Withdrawn));

        Assert.True(ApplicationStatuses.IsCvPassed(ApplicationStatuses.Screening));
        Assert.True(ApplicationStatuses.IsCvPassed(ApplicationStatuses.Interview));
        Assert.True(ApplicationStatuses.IsCvPassed(ApplicationStatuses.Pass));
    }

    [Fact]
    public void Every_status_in_a_group_is_also_a_declared_status()
    {
        // Chặn lỗi gõ nhầm: một chuỗi lạ lọt vào Terminal sẽ khiến hồ sơ mang giá trị đó
        // không bao giờ bị coi là đã đóng, mà không có tín hiệu nào.
        var all = ApplicationStatuses.All.Append(ApplicationStatuses.LegacyFailed).ToHashSet(StringComparer.Ordinal);

        Assert.All(ApplicationStatuses.Terminal, s => Assert.Contains(s, all));
        Assert.All(ApplicationStatuses.CvPhase, s => Assert.Contains(s, all));
        Assert.All(ApplicationStatuses.CvPassed, s => Assert.Contains(s, all));
    }

    [Fact]
    public void Terminal_and_cv_phase_never_overlap()
    {
        // Một hồ sơ không thể vừa "đang chờ duyệt CV" vừa "đã đóng" — nếu chồng nhau thì
        // RejectApplicationAsync sẽ vừa từ chối vừa báo lỗi đã kết thúc.
        Assert.Empty(ApplicationStatuses.CvPhase.Intersect(ApplicationStatuses.Terminal, StringComparer.OrdinalIgnoreCase));
    }
}
