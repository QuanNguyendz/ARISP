using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interviews;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Phòng chờ buổi phỏng vấn THẬT (ADR-067): buổi phỏng vấn chỉ bắt đầu khi Hiring Manager đã vào
/// phòng cùng AI, và ứng viên vào được là do HM cho vào.
///
/// Chốt chặn thật nằm ở TRẠNG THÁI phiên: phiên sinh ra ở <c>waiting</c>, mà AI chỉ sinh câu hỏi khi
/// phiên <c>active</c>. Nên các test dưới đây kiểm đúng thứ đó — không kiểm một cờ giao diện nào.
/// </summary>
public class InterviewAdmissionTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private (InMemoryUnitOfWork uow, JobPosting job, InterviewSession session) Seed(
        bool withHiringManager = true, string status = InterviewSessionStatuses.Waiting)
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var session = new InterviewSession
        {
            ApplicationId = app.Id, RoundNumber = 1, RoundType = "screening",
            SessionType = "real", Status = status,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        if (withHiringManager)
            uow.Seed(new JobHiringTeamMember
            {
                JobPostingId = job.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
                IsPrimary = true, AddedByUserId = _ownerId,
            });

        return (uow, job, session);
    }

    private Task<Result<WaitingRoomDto>> Join(InMemoryUnitOfWork uow, Guid sessionId, Guid? actor = null, string? role = null)
        => new JoinInterviewRoomCommandHandler(uow, new RecordingNotificationService())
            .Handle(new JoinInterviewRoomCommand(sessionId, actor ?? _hmId, role ?? AppRoles.HiringManager),
                CancellationToken.None);

    private Task<Result<WaitingRoomDto>> Admit(InMemoryUnitOfWork uow, Guid sessionId, Guid? actor = null, string? role = null)
        => new AdmitCandidateCommandHandler(uow, new RecordingNotificationService())
            .Handle(new AdmitCandidateCommand(sessionId, actor ?? _hmId, role ?? AppRoles.HiringManager),
                CancellationToken.None);

    // ---------- Điều kiện bắt đầu ----------

    [Fact]
    public async Task Chua_ai_vao_phong_thi_khong_cho_ung_vien_vao_duoc()
    {
        // Đây là chính luật: "điều kiện để buổi phỏng vấn bắt đầu là HM đã vào phòng cùng AI".
        var (uow, _, session) = Seed();

        var res = await Admit(uow, session.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("Chưa có ai vào phòng", res.Error);
        Assert.Equal(InterviewSessionStatuses.Waiting, session.Status);
    }

    [Fact]
    public async Task HM_vao_phong_roi_cho_vao_thi_phien_moi_chay()
    {
        var (uow, _, session) = Seed();

        var joined = await Join(uow, session.Id);
        Assert.True(joined.IsSuccess);
        Assert.NotNull(session.HmJoinedAt);
        Assert.Equal(InterviewSessionStatuses.Waiting, session.Status); // vào phòng CHƯA phải cho vào

        var admitted = await Admit(uow, session.Id);

        Assert.True(admitted.IsSuccess);
        Assert.Equal(InterviewSessionStatuses.Active, session.Status);
        Assert.NotNull(session.AdmittedAt);
        Assert.Equal(_hmId, session.AdmittedByUserId);
    }

    [Fact]
    public async Task Dong_ho_thoi_luong_tinh_tu_luc_CHO_VAO_chu_khong_phai_luc_nhap_ma()
    {
        // Nếu tính từ lúc tạo phiên thì thời gian ngồi chờ bị trừ thẳng vào giờ phỏng vấn của ứng viên.
        var (uow, _, session) = Seed();
        Assert.Null(session.StartedAt);

        await Join(uow, session.Id);
        await Admit(uow, session.Id);

        Assert.NotNull(session.StartedAt);
        Assert.Equal(session.AdmittedAt, session.StartedAt);
    }

    [Fact]
    public async Task Cho_vao_hai_lan_khong_gay_hai()
    {
        // Mạng chập chờn / bấm hai lần là chuyện thường ở quầy lễ tân.
        var (uow, _, session) = Seed();
        await Join(uow, session.Id);
        await Admit(uow, session.Id);
        var firstAdmit = session.AdmittedAt;

        var again = await Admit(uow, session.Id);

        Assert.True(again.IsSuccess);
        Assert.Equal(firstAdmit, session.AdmittedAt);
    }

    // ---------- Ai được điều khiển phòng ----------

    [Fact]
    public async Task Nguoi_ngoai_khong_dieu_khien_duoc_phong()
    {
        var (uow, _, session) = Seed();

        var res = await Join(uow, session.Id, actor: Guid.NewGuid(), role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Quan_tri_vien_mo_cua_ho_thi_ghi_AUDIT_LOG()
    {
        // Lối thoát hiểm có thật (HM kẹt họp, ứng viên đã đến văn phòng) nhưng phải để lại dấu vết —
        // cùng khuôn với vượt cổng duyệt shortlist ở ADR-061.
        var (uow, _, session) = Seed();
        var adminId = Guid.NewGuid();

        await Join(uow, session.Id, actor: adminId, role: AppRoles.HrAdmin);
        var res = await Admit(uow, session.Id, actor: adminId, role: AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "interview_admitted_without_hm");
    }

    [Fact]
    public async Task HM_dung_nguoi_thi_KHONG_ghi_audit_vuot_cong()
    {
        var (uow, _, session) = Seed();

        await Join(uow, session.Id);
        await Admit(uow, session.Id);

        Assert.DoesNotContain(uow.Repo<AuditLog>().Items, a => a.Action == "interview_admitted_without_hm");
    }

    // ---------- Danh sách phòng ----------

    [Fact]
    public async Task Danh_sach_phong_gom_ca_phien_dang_dien_ra()
    {
        // HM đóng nhầm tab giữa buổi vẫn phải tìm được đường quay lại phòng.
        var (uow, _, session) = Seed(status: InterviewSessionStatuses.Active);

        var res = await new GetWaitingRoomsQueryHandler(uow)
            .Handle(new GetWaitingRoomsQuery(_ownerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Contains(res.Value, r => r.SessionId == session.Id);
    }

    [Fact]
    public async Task Danh_sach_phong_khong_lo_phien_ngoai_pham_vi()
    {
        var (uow, _, _) = Seed();

        var res = await new GetWaitingRoomsQueryHandler(uow)
            .Handle(new GetWaitingRoomsQuery(Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task Phien_da_ket_thuc_thi_khong_vao_phong_duoc()
    {
        var (uow, _, session) = Seed(status: InterviewSessionStatuses.Completed);

        var res = await Join(uow, session.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("đã kết thúc", res.Error);
    }
}
