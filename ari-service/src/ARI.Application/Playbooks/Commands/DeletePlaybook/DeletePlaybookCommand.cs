using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Playbooks.Commands.DeletePlaybook
{
    /// <summary>Xoá mềm một playbook VÀ gỡ chunk của nó khỏi kho vector.</summary>
    public record DeletePlaybookCommand(Guid Id) : IRequest<Result>;

    public class DeletePlaybookCommandHandler : IRequestHandler<DeletePlaybookCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIngestionService _ragIngestion;

        public DeletePlaybookCommandHandler(IUnitOfWork unitOfWork, IRagIngestionService ragIngestion)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
        }

        public async Task<Result> Handle(DeletePlaybookCommand request, CancellationToken ct)
        {
            var doc = await _unitOfWork.Repository<PlaybookDocument>().GetByIdAsync(request.Id, ct);
            if (doc == null)
                return Result.Failure("Không tìm thấy playbook.", CommonErrorCodes.NotFound);

            // Gỡ chunk TRƯỚC khi xoá mềm. Bản cũ chỉ set DeletedAt nên tài liệu biến mất khỏi màn hình
            // nhưng chunk vẫn nằm trong kho vector và tiếp tục được truy hồi ở mọi buổi phỏng vấn —
            // xoá mà không hết ảnh hưởng là kiểu hỏng im lặng tệ nhất.
            //
            // Ingest với text rỗng = "tài liệu không còn nội dung": rag-service xoá sạch chunk cũ của
            // (source_type, source_id) rồi ghi 0 chunk (app/rag/ingest.py) — dùng lại đúng đường đã có,
            // không đẻ thêm endpoint xoá riêng.
            try
            {
                await _ragIngestion.IngestAsync(
                    sourceType: "playbook", sourceId: doc.Id, text: string.Empty, ct: ct);
            }
            catch (Exception ex)
            {
                // KHÔNG nuốt lỗi: thứ tự này giữ trạng thái nhất quán (tài liệu vẫn hiện, chunk vẫn còn)
                // để nhân sự bấm lại, thay vì để lại một playbook vô hình vẫn điều khiển AI.
                return Result.Failure(
                    $"Không gỡ được nội dung playbook khỏi kho tri thức của AI: {ex.Message}. Vui lòng thử lại.",
                    CommonErrorCodes.ServerError);
            }

            doc.DeletedAt = DateTimeOffset.UtcNow;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<PlaybookDocument>().Update(doc);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }
}
