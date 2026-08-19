using System;

namespace ARI.Domain.Entities
{
    public class PlaybookDocument : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Scope { get; set; } = "org"; // org | job_posting | round
        public Guid? ScopeRefId { get; set; }
        public int? RoundNumber { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileFormat { get; set; } = "txt"; // pdf | docx | txt | md | json
        public string? ParsedText { get; set; }

        /// <summary>
        /// Bộ tiêu chí chấm điểm đã parse + kiểm tổng trọng số = 100 (chỉ tài liệu loại
        /// <c>cv_rubric</c>/<c>interview_rubric</c>). Lưu dạng JSON mảng
        /// <c>[{key,name,weight,description}]</c> — ADR-060.
        /// Tách khỏi <see cref="ParsedText"/> vì đây là DỮ LIỆU dùng để tính điểm, còn ParsedText là
        /// văn bản cho RAG truy hồi; trộn hai thứ thì một lỗi định dạng sẽ làm hỏng cả việc chấm.
        /// </summary>
        public string? RubricJson { get; set; }
        public string Status { get; set; } = "processing"; // processing | ready | error
        public string? ErrorMessage { get; set; }
        public Guid UploadedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
