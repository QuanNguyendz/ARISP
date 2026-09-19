using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.InterviewRubrics;
using ARI.Application.Playbooks;
using ARI.Domain.Entities;

namespace ARI.Application.Dev
{
    /// <summary>
    /// Tin sandbox cũng phải có bộ tiêu chí chấm PHỎNG VẤN (ADR-073) — thiếu nó thì buổi phỏng vấn dev không
    /// bao giờ ra báo cáo và tin không đăng lại được. Chỉ gieo khi tin chưa có bộ chung nào; muốn thử nhánh
    /// "chờ bộ tiêu chí" thì xoá mềm bộ này trong DB dev.
    /// </summary>
    internal static class DevInterviewRubricSeed
    {
        public static async Task EnsureAsync(
            IUnitOfWork uow, InterviewRubricService rubrics, JobPosting job, Guid actorId, CancellationToken ct)
        {
            if (await InterviewRubricStore.JobLevelAsync(uow, job.Id, ct) != null) return;

            var criteria = new List<RubricCriterion>
            {
                new()
                {
                    Key = "kien_thuc_chuyen_mon", Name = "Kiến thức chuyên môn", Weight = 40,
                    Description = "Trả lời đúng và sâu các câu hỏi về .NET, cơ sở dữ liệu và thiết kế API.",
                    Levels = new RubricLevels
                    {
                        Excellent = "Giải thích đúng bản chất, nêu được đánh đổi và trường hợp biên.",
                        Good = "Trả lời đúng ý chính, có ví dụ nhưng chưa đi sâu.",
                        Fair = "Đúng một phần, còn nhầm lẫn khái niệm.",
                        Poor = "Sai hoặc không trả lời được.",
                    },
                },
                new()
                {
                    Key = "giai_quyet_van_de", Name = "Giải quyết vấn đề", Weight = 30,
                    Description = "Chia nhỏ vấn đề, đưa ra hướng xử lý hợp lý và giải thích lý do chọn.",
                },
                new()
                {
                    Key = "kinh_nghiem_thuc_te", Name = "Kinh nghiệm thực tế", Weight = 20,
                    Description = "Minh hoạ bằng ví dụ cụ thể từ dự án đã làm, có kết quả đo được.",
                },
                new()
                {
                    Key = "giao_tiep", Name = "Giao tiếp và trình bày", Weight = 10,
                    Description = "Trình bày mạch lạc, đúng trọng tâm câu hỏi.",
                },
            };

            await rubrics.SaveAsync(job.Id, null, criteria, actorId, ct);
        }
    }
}
