using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.RecruitmentRequests;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.JdDocuments
{
    /// <summary>
    /// Rút danh sách <b>kỹ năng yêu cầu</b> từ bản JD đã soạn, để màn tạo tin điền sẵn ô "Kỹ năng
    /// yêu cầu" thay vì bắt Recruiter gõ lại từng thẻ.
    ///
    /// <b>Vì sao chỗ NÀY cần AI còn phần mô tả thì không.</b> Nội dung JD vốn đã có cấu trúc theo
    /// từng mục (<c>JdDocument.Sections</c>), nên dựng phần mô tả công việc chỉ là ghép đúng mục —
    /// không phải đoán. Nhưng "kỹ năng" là những thẻ ngắn (<c>C#</c>, <c>ReactJS</c>, <c>MongoDB</c>)
    /// nằm rải trong văn xuôi của mục Yêu cầu; muốn có chúng thì phải SUY RA, và đó đúng là việc
    /// của mô hình ngôn ngữ.
    ///
    /// Dùng lại <see cref="IGeminiProvider.ExtractJobFromJdAsync"/> qua đường <c>fallbackJdText</c>
    /// (ADR-042) chứ không dựng thêm lời gọi AI mới: cùng một prompt đã kiểm chứng, cùng chỗ xử lý
    /// lỗi và trần thời gian.
    /// </summary>
    public record ExtractJdSkillsCommand(Guid RecruitmentRequestId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<JdSkillSuggestionDto>>;

    /// <summary>
    /// Gợi ý điền sẵn. <see cref="Skills"/> rỗng KHÔNG phải lỗi — bản JD có thể không nêu công nghệ
    /// nào cụ thể, và màn tạo tin chỉ việc để người dùng tự gõ như trước.
    /// </summary>
    public record JdSkillSuggestionDto(List<string> Skills, string? JobCategory);

    public class ExtractJdSkillsCommandHandler
        : IRequestHandler<ExtractJdSkillsCommand, Result<JdSkillSuggestionDto>>
    {
        /// <summary>Nhiều hơn chừng này thì không còn là "kỹ năng chính" mà là chép cả mục yêu cầu.</summary>
        private const int MaxSkills = 15;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGeminiProvider _gemini;

        public ExtractJdSkillsCommandHandler(IUnitOfWork unitOfWork, IGeminiProvider gemini)
        {
            _unitOfWork = unitOfWork;
            _gemini = gemini;
        }

        public async Task<Result<JdSkillSuggestionDto>> Handle(
            ExtractJdSkillsCommand request, CancellationToken ct)
        {
            // Cùng cổng phạm vi với mọi thao tác khác trên bản JD — mỗi lượt gọi là một lượt Gemini
            // có tính phí, không mở rộng hơn người được soạn JD.
            var (req, error, code) = await RecruitmentRequestAccess.LoadExecutableAsync(
                _unitOfWork, request.RecruitmentRequestId, request.ActorId, request.ActorRole, ct);

            if (req == null)
                return code == null
                    ? Result.Failure<JdSkillSuggestionDto>(error!)
                    : Result.Failure<JdSkillSuggestionDto>(error!, code);

            var doc = await JdDocumentSupport.GetAsync(_unitOfWork, req.Id, ct);
            if (doc == null)
                return Result.Failure<JdSkillSuggestionDto>("Phiếu này chưa có bản mô tả công việc.");

            var text = ComposePlainText(doc);
            if (text.Length < 40)
                // Chưa đủ chữ để suy ra gì — trả rỗng thay vì đốt một lượt Gemini chắc chắn vô ích.
                return Result.Success(new JdSkillSuggestionDto(new List<string>(), null));

            var result = await _gemini.ExtractJobFromJdAsync(null, null, text, ct);

            // AI hỏng KHÔNG được làm hỏng việc tạo tin: đây là bước điền sẵn cho tiện, người dùng
            // vẫn gõ tay được. Trả danh sách rỗng và để màn hình đi tiếp.
            if (result.IsFailure || result.Value is not { } extracted)
                return Result.Success(new JdSkillSuggestionDto(new List<string>(), null));

            return Result.Success(new JdSkillSuggestionDto(
                Clean(extracted.Skills),
                string.IsNullOrWhiteSpace(extracted.JobCategory) ? null : extracted.JobCategory));
        }

        /// <summary>
        /// Ghép bản JD thành văn bản thuần cho AI đọc. Có kèm TIÊU ĐỀ MỤC vì chính chúng nói cho mô
        /// hình biết đoạn nào là yêu cầu, đoạn nào là quyền lợi — bỏ đi thì kỹ năng dễ bị nhặt nhầm
        /// từ phần mô tả phúc lợi.
        /// </summary>
        private static string ComposePlainText(JdDocument doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine(doc.Title);
            if (!string.IsNullOrWhiteSpace(doc.ExperienceLevel)) sb.AppendLine($"Cấp bậc: {doc.ExperienceLevel}");

            foreach (var (key, value) in JdLayout.ParseContent(doc.SectionsJson))
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                sb.AppendLine();
                sb.AppendLine(key);
                sb.AppendLine(value.Trim());
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Dọn danh sách AI trả về: bỏ trống, bỏ trùng (không phân biệt hoa thường), cắt những thẻ
        /// dài như một câu, và giới hạn số lượng.
        /// </summary>
        private static List<string> Clean(List<string>? skills) =>
            (skills ?? new List<string>())
                .Select(s => (s ?? string.Empty).Trim().Trim(',', '.', ';'))
                .Where(s => s.Length is > 0 and <= 40)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxSkills)
                .ToList();
    }
}
