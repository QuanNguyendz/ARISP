using System.Linq;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Domain.UnitTests.Constants;

/// <summary>
/// Hệ thống có HAI bộ từ vựng cho vai trò: giá trị DB (<see cref="RoleNames"/>) và giá trị claim
/// JWT (<see cref="AppRoles"/>). Lẫn lộn chúng đã làm màn "Phân công &amp; tải tuyển dụng" trả về
/// rỗng suốt một thời gian dài mà không có lỗi nào. Các bài test dưới đây chốt ranh giới đó.
/// </summary>
public class RoleNamesTests
{
    [Fact]
    public void Db_values_are_lowercase_snake_case()
    {
        // Cột users.role được ràng buộc CHECK theo đúng bốn giá trị này.
        Assert.Equal("super_admin", RoleNames.SuperAdmin);
        Assert.Equal("hr_admin", RoleNames.HrAdmin);
        Assert.Equal("recruiter", RoleNames.Recruiter);
        Assert.Equal("hiring_manager", RoleNames.HiringManager);
    }

    [Fact]
    public void Db_values_and_claim_values_are_deliberately_different()
    {
        // Nếu hai bộ này bằng nhau thì mọi phép so sánh nhầm sẽ "vô tình đúng" và lỗi casing
        // quay lại mà không ai biết. Sự khác nhau là có chủ ý.
        Assert.NotEqual(AppRoles.Recruiter, RoleNames.Recruiter);
        Assert.NotEqual(AppRoles.HrAdmin, RoleNames.HrAdmin);
    }

    [Theory]
    [InlineData("hr_admin", "Hr_admin")]
    [InlineData("Hr_admin", "Hr_admin")]   // đã là dạng claim → vẫn ra đúng
    [InlineData("HR_ADMIN", "Hr_admin")]
    [InlineData("  recruiter  ", "Recruiter")]
    [InlineData("super_admin", "Super_admin")]
    [InlineData("hiring_manager", "Hiring_manager")]
    public void ToClaim_maps_db_value_to_jwt_claim(string dbRole, string expected)
        => Assert.Equal(expected, RoleNames.ToClaim(dbRole));

    [Fact]
    public void ToClaim_passes_unknown_values_through_untouched()
    {
        // Dữ liệu lệch không được làm hỏng đăng nhập — token vẫn phát ra, chỉ là không khớp
        // policy nào nên người dùng không vào được khu vực nào. Ném lỗi ở đây sẽ biến một hàng
        // dữ liệu xấu thành sự cố đăng nhập toàn hệ thống.
        Assert.Equal("Pending", RoleNames.ToClaim("Pending"));
        Assert.Equal(string.Empty, RoleNames.ToClaim(null));
    }

    [Theory]
    [InlineData("hr_admin")]
    [InlineData("Hr_admin")]
    [InlineData("super_admin")]
    [InlineData("Super_admin")]
    public void IsAdmin_accepts_both_vocabularies(string role)
        => Assert.True(RoleNames.IsAdmin(role));

    [Theory]
    [InlineData("recruiter")]
    [InlineData("Recruiter")]
    [InlineData("hiring_manager")]
    [InlineData("Hiring_manager")]
    [InlineData(null)]
    [InlineData("")]
    public void IsAdmin_rejects_everyone_else(string? role)
    {
        // Hiring Manager KHÔNG phải quản trị viên: phạm vi của họ là đội tuyển dụng của từng tin,
        // không phải toàn bộ tin trong công ty.
        Assert.False(RoleNames.IsAdmin(role));
    }

    [Fact]
    public void NormalizeDbRole_returns_null_for_unrecognised_input()
    {
        Assert.Null(RoleNames.NormalizeDbRole("Pending"));
        Assert.Null(RoleNames.NormalizeDbRole("admin"));
        Assert.Null(RoleNames.NormalizeDbRole("   "));
        Assert.Equal("recruiter", RoleNames.NormalizeDbRole(" Recruiter "));
    }

    [Fact]
    public void Assignable_staff_excludes_super_admin()
    {
        // Quyền quản trị tối cao không cấp được qua màn quản lý tài khoản.
        Assert.DoesNotContain(RoleNames.SuperAdmin, RoleNames.AssignableStaff);
        Assert.All(RoleNames.AssignableStaff, r => Assert.Contains(r, RoleNames.All));
    }

    [Fact]
    public void Every_declared_role_maps_to_a_distinct_claim()
    {
        var claims = RoleNames.All.Select(RoleNames.ToClaim).ToList();
        Assert.Equal(claims.Count, claims.Distinct().Count());
        Assert.DoesNotContain(string.Empty, claims);
    }
}
