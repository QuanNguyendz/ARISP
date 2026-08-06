using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.InterviewCodes;

/// <summary>
/// Bảng mã của HR theo job (UC-67, <c>InterviewCodeService.GetCodesByJobAsync</c>): gộp mã của mọi hồ sơ
/// thuộc job, gắn tên ứng viên + trạng thái Active/Used/Expired.
/// </summary>
public class GetCodesByJobTests
{
    private static ARI.Application.Services.InterviewCodeService Svc(InMemoryUnitOfWork uow)
        => InterviewCodeData.Service(uow, new RecordingNotificationService(), new FakeTokenService());

    [Fact]
    public async Task Returns_summaries_with_status_and_candidate_name()
    {
        var uow = new InMemoryUnitOfWork();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id, name: "Trần Văn B");
        uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Code(app.Id, "ACTIVE1", expiresAt: DateTimeOffset.UtcNow.AddHours(2)))
            .Seed(InterviewCodeData.Code(app.Id, "USEDXX", usedAt: DateTimeOffset.UtcNow.AddMinutes(-10)))
            .Seed(InterviewCodeData.Code(app.Id, "EXPIRE", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));

        var list = await Svc(uow).GetCodesByJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(3, list.Count);
        Assert.All(list, c => Assert.Equal("Trần Văn B", c.CandidateName));
        Assert.Equal("Active", list.Single(c => c.Code == "ACTIVE1").Status);
        Assert.Equal("Used", list.Single(c => c.Code == "USEDXX").Status);
        Assert.Equal("Expired", list.Single(c => c.Code == "EXPIRE").Status);
    }

    [Fact]
    public async Task Only_includes_codes_of_this_job()
    {
        var uow = new InMemoryUnitOfWork();
        var job = InterviewCodeData.Job();
        var otherJob = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        var otherApp = InterviewCodeData.App(otherJob.Id);
        uow.Seed(job, otherJob).Seed(app, otherApp)
            .Seed(InterviewCodeData.Code(app.Id, "MINE12"))
            .Seed(InterviewCodeData.Code(otherApp.Id, "OTHER1"));

        var list = await Svc(uow).GetCodesByJobAsync(job.Id, CancellationToken.None);

        Assert.Equal("MINE12", Assert.Single(list).Code);
    }

    [Fact]
    public async Task Empty_when_job_has_no_codes()
    {
        var uow = new InMemoryUnitOfWork();
        var job = InterviewCodeData.Job();
        uow.Seed(job).Seed(InterviewCodeData.App(job.Id));

        var list = await Svc(uow).GetCodesByJobAsync(job.Id, CancellationToken.None);

        Assert.Empty(list);
    }
}
