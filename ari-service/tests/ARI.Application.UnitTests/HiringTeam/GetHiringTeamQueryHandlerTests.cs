using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.HiringTeam;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.HiringTeam;

/// <summary>
/// Xem đội tuyển dụng của một tin (<see cref="GetHiringTeamQueryHandler"/>, ADR-061): cổng đọc ở ngưỡng
/// TeamMember (thành viên đội thấy được chính đội mình), Hiring Manager chính xếp lên đầu.
/// </summary>
public class GetHiringTeamQueryHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static (InMemoryUnitOfWork uow, JobPosting job) Seed()
    {
        var job = new JobPosting { Id = Guid.NewGuid(), Title = "Backend Developer", CreatedByUserId = OwnerId, Status = "active" };
        var uow = new InMemoryUnitOfWork()
            .Seed(job)
            .Seed(new User { Id = OwnerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true });
        return (uow, job);
    }

    private static Task<Result<System.Collections.Generic.List<HiringTeamMemberDto>>> Run(
        InMemoryUnitOfWork uow, Guid jobId, Guid actor, string role)
        => new GetHiringTeamQueryHandler(uow).Handle(new GetHiringTeamQuery(jobId, actor, role), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid(), OwnerId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Outsider_forbidden()
    {
        var (uow, job) = Seed();

        var res = await Run(uow, job.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Owner_sees_empty_team()
    {
        var (uow, job) = Seed();

        var res = await Run(uow, job.Id, OwnerId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID04_Primary_hm_listed_first()
    {
        var (uow, job) = Seed();
        var hmId = Guid.NewGuid();
        var interviewerId = Guid.NewGuid();
        uow.Seed(new User { Id = hmId, Email = "hm@corp.io", FullName = "HM Chinh", Role = RoleNames.HiringManager, IsActive = true })
           .Seed(new User { Id = interviewerId, Email = "iv@corp.io", FullName = "Interviewer", Role = RoleNames.HiringManager, IsActive = true })
           .Seed(new JobHiringTeamMember
           {
               Id = Guid.NewGuid(), JobPostingId = job.Id, UserId = interviewerId, RoleOnJob = JobTeamRoles.Interviewer,
               IsPrimary = false, AddedByUserId = OwnerId, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
           })
           .Seed(new JobHiringTeamMember
           {
               Id = Guid.NewGuid(), JobPostingId = job.Id, UserId = hmId, RoleOnJob = JobTeamRoles.HiringManager,
               IsPrimary = true, AddedByUserId = OwnerId, CreatedAt = DateTimeOffset.UtcNow,
           });

        var res = await Run(uow, job.Id, OwnerId, AppRoles.Recruiter);

        Assert.Equal(2, res.Value.Count);
        Assert.True(res.Value[0].IsPrimary);          // HM chính lên đầu bất kể thêm sau
        Assert.Equal(hmId, res.Value[0].UserId);
        Assert.Equal("HM Chinh", res.Value[0].FullName);
    }

    [Fact]
    public async Task UTCID05_Team_member_sees_own_team()
    {
        var (uow, job) = Seed();
        var hmId = Guid.NewGuid();
        uow.Seed(new User { Id = hmId, Email = "hm@corp.io", Role = RoleNames.HiringManager, IsActive = true })
           .Seed(new JobHiringTeamMember
           {
               Id = Guid.NewGuid(), JobPostingId = job.Id, UserId = hmId, RoleOnJob = JobTeamRoles.HiringManager,
               IsPrimary = true, AddedByUserId = OwnerId,
           });

        var res = await Run(uow, job.Id, hmId, AppRoles.HiringManager);

        Assert.True(res.IsSuccess);
        Assert.Contains(res.Value, m => m.UserId == hmId);
    }
}
