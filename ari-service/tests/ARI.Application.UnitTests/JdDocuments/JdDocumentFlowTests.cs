using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.JdDocuments;
using ARI.Application.JdTemplates;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JdDocuments;

/// <summary>
/// Trình soạn JD theo mẫu công ty (ADR-064): điền sẵn từ phiếu, phân quyền dùng chung với
/// <c>CreateJobCommand</c>, và xuất file gắn thẳng vào tin.
/// </summary>
public class JdDocumentFlowTests
{
    private readonly Guid _hmId = Guid.NewGuid();
    private readonly Guid _recruiterId = Guid.NewGuid();
    private readonly Guid _otherRecruiterId = Guid.NewGuid();
    private readonly Guid _hrLeaderId = Guid.NewGuid();

    private (InMemoryUnitOfWork Uow, RecruitmentRequest Request) Seed(
        string status = RecruitmentRequestStatus.Approved)
    {
        var uow = new InMemoryUnitOfWork();
        var req = new RecruitmentRequest
        {
            RequestedByUserId = _hmId,
            Title = "Intern Backend",
            DepartmentId = Guid.NewGuid(),
            Headcount = 2,
            Description = "Tham gia phát triển hệ thống\nLàm việc với cơ sở dữ liệu",
            Requirements = "Nắm chắc OOP\nBiết C#",
            SalaryMin = 3_000_000,
            SalaryCurrency = "VND",
            Status = status,
            AssignedRecruiterId = _recruiterId,
        };
        uow.Seed(req);
        return (uow, req);
    }

    private static Task<Result<JdDocumentDto>> Get(
        InMemoryUnitOfWork uow, Guid requestId, Guid actor, string role) =>
        new GetJdDocumentQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetJdDocumentQuery(requestId, actor, role), CancellationToken.None);

    private static Task<Result> Save(
        InMemoryUnitOfWork uow, Guid requestId, Guid actor, string role, JdDocumentInput input) =>
        new SaveJdDocumentCommandHandler(uow)
            .Handle(new SaveJdDocumentCommand(requestId, input, actor, role), CancellationToken.None);

    private static Task<Result<JdGeneratedFileDto>> Generate(
        InMemoryUnitOfWork uow, Guid requestId, Guid actor, string role,
        string format = "pdf", IFileStorageService? storage = null, IJdDocumentRenderer? renderer = null) =>
        new GenerateJdFileCommandHandler(uow, storage ?? new RecordingFileStorage(), renderer ?? new StubJdRenderer())
            .Handle(new GenerateJdFileCommand(requestId, format, actor, role), CancellationToken.None);

    private static JdDocumentInput Input(string? title = "Intern Backend") =>
        new(title!, "SDC3.BU3", "full_time", "onsite", "Hà Nội", "intern", 2,
            3_000_000, 5_000_000, "VND", null,
            new Dictionary<string, string> { [JdSectionKeys.Description] = "Viết API" });

    // ---------- Điền sẵn từ phiếu ----------

    [Fact]
    public async Task Mo_lan_dau_thi_dien_san_tu_phieu_chu_khong_phai_trang_trang()
    {
        // Đây là toàn bộ lý do ô "yêu cầu ứng viên" được thêm vào phiếu: hai ô HM đã điền rơi thẳng
        // vào hai mục đầu của JD.
        var (uow, req) = Seed();

        var res = await Get(uow, req.Id, _recruiterId, RoleNames.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("Intern Backend", res.Value.Title);
        Assert.Equal(2, res.Value.Vacancies);
        Assert.Contains("cơ sở dữ liệu", res.Value.Sections[JdSectionKeys.Description]);
        Assert.Contains("OOP", res.Value.Sections[JdSectionKeys.Requirements]);
    }

    [Fact]
    public async Task Chi_mo_xem_thi_KHONG_ghi_dong_nao_xuong_db()
    {
        // Ghi trong đường ĐỌC thì mở màn xem cũng đẻ dữ liệu — và unique index sẽ biến lần mở thứ
        // hai của người khác thành lỗi 500.
        var (uow, req) = Seed();

        await Get(uow, req.Id, _recruiterId, RoleNames.Recruiter);

        Assert.Empty(uow.Repo<JdDocument>().Items);
    }

    // ---------- Phân quyền: dùng chung với CreateJobCommand ----------

    [Fact]
    public async Task Recruiter_khac_khong_soan_duoc_jd_cua_phieu_nguoi_ta()
    {
        var (uow, req) = Seed();

        var res = await Get(uow, req.Id, _otherRecruiterId, RoleNames.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Admin_soan_ho_duoc()
    {
        var (uow, req) = Seed();

        var res = await Get(uow, req.Id, _hrLeaderId, RoleNames.HrAdmin);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Phieu_chua_duyet_thi_chua_soan_jd_duoc()
    {
        var (uow, req) = Seed(RecruitmentRequestStatus.Pending);

        var res = await Get(uow, req.Id, _recruiterId, RoleNames.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa được HR Leader duyệt", res.Error);
    }

    // ---------- Lưu ----------

    [Fact]
    public async Task Luu_hai_lan_van_chi_MOT_ban_jd_cho_moi_phieu()
    {
        var (uow, req) = Seed();

        Assert.True((await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, Input())).IsSuccess);
        Assert.True((await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, Input("Đổi tiêu đề"))).IsSuccess);

        var doc = Assert.Single(uow.Repo<JdDocument>().Items);
        Assert.Equal("Đổi tiêu đề", doc.Title);
    }

    [Fact]
    public async Task Muc_khong_thuoc_bo_khoa_bi_loai_khi_luu()
    {
        // Khoá lạ lọt vào thì renderer không bao giờ in ra, mà dữ liệu vẫn phình — im lặng và vô ích.
        var (uow, req) = Seed();
        var input = Input() with
        {
            Sections = new Dictionary<string, string>
            {
                [JdSectionKeys.Description] = "Viết API",
                ["khoa_bia_dat"] = "nội dung rác",
            },
        };

        await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, input);

        var doc = Assert.Single(uow.Repo<JdDocument>().Items);
        Assert.DoesNotContain("khoa_bia_dat", doc.SectionsJson);
        Assert.Contains("description", doc.SectionsJson);
    }

    // ---------- Xuất file ----------

    [Fact]
    public async Task Xuat_file_luu_vao_thu_muc_jd_va_ghi_lai_tren_ban_soan()
    {
        var (uow, req) = Seed();
        await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, Input());
        var storage = new RecordingFileStorage();

        var res = await Generate(uow, req.Id, _recruiterId, RoleNames.Recruiter, "pdf", storage);

        Assert.True(res.IsSuccess);
        Assert.Equal("pdf", res.Value.Format);
        Assert.EndsWith(".pdf", res.Value.FileName);

        // Cùng thư mục với file JD tải tay: từ góc nhìn của tin, đây CHÍNH LÀ file JD.
        Assert.Equal(StorageFolder.Jd, Assert.Single(storage.Saved).Folder);

        var doc = Assert.Single(uow.Repo<JdDocument>().Items);
        Assert.Equal(res.Value.StorageKey, doc.GeneratedFileStorageKey);
        Assert.NotNull(doc.GeneratedAt);
    }

    [Fact]
    public async Task Chua_luu_noi_dung_thi_chua_xuat_duoc_file()
    {
        var (uow, req) = Seed();

        var res = await Generate(uow, req.Id, _recruiterId, RoleNames.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("Chưa có nội dung JD", res.Error);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("")]
    [InlineData("PDF ")]   // có khoảng trắng thừa vẫn phải nhận
    public async Task Chi_nhan_dinh_dang_pdf_hoac_docx(string format)
    {
        var (uow, req) = Seed();
        await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, Input());

        var res = await Generate(uow, req.Id, _recruiterId, RoleNames.Recruiter, format);

        if (format.Trim().ToLowerInvariant() is "pdf" or "docx") Assert.True(res.IsSuccess);
        else Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Logo_hong_van_xuat_duoc_file()
    {
        // Một file ảnh lỗi không được phép làm chết thao tác "tải PDF" — người dùng sẽ không hiểu
        // vì sao, và cũng không có cách nào tự sửa.
        var (uow, req) = Seed();
        uow.Seed(new JdTemplate { CompanyName = "Eastern Sun", LogoStorageKey = "branding/logo.png" });
        await Save(uow, req.Id, _recruiterId, RoleNames.Recruiter, Input());

        var storage = new ThrowingReadStorage();

        var res = await Generate(uow, req.Id, _recruiterId, RoleNames.Recruiter, "pdf", storage);

        Assert.True(res.IsSuccess);
    }

    // ---------- Test double ----------

    private sealed class StubJdRenderer : IJdDocumentRenderer
    {
        public Task<byte[]> RenderDocxAsync(JdTemplate t, JdDocument d, byte[]? logo, CancellationToken ct = default)
            => Task.FromResult(Encoding.UTF8.GetBytes("PK-docx"));

        public Task<byte[]> RenderPdfAsync(JdTemplate t, JdDocument d, byte[]? logo, CancellationToken ct = default)
            => Task.FromResult(Encoding.UTF8.GetBytes("%PDF-stub"));
    }

    /// <summary>Đọc file ném lỗi — mô phỏng logo hỏng hoặc mất khỏi storage.</summary>
    private sealed class ThrowingReadStorage : IFileStorageService
    {
        public Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, StorageFolder folder, CancellationToken ct = default)
            => Task.FromResult($"{folder.ToSegment()}/{Guid.NewGuid()}-{originalFileName}");

        public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default)
            => Task.FromResult($"/files/{storageKey}");

        public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default)
            => Task.FromResult($"/files/{storageKey}");

        public Task DeleteAsync(string storageKey, CancellationToken ct = default) => Task.CompletedTask;

        public Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default)
            => throw new InvalidOperationException("storage down");
    }
}
