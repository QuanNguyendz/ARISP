using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Queries.GetCurrentUser;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Thông tin người dùng hiện tại từ claims (<see cref="GetCurrentUserQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetCurrentUser" (UTCID01–11): chọn repository theo role, parse Guid, map claim null, và lỗi phụ thuộc.
/// </summary>
public class GetCurrentUserQueryHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private const string StaffRole = "Recruiter";

    private static GetCurrentUserQueryHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static CandidateAccount Candidate(string fullName = "Candidate User")
    {
        var c = AuthData.Candidate(email: "candidate@example.com", fullName: fullName);
        c.Id = CandidateId;
        return c;
    }

    private static User Staff(string? fullName = "Staff User")
    {
        var u = AuthData.Staff(email: "staff@example.com", fullName: fullName);
        u.Id = StaffId;
        return u;
    }

    // UTCID01 — candidate tồn tại → Name lấy từ CandidateAccount
    [Fact]
    public async Task UTCID01_Candidate_exists()
    {
        var uow = new InMemoryUnitOfWork().Seed(Candidate());
        var res = await Handler(uow).Handle(new GetCurrentUserQuery(CandidateId.ToString(), "candidate@example.com", AppRoles.Candidate), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(CandidateId.ToString(), res.Value.Id);
        Assert.Equal("candidate@example.com", res.Value.Email);
        Assert.Equal("Candidate User", res.Value.Name);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
    }

    // UTCID02 — staff tồn tại → Name lấy từ User
    [Fact]
    public async Task UTCID02_Staff_exists()
    {
        var uow = new InMemoryUnitOfWork().Seed(Staff());
        var res = await Handler(uow).Handle(new GetCurrentUserQuery(StaffId.ToString(), "staff@example.com", StaffRole), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("Staff User", res.Value.Name);
        Assert.Equal(StaffRole, res.Value.Role);
    }

    // UTCID03 — candidate không tồn tại → Name=""
    [Fact]
    public async Task UTCID03_Candidate_missing()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetCurrentUserQuery(CandidateId.ToString(), "candidate@example.com", AppRoles.Candidate), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Name);
    }

    // UTCID04 — staff không tồn tại → Name=""
    [Fact]
    public async Task UTCID04_Staff_missing()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetCurrentUserQuery(StaffId.ToString(), "staff@example.com", StaffRole), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Name);
    }

    // UTCID05 — UserId candidate không parse được Guid → Name="", repository không được gọi
    [Fact]
    public async Task UTCID05_Invalid_candidate_id()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("should-not-be-called");
        var res = await Handler(uow).Handle(new GetCurrentUserQuery("invalid-id", "candidate@example.com", AppRoles.Candidate), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Name);
    }

    // UTCID06 — UserId staff không parse được Guid → Name="", repository không được gọi
    [Fact]
    public async Task UTCID06_Invalid_staff_id()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<User>("should-not-be-called");
        var res = await Handler(uow).Handle(new GetCurrentUserQuery("invalid-id", "staff@example.com", StaffRole), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Name);
    }

    // UTCID07 — Email claim null → Email=""
    [Fact]
    public async Task UTCID07_Null_email_claim()
    {
        var uow = new InMemoryUnitOfWork().Seed(Staff());
        var res = await Handler(uow).Handle(new GetCurrentUserQuery(StaffId.ToString(), null, StaffRole), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Email);
    }

    // UTCID08 — Role claim null → Role=""; dùng nhánh staff repository
    [Fact]
    public async Task UTCID08_Null_role_claim_uses_staff_repo()
    {
        var uow = new InMemoryUnitOfWork().Seed(Staff());
        var res = await Handler(uow).Handle(new GetCurrentUserQuery(StaffId.ToString(), "staff@example.com", null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.Role);
        Assert.Equal("Staff User", res.Value.Name);   // chứng minh dùng staff repo
    }

    // UTCID09 — Role không xác định → Role giữ nguyên; dùng nhánh staff repository
    [Fact]
    public async Task UTCID09_Unknown_role_uses_staff_repo()
    {
        var uow = new InMemoryUnitOfWork().Seed(Staff());
        var res = await Handler(uow).Handle(new GetCurrentUserQuery(StaffId.ToString(), "staff@example.com", "Unknown"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("Unknown", res.Value.Role);
        Assert.Equal("Staff User", res.Value.Name);
    }

    // UTCID10 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID10_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetCurrentUserQuery(CandidateId.ToString(), "candidate@example.com", AppRoles.Candidate), CancellationToken.None));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    // UTCID11 — staff lookup ném lỗi
    [Fact]
    public async Task UTCID11_Staff_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<User>("User DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetCurrentUserQuery(StaffId.ToString(), "staff@example.com", StaffRole), CancellationToken.None));
        Assert.Equal("User DB Error", ex.Message);
    }
}
