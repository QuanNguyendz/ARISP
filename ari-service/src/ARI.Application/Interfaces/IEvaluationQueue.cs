using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Hàng đợi sinh báo cáo đánh giá phỏng vấn (ADR-073) — thay cho việc gọi AI chấm NGAY trong lệnh đóng
    /// phiên. Hàng đợi chỉ là đường TẮT cho nhanh: nguồn sự thật là cột <c>interview_sessions.evaluation_status</c>,
    /// và lượt quét định kỳ tìm lại mọi phiên còn dang dở, nên việc có rơi khỏi hàng (khởi động lại, AI lỗi)
    /// cũng không mất báo cáo.
    /// </summary>
    public interface IEvaluationQueue
    {
        /// <summary>Đưa phiên vào hàng. Bỏ qua nếu phiên đang nằm trong hàng.</summary>
        void Enqueue(Guid sessionId);

        ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

        /// <summary>Báo phiên đã xử lý xong (thành công hay không) — cho phép đưa vào hàng lần nữa.</summary>
        void Complete(Guid sessionId);
    }
}
