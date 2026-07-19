using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Domain.UnitTests.Entities;

/// <summary>
/// Skeleton test Domain — chốt cứng các default/hằng số mà DB và JWT claims phụ thuộc
/// (đổi giá trị AppRoles là vỡ role check của mọi token đã phát hành).
/// </summary>
public class EntityDefaultsTests
{
    [Fact]
    public void AppRoles_values_are_stable()
    {
        Assert.Equal("Super_admin", AppRoles.SuperAdmin);
        Assert.Equal("Hr_admin", AppRoles.HrAdmin);
        Assert.Equal("Recruiter", AppRoles.Recruiter);
        Assert.Equal("Candidate", AppRoles.Candidate);
    }

    [Fact]
    public void New_user_defaults_active_recruiter()
    {
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.True(user.IsActive);
        Assert.Equal("recruiter", user.Role);
        Assert.Null(user.DeletedAt);
    }

    [Fact]
    public void New_job_posting_is_soft_deletable()
    {
        Assert.IsAssignableFrom<ISoftDelete>(new JobPosting());
    }
}
