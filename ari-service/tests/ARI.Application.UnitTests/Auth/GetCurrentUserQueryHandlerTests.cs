using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Queries.GetCurrentUser;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Người dùng hiện tại từ claims (<see cref="GetCurrentUserQueryHandler"/>): tra fullName theo loại tài khoản
/// (staff ↔ User, candidate ↔ CandidateAccount), echo Id/Email/Role; id lạ → tên rỗng (không lỗi).
/// </summary>
public class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Staff_role_resolves_full_name_from_user()
    {
        var staff = AuthData.Staff(email: "hr@example.io", role: AppRoles.HrAdmin, fullName: "HR Boss");
        var uow = new InMemoryUnitOfWork().Seed(staff);

        var res = await new GetCurrentUserQueryHandler(uow)
            .Handle(new GetCurrentUserQuery(staff.Id.ToString(), "hr@example.io", AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(staff.Id.ToString(), res.Value.Id);
        Assert.Equal("hr@example.io", res.Value.Email);
        Assert.Equal("HR Boss", res.Value.Name);
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role);
    }

    [Fact]
    public async Task Candidate_role_resolves_full_name_from_candidate()
    {
        var cand = AuthData.Candidate(email: "cand@example.io", fullName: "Nguyen Van A");
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await new GetCurrentUserQueryHandler(uow)
            .Handle(new GetCurrentUserQuery(cand.Id.ToString(), "cand@example.io", AppRoles.Candidate), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Nguyen Van A", res.Value.Name);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
    }

    [Fact]
    public async Task Unknown_id_returns_empty_name()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetCurrentUserQueryHandler(uow)
            .Handle(new GetCurrentUserQuery(Guid.NewGuid().ToString(), "x@example.io", AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Name);        // không tìm thấy user → tên rỗng, vẫn Success
        Assert.Equal("x@example.io", res.Value.Email);
    }
}
