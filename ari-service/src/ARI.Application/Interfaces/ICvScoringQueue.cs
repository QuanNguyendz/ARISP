using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Việc chấm CV nền (ADR-070): hoặc một hồ sơ cụ thể, hoặc "rà lại mọi hồ sơ của tin này" (sau khi
    /// Hiring Manager lưu bộ tiêu chí mới).
    /// </summary>
    public record CvScoringWorkItem(Guid? ApplicationId, Guid? JobPostingId);

    /// <summary>
    /// Hàng đợi chấm CV — thay cho các <c>Task.Run</c> tự phát trước đây (mất việc khi service khởi động
    /// lại, không giới hạn số lượt gọi AI đồng thời). Hàng đợi chỉ là đường TẮT cho nhanh: lượt quét định
    /// kỳ của hosted service tự tìm lại mọi hồ sơ còn thiếu điểm, nên việc có rơi khỏi hàng cũng không mất.
    /// </summary>
    public interface ICvScoringQueue
    {
        /// <summary>Đưa hồ sơ vào hàng. Bỏ qua nếu hồ sơ đang nằm trong hàng.</summary>
        void EnqueueApplication(Guid applicationId);

        /// <summary>Yêu cầu rà lại mọi hồ sơ của tin.</summary>
        void EnqueueJob(Guid jobPostingId);

        ValueTask<CvScoringWorkItem> DequeueAsync(CancellationToken cancellationToken);

        /// <summary>Báo hồ sơ đã xử lý xong (thành công hay không) — cho phép đưa vào hàng lần nữa.</summary>
        void Complete(Guid applicationId);
    }
}
