using System;

namespace ARI.Application.AccountRequests
{
    /// <summary>Một mục trong request tạo tài khoản hàng loạt (giữ nguyên tên class — trùng schema swagger cũ).</summary>
    public class AccountRequestItem
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "recruiter";
        public string? Department { get; set; }
    }

    /// <summary>Yêu cầu của chính HR Leader hiện tại (không có cột RequestedBy).</summary>
    public record MyAccountRequestItemDto(
        Guid Id, Guid? BatchId, string Email, string FullName, string Role, string? Department,
        string Status, string? ReviewReason, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);

    public record CreateAccountRequestsResultDto(int Count, Guid? BatchId);
}
