using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Departments;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Departments;

/// <summary>
/// Danh sách đội/bộ phận (<see cref="GetDepartmentsQueryHandler"/>, ADR-065): sắp theo tên, đếm số nhân sự mỗi đội
/// bằng một truy vấn gộp, và <c>ActiveOnly</c> lọc đội đã giải thể (tắt, không xoá).
/// </summary>
public class GetDepartmentsQueryHandlerTests
{
    private static Department Dept(string name, bool active = true, string? code = null)
        => new() { Id = Guid.NewGuid(), Name = name, Code = code, IsActive = active, CreatedAt = DateTimeOffset.UtcNow };

    private static User InDept(Guid deptId)
        => new() { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@corp.io", Role = AppRoles.Recruiter, DepartmentId = deptId };

    [Fact]
    public async Task UTCID01_No_departments_returns_empty()
    {
        var res = await new GetDepartmentsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetDepartmentsQuery(ActiveOnly: false), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID02_Ordered_by_name()
    {
        var uow = new InMemoryUnitOfWork().Seed(Dept("Backend"), Dept("AI"), Dept("Mobile"));

        var res = await new GetDepartmentsQueryHandler(uow)
            .Handle(new GetDepartmentsQuery(ActiveOnly: false), CancellationToken.None);

        Assert.Equal(new[] { "AI", "Backend", "Mobile" }, res.Value.Select(d => d.Name).ToArray());
    }

    [Fact]
    public async Task UTCID03_Member_count_grouped()
    {
        var eng = Dept("Engineering");
        var uow = new InMemoryUnitOfWork()
            .Seed(eng)
            .Seed(InDept(eng.Id), InDept(eng.Id))
            .Seed(new User { Id = Guid.NewGuid(), Email = "no-dept@corp.io", Role = AppRoles.Recruiter, DepartmentId = null });

        var res = await new GetDepartmentsQueryHandler(uow)
            .Handle(new GetDepartmentsQuery(ActiveOnly: false), CancellationToken.None);

        Assert.Equal(2, res.Value.Single().MemberCount);
    }

    [Fact]
    public async Task UTCID04_ActiveOnly_excludes_disbanded()
    {
        var uow = new InMemoryUnitOfWork().Seed(Dept("Live"), Dept("Disbanded", active: false));

        var res = await new GetDepartmentsQueryHandler(uow)
            .Handle(new GetDepartmentsQuery(ActiveOnly: true), CancellationToken.None);

        Assert.Equal("Live", Assert.Single(res.Value).Name);
    }

    [Fact]
    public async Task UTCID05_ActiveOnly_false_includes_disbanded()
    {
        var uow = new InMemoryUnitOfWork().Seed(Dept("Live"), Dept("Disbanded", active: false));

        var res = await new GetDepartmentsQueryHandler(uow)
            .Handle(new GetDepartmentsQuery(ActiveOnly: false), CancellationToken.None);

        Assert.Equal(2, res.Value.Count);
        Assert.Contains(res.Value, d => !d.IsActive);
    }

    [Fact]
    public async Task UTCID06_Query_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<Department>("Department DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(() => new GetDepartmentsQueryHandler(uow)
            .Handle(new GetDepartmentsQuery(ActiveOnly: false), CancellationToken.None));
        Assert.Equal("Department DB Error", ex.Message);
    }
}
