using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.RecruitmentRequests
{
    /// <summary>
    /// Ai được THỰC THI một phiếu đã duyệt — dựng JD (ADR-064) và dựng tin (ADR-063).
    ///
    /// Tách ra vì hai chỗ phải trả lời cùng một câu hỏi. Để mỗi bên tự viết lại thì chỉ cần một bên
    /// được nới là quyền rò qua đường đó: dựng được JD nhưng không dựng được tin (hoặc ngược lại) là
    /// một trạng thái vô nghĩa mà vẫn biểu diễn được.
    /// </summary>
    public static class RecruitmentRequestAccess
    {
        /// <summary>
        /// <c>true</c> nếu người gọi được thực thi phiếu này: đúng Recruiter được phân công, hoặc
        /// quản trị viên (HR Leader / Super Admin) vận hành hộ.
        /// </summary>
        public static bool CanExecute(RecruitmentRequest request, Guid? actorId, string? actorRole) =>
            RoleNames.IsAdmin(actorRole) || (actorId is { } id && request.AssignedRecruiterId == id);

        /// <summary>
        /// Lấy phiếu ĐÃ DUYỆT mà người gọi được thực thi. Trả lý do từ chối dạng
        /// <c>(request, error, errorCode)</c> để nơi gọi dựng <c>Result&lt;T&gt;</c> đúng kiểu của mình.
        ///
        /// Phiếu giao cho người khác trả **403 kèm câu nói rõ**, không phải 404: cả ba vai trò nội
        /// bộ đều đã liệt kê được phiếu trong phạm vi của mình, nên giấu sự tồn tại của phiếu chẳng
        /// bảo vệ được gì mà lại khiến người dùng không hiểu vì sao mình không làm được.
        /// </summary>
        public static async Task<(RecruitmentRequest? Request, string? Error, string? ErrorCode)> LoadExecutableAsync(
            IUnitOfWork uow, Guid requestId, Guid? actorId, string? actorRole, CancellationToken ct)
        {
            var found = await uow.Repository<RecruitmentRequest>()
                .FindAsync(r => r.Id == requestId && r.DeletedAt == null, ct);
            var request = found.FirstOrDefault();

            if (request == null)
                return (null, "Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            if (!RecruitmentRequestStatus.Is(request.Status, RecruitmentRequestStatus.Approved))
                return (null, "Phiếu yêu cầu tuyển dụng này chưa được HR Leader duyệt.", null);

            if (!CanExecute(request, actorId, actorRole))
                return (null, "Phiếu này được phân công cho Recruiter khác.", CommonErrorCodes.Forbidden);

            return (request, null, null);
        }
    }
}
