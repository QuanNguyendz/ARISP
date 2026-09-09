using System;
using System.Collections.Generic;

namespace ARI.Application.Admin
{
    // ===== Request bodies (giữ nguyên tên class — trùng schema swagger cũ) =====

    public class UpdateSettingItem
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateDepartmentRequest
    {
        /// <summary><c>null</c> = gỡ khỏi đội (người rời công ty, đội giải thể).</summary>
        public Guid? DepartmentId { get; set; }
    }

    public class CreateStaffUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "recruiter"; // hr_admin | recruiter

        /// <summary>Đội/bộ phận (ADR-065) — khoá ngoại, không còn là chuỗi gõ tay.</summary>
        public Guid? DepartmentId { get; set; }
    }

    public class DeactivateUserRequest
    {
        public string? Reason { get; set; }
    }

    public class RejectRequest
    {
        public string? Reason { get; set; }
    }

    // ===== Response DTOs (property khớp shape anonymous cũ — camelCase khi serialize) =====

    public record PendingUserDto(Guid Id, string Email, string Role, string? FullName, DateTimeOffset CreatedAt);

    public record StaffUserListItemDto(
        Guid Id, string Email, string? FullName, string Role, bool IsActive, string? LockReason, DateTimeOffset CreatedAt,

        // ADR-065: đội là khoá ngoại, nhưng bảng vẫn cần TÊN để hiển thị. Trả cả hai: `DepartmentId`
        // cho ô chọn, `Department` để hiện — không lưu tên trong `users` (hai nguồn cho một sự thật
        // là kiểu trôi lệch mà ADR-058 đã phải đi chữa).
        Guid? DepartmentId = null, string? Department = null);

    public record CreatedStaffUserDto(
        Guid Id, string Email, string? FullName, string Role, string? Department, bool IsActive, DateTimeOffset CreatedAt);

    /// <summary>Trang kết quả kiểu admin: { totalCount, page, pageSize, totalPages, items }.</summary>
    public record PagedListDto<T>(int TotalCount, int Page, int PageSize, int TotalPages, IReadOnlyList<T> Items);

    public record AdminStatsDto(
        int TotalUsers, int ActiveUsers, int LockedUsers, int PendingRequests,
        int SuperAdmins, int HrAdmins, int Recruiters, int HiringManagers, int Candidates);

    public record AuditLogItemDto(
        Guid Id, string Action, string? EntityType, Guid? EntityId, string? Metadata, string ActorName, DateTimeOffset CreatedAt);

    public record SettingDto(string Key, string Value, string? Description, DateTimeOffset UpdatedAt);

    public record AccountRequestListItemDto(
        Guid Id, Guid? BatchId, string Email, string FullName, string Role, string? Department,
        string Status, string? ReviewReason, string RequestedBy, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);
}
