using System;
using System.Collections.Generic;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Dấu vết tham dự một vòng (<see cref="RoundAttendance"/>) — thứ mà tác vụ tự đánh trượt người vắng
/// mặt (ADR-059) đối chiếu.
///
/// Lỗi mà bộ test này khoá lại: phép kiểm cũ chỉ tìm <see cref="InterviewSession"/> loại
/// <c>real</c>, mà vòng TRẮC NGHIỆM không sinh phiên nào. Hậu quả là mọi ứng viên thi trắc nghiệm —
/// <b>kể cả người đã nộp bài</b> — bị đánh <c>not_pass</c> khi khung giờ trôi qua, rồi biến khỏi
/// vòng sang "Không phù hợp" mà không ai bấm gì.
/// </summary>
public class RoundAttendanceTests
{
    private static readonly Guid AppA = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid AppB = Guid.Parse("50000000-0000-0000-0000-000000000002");

    private static InterviewSession RealSession(Guid appId, int round) => new()
    {
        ApplicationId = appId, RoundNumber = round, SessionType = "real",
        Status = InterviewSessionStatuses.Completed,
    };

    private static OnlineTestSubmission Submission(Guid appId, int round, decimal score, bool passed) => new()
    {
        ApplicationId = appId, RoundNumber = round, Score = score, IsPassed = passed,
    };

    // ---------- Ai đi đường "không tham dự → đánh trượt + trả chỗ" ----------

    [Theory]
    [InlineData("online_test")]
    [InlineData("ONLINE_TEST")]
    public void Vong_trac_nghiem_khong_bao_gio_bi_danh_truot_vang_mat(string roundType)
    {
        // Đường no-show huỷ lịch, TRẢ CHỖ trong ca và tự ghi not_pass. Ở vòng trắc nghiệm cả ba đều
        // sai: chỉ ứng viên từ chối mới trả chỗ, và không vào làm bài vẫn là có kết quả (0 điểm)
        // để Recruiter quyết định — hết hạn thì hệ thống nộp thay (OnlineTestExpiry).
        Assert.False(RoundAttendance.CanBeNoShow(roundType));
    }

    [Theory]
    [InlineData("screening")]
    [InlineData("technical")]
    [InlineData(null)]
    public void Vong_hoi_thoai_van_di_duong_khong_tham_du(string? roundType)
    {
        Assert.True(RoundAttendance.CanBeNoShow(roundType));
    }

    // ---------- Vòng trắc nghiệm: bằng chứng là BÀI ĐÃ NỘP ----------

    [Theory]
    [InlineData(95)]
    [InlineData(70)]
    [InlineData(30)]
    [InlineData(0)]
    public void Nop_bai_la_da_du_thi_du_duoc_bao_nhieu_diem(decimal score)
    {
        // ĐIỂM không tham gia phép kiểm. Dưới điểm sàn vẫn là đã dự thi — việc loại hay giữ người đó
        // là quyết định của Recruiter, không phải của một tác vụ nền.
        var attended = RoundAttendance.HasAttended(
            AppA, 1, "online_test",
            Array.Empty<InterviewSession>(),
            new[] { Submission(AppA, 1, score, passed: score >= 70) });

        Assert.True(attended);
    }

    [Fact]
    public void Vong_trac_nghiem_khong_nop_bai_thi_chua_co_dau_vet_tham_du()
    {
        // Chưa có bài thì chưa có dấu vết tham dự — nhưng ở vòng này điều đó KHÔNG dẫn tới đánh
        // trượt (xem CanBeNoShow): hết hạn thì hệ thống nộp thay và bài đó trở thành dấu vết.
        var attended = RoundAttendance.HasAttended(
            AppA, 1, "online_test",
            Array.Empty<InterviewSession>(),
            Array.Empty<OnlineTestSubmission>());

        Assert.False(attended);
    }

    [Fact]
    public void Vong_trac_nghiem_khong_nhan_bai_cua_nguoi_khac()
    {
        var attended = RoundAttendance.HasAttended(
            AppA, 1, "online_test",
            Array.Empty<InterviewSession>(),
            new[] { Submission(AppB, 1, 90, passed: true) });

        Assert.False(attended);
    }

    [Fact]
    public void Vong_trac_nghiem_khong_nhan_bai_cua_vong_khac()
    {
        var attended = RoundAttendance.HasAttended(
            AppA, 2, "online_test",
            Array.Empty<InterviewSession>(),
            new[] { Submission(AppA, 1, 90, passed: true) });

        Assert.False(attended);
    }

    [Fact]
    public void Vong_trac_nghiem_khong_lay_phien_phong_van_lam_bang_chung()
    {
        // Phiên phỏng vấn của vòng khác không nói gì về việc có làm bài thi hay không.
        var attended = RoundAttendance.HasAttended(
            AppA, 1, "online_test",
            new[] { RealSession(AppA, 1) },
            Array.Empty<OnlineTestSubmission>());

        Assert.False(attended);
    }

    // ---------- Vòng hội thoại: bằng chứng là PHIÊN PHỎNG VẤN THẬT ----------

    [Theory]
    [InlineData("screening")]
    [InlineData("technical")]
    [InlineData(null)]
    public void Vong_hoi_thoai_can_phien_phong_van_that(string? roundType)
    {
        // roundType null (không tra được cấu hình vòng) đi theo nhánh vòng hội thoại — giữ đúng hành
        // vi cũ thay vì âm thầm nới lỏng phép kiểm.
        Assert.True(RoundAttendance.HasAttended(
            AppA, 1, roundType, new[] { RealSession(AppA, 1) }, Array.Empty<OnlineTestSubmission>()));

        Assert.False(RoundAttendance.HasAttended(
            AppA, 1, roundType, Array.Empty<InterviewSession>(), Array.Empty<OnlineTestSubmission>()));
    }

    [Fact]
    public void Vong_hoi_thoai_khong_lay_bai_thi_lam_bang_chung()
    {
        var attended = RoundAttendance.HasAttended(
            AppA, 1, "technical",
            Array.Empty<InterviewSession>(),
            new[] { Submission(AppA, 1, 90, passed: true) });

        Assert.False(attended);
    }

    [Fact]
    public void Danh_sach_rong_thi_khong_ai_duoc_coi_la_da_du()
    {
        Assert.False(RoundAttendance.HasAttended(
            AppA, 1, "online_test",
            new List<InterviewSession>(), new List<OnlineTestSubmission>()));
    }
}
