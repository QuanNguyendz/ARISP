using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Dev.RegradeSession
{
    /// <summary>
    /// DEV-ONLY (ADR-053): chấm lại một phiên phỏng vấn đã kết thúc bằng prompt hiện tại.
    /// Dùng để kiểm chứng các bản sửa prompt (ngôn ngữ báo cáo, điểm từng câu, khoá tiêu chí)
    /// trên dữ liệu cũ mà không phải phỏng vấn lại từ đầu.
    /// </summary>
    public record RegradeSessionCommand(Guid SessionId, string? Language = null) : IRequest<Result<bool>>;

    public class RegradeSessionCommandHandler : IRequestHandler<RegradeSessionCommand, Result<bool>>
    {
        private readonly IInterviewService _interviewService;

        public RegradeSessionCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<bool>> Handle(RegradeSessionCommand request, CancellationToken ct)
            => _interviewService.RegenerateEvaluationAsync(request.SessionId, request.Language, ct);
    }
}
