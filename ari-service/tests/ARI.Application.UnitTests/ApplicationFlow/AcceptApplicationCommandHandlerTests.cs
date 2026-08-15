using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Common;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Duyệt CV = duyệt + xếp lịch vòng 1 trong MỘT thao tác. Chốt: không có khung giờ thì không duyệt
/// được (trước đây duyệt suông là hợp lệ và ứng viên không nhận được email nào), khung giờ phải đúng
/// tin + vòng 1 + còn chỗ + chưa qua giờ, và duyệt thành công thì có booking + thư mời gửi đi.
/// </summary>
public class AcceptApplicationCommandHandlerTests
{
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    /// <summary>
    /// ISender tối giản: chỉ chuyển tiếp <see cref="AssignSlotCommand"/> sang handler thật để test
    /// đi hết đường duyệt → chốt chỗ → thư mời, đúng như lúc chạy thật.
    /// </summary>
    private sealed class AssignSlotSender : ISender
    {
        private readonly InMemoryUnitOfWork _uow;
        private readonly RecordingNotificationService _notif;

        public AssignSlotSender(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        {
            _uow = uow;
            _notif = notif;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is AssignSlotCommand cmd)
            {
                var result = await new AssignSlotCommandHandler(_uow, _notif, EmptyConfig).Handle(cmd, ct);
                return (TResponse)(object)result;
            }
            throw new NotSupportedException($"Test sender chưa hỗ trợ {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed record Ctx(
        InMemoryUnitOfWork Uow,
        RecordingNotificationService Notif,
        RecordingEmailService Email,
        JobPosting Job,
        ARI.Domain.Entities.Application App);

    private Ctx NewCtx(string appStatus = "cv_submitted", int capacity = 1, int booked = 0, bool pastSlot = false)
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: appStatus);
        var slot = SchedulingData.Slot(
            job.Id, round: 1, capacity: capacity, booked: booked,
            start: pastSlot ? SchedulingData.Past : SchedulingData.Future);
        uow.Seed(job).Seed(app).Seed(slot);

        // Chốt chỗ chạy bằng SQL thô — giả lập tăng booked_count như production.
        uow.OnExecuteSqlRaw = (_, parameters, _) =>
        {
            var target = uow.Repo<AvailabilitySlot>().Items.FirstOrDefault(s => s.Id.Equals(parameters[1]));
            if (target == null || target.BookedCount >= target.Capacity) return Task.FromResult(0);
            target.BookedCount++;
            return Task.FromResult(1);
        };

        return new Ctx(uow, new RecordingNotificationService(), new RecordingEmailService(), job, app);
    }

    private Task<Result<AcceptApplicationResultDto>> Run(Ctx c, Guid appId, Guid slotId)
        => new AcceptApplicationCommandHandler(
                ApplicationServiceFactory.Create(c.Uow, c.Notif, c.Email, new RecordingRagIngestionService()),
                c.Uow,
                new AssignSlotSender(c.Uow, c.Notif))
            .Handle(new AcceptApplicationCommand(appId, slotId, _staffId, AppRoles.Recruiter), CancellationToken.None);

    private Guid SlotId(Ctx c) => c.Uow.Repo<AvailabilitySlot>().Items[0].Id;

    [Fact]
    public async Task Accept_without_slot_is_rejected()
    {
        var c = NewCtx();

        var res = await Run(c, c.App.Id, Guid.Empty);

        Assert.True(res.IsFailure);
        Assert.Contains("chọn khung giờ", res.Error);
        Assert.Equal("cv_submitted", c.App.Status); // không duyệt nửa vời
        Assert.Empty(c.Uow.Repo<InterviewBooking>().Items);
    }

    [Fact]
    public async Task Accept_with_slot_books_and_sends_invitation()
    {
        var c = NewCtx();

        var res = await Run(c, c.App.Id, SlotId(c));

        Assert.True(res.IsSuccess);
        var booking = Assert.Single(c.Uow.Repo<InterviewBooking>().Items);
        Assert.Equal(res.Value.BookingId, booking.Id);
        Assert.Equal(1, booking.RoundNumber);
        Assert.Equal("scheduled", booking.Status);
        Assert.Equal("interview", c.App.Status); // cv_submitted → screening → interview
        Assert.Equal(1, c.Uow.Repo<AvailabilitySlot>().Items[0].BookedCount);
        Assert.Single(c.Notif.Emails); // thư mời kèm giờ hẹn
        Assert.Contains("Thư mời phỏng vấn vòng 1", c.Notif.Emails[0].Subject);
    }

    [Fact]
    public async Task Slot_of_another_job_is_rejected()
    {
        var c = NewCtx();
        var otherSlot = SchedulingData.Slot(Guid.NewGuid(), round: 1);
        c.Uow.Seed(otherSlot);

        var res = await Run(c, c.App.Id, otherSlot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("không thuộc vòng 1", res.Error);
        Assert.Equal("cv_submitted", c.App.Status);
    }

    [Fact]
    public async Task Slot_of_later_round_is_rejected()
    {
        var c = NewCtx();
        var round2 = SchedulingData.Slot(c.Job.Id, round: 2);
        c.Uow.Seed(round2);

        var res = await Run(c, c.App.Id, round2.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("không thuộc vòng 1", res.Error);
    }

    [Fact]
    public async Task Past_slot_is_rejected()
    {
        var c = NewCtx(pastSlot: true);

        var res = await Run(c, c.App.Id, SlotId(c));

        Assert.True(res.IsFailure);
        Assert.Contains("quá khứ", res.Error);
        Assert.Equal("cv_submitted", c.App.Status);
    }

    [Fact]
    public async Task Full_slot_is_rejected_before_approving()
    {
        var c = NewCtx(capacity: 1, booked: 1);

        var res = await Run(c, c.App.Id, SlotId(c));

        Assert.True(res.IsFailure);
        Assert.Contains("đã đầy", res.Error);
        Assert.Equal("cv_submitted", c.App.Status); // chưa duyệt → không rơi vào trạng thái "duyệt mà không có lịch"
        Assert.Empty(c.Notif.Emails);
    }

    [Fact]
    public async Task Unknown_application_is_not_found()
    {
        var c = NewCtx();

        var res = await Run(c, Guid.NewGuid(), SlotId(c));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }
}
