using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Emails
{
    // ============================================================
    // POST /api/emails/preview
    // ============================================================

    /// <summary>
    /// Dựng bản xem trước để nhân sự sửa trước khi gửi.
    ///
    /// ⚠ Endpoint này render nội dung có TÊN, EMAIL, GIỜ HẸN của ứng viên — tức là một đường đọc
    /// dữ liệu hồ sơ. Phải kiểm quyền trên tin y hệt endpoint đọc hồ sơ, nếu không nó thành lỗ
    /// rò vòng qua toàn bộ công sức siết phạm vi ở Phase 1.
    /// </summary>
    public record PreviewEmailQuery(string TemplateKey, Guid ContextId, Guid? SecondaryId, Guid? UserId, string? Role)
        : IRequest<Result<RenderedEmail>>;

    public class PreviewEmailQueryHandler : IRequestHandler<PreviewEmailQuery, Result<RenderedEmail>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailTemplateRenderer _renderer;

        public PreviewEmailQueryHandler(IUnitOfWork unitOfWork, IEmailTemplateRenderer renderer)
        {
            _unitOfWork = unitOfWork;
            _renderer = renderer;
        }

        public async Task<Result<RenderedEmail>> Handle(PreviewEmailQuery request, CancellationToken ct)
        {
            if (!EmailTemplateKeys.IsKnown(request.TemplateKey))
                return Result.Failure<RenderedEmail>(
                    $"Không biết mẫu thư '{request.TemplateKey}'.", CommonErrorCodes.NotFound);

            // Mọi mẫu thư hiện có đều lấy ngữ cảnh từ MỘT hồ sơ ứng tuyển.
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.ContextId, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden);

            return await _renderer.RenderAsync(request.TemplateKey, request.ContextId, request.SecondaryId, ct);
        }
    }

    // ============================================================
    // GET /api/applications/{id}/emails
    // ============================================================

    public class EmailLogItemDto
    {
        public Guid Id { get; set; }
        public string TemplateKey { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public bool WasEdited { get; set; }
        public string? SentByName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>Lịch sử thư đã gửi cho một ứng viên — tab "Lịch sử email" ở trang chi tiết hồ sơ.</summary>
    public record GetApplicationEmailsQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<List<EmailLogItemDto>>>;

    public class GetApplicationEmailsQueryHandler
        : IRequestHandler<GetApplicationEmailsQuery, Result<List<EmailLogItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetApplicationEmailsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<EmailLogItemDto>>> Handle(
            GetApplicationEmailsQuery request, CancellationToken ct)
        {
            // Đọc → ngưỡng TeamMember: Hiring Manager của tin xem được lịch sử trao đổi với ứng viên.
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.ApplicationId, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<List<EmailLogItemDto>>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<List<EmailLogItemDto>>(JobAccessErrors.ApplicationForbidden, CommonErrorCodes.Forbidden);

            var logs = (await _unitOfWork.Repository<EmailLog>()
                .FindAsync(e => e.ApplicationId == request.ApplicationId, ct))
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            var senderIds = logs.Where(l => l.SentByUserId.HasValue).Select(l => l.SentByUserId!.Value).Distinct().ToList();
            var senders = senderIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<User>()
                        .QueryAsync(q => q.Where(u => senderIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                    .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            return Result.Success(logs.Select(l => new EmailLogItemDto
            {
                Id = l.Id,
                TemplateKey = l.TemplateKey,
                ToEmail = l.ToEmail,
                Subject = l.Subject,
                BodyHtml = l.BodyHtml,
                WasEdited = l.WasEdited,
                SentByName = l.SentByUserId.HasValue && senders.TryGetValue(l.SentByUserId.Value, out var n) ? n : null,
                Status = l.Status,
                ErrorMessage = l.ErrorMessage,
                CreatedAt = l.CreatedAt,
            }).ToList());
        }
    }
}
