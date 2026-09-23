using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CvScoring;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Entities;

namespace ARI.Application.Dev
{
    /// <summary>
    /// Tin sandbox cũng phải có bộ tiêu chí chấm CV (ADR-070) — thiếu nó thì hồ sơ dev không bao giờ được
    /// chấm và tin không đăng lại được. Chỉ gieo khi tin chưa có bộ nào, để không chấm lại hồ sơ vô ích.
    /// </summary>
    internal static class DevCvRubricSeed
    {
        public static async Task EnsureAsync(IUnitOfWork uow, CvRubricService rubrics, JobPosting job, Guid actorId, CancellationToken ct)
        {
            if (await CvRubricStore.HasLiveAsync(uow, job.Id, ct)) return;

            var criteria = new List<RubricCriterion>
            {
                new()
                {
                    Key = "kinh_nghiem_backend", Name = "Kinh nghiệm backend .NET", Weight = 40,
                    Description = "Số năm làm sản phẩm thật với C#/.NET, độ liên quan với JD.",
                    Levels = new RubricLevels
                    {
                        Excellent = "Từ 4 năm .NET production, có dẫn dắt kỹ thuật.",
                        Good = "2–4 năm .NET production.",
                        Fair = "Dưới 2 năm hoặc chủ yếu dự án cá nhân.",
                        Poor = "Không có kinh nghiệm .NET thực tế.",
                    },
                    Checks = new List<RubricCheck>
                    {
                        new() { Key = "k1", Text = "Có ≥ 4 năm làm .NET production" },
                        new() { Key = "k2", Text = "Từng dẫn dắt kỹ thuật hoặc trưởng nhóm" },
                        new() { Key = "k3", Text = "Có hệ thống quy mô lớn (nhiều người dùng / giao dịch)" },
                        new() { Key = "k4", Text = "Có kết quả đo được (%, thời gian phản hồi, số người dùng)" },
                    },
                },
                new()
                {
                    Key = "ky_nang_bat_buoc", Name = "Kỹ năng bắt buộc", Weight = 35,
                    Description = "ASP.NET Core, EF Core, PostgreSQL, REST API — có bằng chứng dùng thực tế.",
                    Checks = new List<RubricCheck>
                    {
                        new() { Key = "k1", Text = "Dùng ASP.NET Core trong dự án thật" },
                        new() { Key = "k2", Text = "Dùng EF Core" },
                        new() { Key = "k3", Text = "Thiết kế / tối ưu cơ sở dữ liệu PostgreSQL" },
                        new() { Key = "k4", Text = "Xây dựng REST API" },
                    },
                },
                new()
                {
                    Key = "ky_nang_cong_them", Name = "Kỹ năng cộng thêm", Weight = 15,
                    Description = "CI/CD, Docker, SignalR/realtime.",
                },
                new()
                {
                    Key = "chat_luong_cv", Name = "Chất lượng trình bày CV", Weight = 10,
                    Description = "Rõ ràng, có số liệu kết quả, không lỗi trình bày.",
                },
            };

            // Công thức mặc định (ADR-075) — seed giữ đúng điểm như trước.
            await rubrics.SaveForJobAsync(job.Id, criteria, null, actorId, ct);
        }
    }
}
