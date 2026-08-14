using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.Realtime;
using ARI.Domain.Entities;
using ARI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

// Entity "Application" trùng tên với namespace ARI.Application — phải đặt bí danh.
using ApplicationEntity = ARI.Domain.Entities.Application;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Cầu nối realtime tầng database → SignalR (ADR-057).
    ///
    /// Giữ MỘT kết nối Postgres riêng chỉ để <c>LISTEN arisp_changes</c>. Mỗi khi có transaction ghi
    /// dữ liệu COMMIT, trigger trong DB phát NOTIFY; service này nhận, hỏi <see cref="DbChangeRouter"/>
    /// xem gửi cho ai, rồi đẩy qua <see cref="INotificationService"/> — tái dùng đúng hub và group
    /// mà tầng ứng dụng đang dùng, không thêm hub mới.
    ///
    /// Nhờ nguồn phát nằm ở DB, mọi đường ghi đều sinh sự kiện: EF, sửa SQL tay, rag-service Python,
    /// hosted service. Không còn cảnh "quên gọi Publish thì UI cũ âm thầm".
    /// </summary>
    public class DbChangeListenerHostedService : BackgroundService
    {
        // Kết nối lại theo backoff luỹ tiến khi DB rớt — không quay vòng dồn dập.
        private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

        // Kênh nội bộ: sự kiện Notification của Npgsql là callback đồng bộ, không await được trong đó.
        // Hàng đợi có trần để một đợt ghi hàng loạt không thể nuốt hết RAM; đầy thì bỏ bản ghi cũ nhất
        // (mất một tín hiệu refetch không nguy hiểm bằng việc kéo sập tiến trình).
        private const int QueueCapacity = 2000;

        private readonly string _connectionString;
        private readonly string _channel;
        private readonly bool _enabled;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DbChangeListenerHostedService> _logger;

        private readonly Channel<string> _queue = Channel.CreateBounded<string>(
            new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest });

        public DbChangeListenerHostedService(
            string connectionString,
            IServiceScopeFactory scopeFactory,
            RealtimeOptions options,
            ILogger<DbChangeListenerHostedService> logger)
        {
            // Connection RIÊNG, KHÔNG lấy từ pool của EF: trạng thái LISTEN gắn với đúng một connection
            // và connection phải mở suốt đời ứng dụng. Connection từ pool sẽ bị trả về rồi tái sử dụng
            // cho query khác → mất LISTEN im lặng. Multiplexing cũng phải tắt vì nó ghép nhiều lệnh
            // lên connection dùng chung.
            var csb = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                Multiplexing = false,
                KeepAlive = 30,          // giữ kết nối sống qua idle timeout của NAT/proxy
                Timeout = 30,
            };
            _connectionString = csb.ConnectionString;

            _scopeFactory = scopeFactory;
            _logger = logger;
            _enabled = options.DbListener.Enabled;

            // Tên kênh đi thẳng vào câu lệnh LISTEN (không tham số hoá được) nên phải chặn ký tự lạ.
            var channel = options.DbListener.Channel;
            _channel = Regex.IsMatch(channel ?? string.Empty, "^[a-z_][a-z0-9_]*$")
                ? channel!
                : new DbListenerOptions().Channel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("[DbChangeListener] TẮT theo cấu hình Realtime:DbListener:Enabled.");
                return;
            }

            await Task.WhenAll(ListenLoopAsync(stoppingToken), ConsumeLoopAsync(stoppingToken));
        }

        /// <summary>Giữ kết nối LISTEN, tự nối lại khi rớt.</summary>
        private async Task ListenLoopAsync(CancellationToken ct)
        {
            var delay = MinRetryDelay;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(_connectionString);
                    conn.Notification += (_, e) => _queue.Writer.TryWrite(e.Payload);

                    await conn.OpenAsync(ct);
                    await using (var cmd = new NpgsqlCommand($"LISTEN {_channel};", conn))
                        await cmd.ExecuteNonQueryAsync(ct);

                    _logger.LogInformation("[DbChangeListener] LISTEN {Channel} — realtime tầng DB đã bật.", _channel);
                    delay = MinRetryDelay;

                    // NOTIFY là fire-and-forget: mọi sự kiện phát ra trong lúc listener chưa nối được
                    // đã mất vĩnh viễn. Nên sau mỗi lần (re)connect phải bảo client nạp lại toàn bộ,
                    // nếu không màn hình đang mở sẽ giữ dữ liệu cũ mà không ai biết.
                    await BroadcastResyncAsync(ct);

                    while (!ct.IsCancellationRequested)
                        await conn.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DbChangeListener] Mất kết nối LISTEN, thử lại sau {Delay}s.", delay.TotalSeconds);

                    try { await Task.Delay(delay, ct); }
                    catch (OperationCanceledException) { break; }

                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxRetryDelay.TotalSeconds));
                }
            }
        }

        /// <summary>Xử lý tuần tự payload trong hàng đợi.</summary>
        private async Task ConsumeLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var payload in _queue.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        await DispatchAsync(payload, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Một sự kiện hỏng không được làm chết cả kênh realtime.
                        _logger.LogError(ex, "[DbChangeListener] Xử lý sự kiện thất bại: {Payload}", payload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Dừng ứng dụng — kết thúc bình thường.
            }
        }

        private async Task DispatchAsync(string payload, CancellationToken ct)
        {
            var change = DbChangeRouter.Parse(payload);
            if (change is null)
            {
                _logger.LogDebug("[DbChangeListener] Bỏ qua payload không đọc được: {Payload}", payload);
                return;
            }

            using var scope = _scopeFactory.CreateScope();

            var lookup = DbChangeRouter.NeedsApplicationLookup(change.Table) || change.Table == "applications"
                ? await LookupApplicationAsync(scope, change, ct)
                : DbChangeLookup.Empty;

            var dispatch = DbChangeRouter.Resolve(change, lookup);
            if (!dispatch.HasRecipients)
            {
                // Bảng chưa định tuyến (refresh_tokens, audit_logs, questions/answers...) dừng ở đây.
                _logger.LogDebug("[DbChangeListener] {Table}/{Op}: không có người nhận.", change.Table, change.Op);
                return;
            }

            var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

            foreach (var userId in dispatch.UserIds)
                await notif.PublishUserEventAsync(userId, DbChangeRouter.EventType, dispatch.Payload, ct);

            foreach (var group in dispatch.RoleGroups)
                await notif.PublishGroupEventAsync(group, DbChangeRouter.EventType, dispatch.Payload, ct);

            if (dispatch.BroadcastAll)
                await notif.PublishAllEventAsync(DbChangeRouter.EventType, dispatch.BroadcastPayload, ct);
        }

        /// <summary>
        /// Tra ứng viên + chủ tin cho những bảng chỉ mang <c>application_id</c>. IgnoreQueryFilters để
        /// hồ sơ vừa bị xoá mềm vẫn định tuyến được sự kiện xoá.
        /// </summary>
        private static async Task<DbChangeLookup> LookupApplicationAsync(
            IServiceScope scope, DbChangeNotification change, CancellationToken ct)
        {
            var applicationId = change.Table == "applications" ? change.Id : change.ApplicationId;
            if (applicationId is null) return DbChangeLookup.Empty;

            var db = scope.ServiceProvider.GetRequiredService<AriDbContext>();

            var row = await (
                from a in db.Set<ApplicationEntity>().IgnoreQueryFilters()
                where a.Id == applicationId
                join j in db.Set<JobPosting>().IgnoreQueryFilters() on a.JobPostingId equals j.Id into jobs
                from j in jobs.DefaultIfEmpty()
                select new
                {
                    a.CandidateAccountId,
                    a.JobPostingId,
                    OwnerUserId = (Guid?)j.CreatedByUserId,
                }).FirstOrDefaultAsync(ct);

            if (row is null) return DbChangeLookup.Empty;

            return new DbChangeLookup
            {
                CandidateAccountId = row.CandidateAccountId,
                JobPostingId = row.JobPostingId,
                JobOwnerUserId = row.OwnerUserId,
            };
        }

        private async Task BroadcastResyncAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await notif.PublishAllEventAsync(
                    DbChangeRouter.EventType,
                    new Dictionary<string, object?> { ["t"] = "*", ["op"] = DbChangeRouter.ResyncOp },
                    ct);
            }
            catch (Exception ex)
            {
                // Không chặn vòng lặp LISTEN chỉ vì không phát được resync.
                _logger.LogWarning(ex, "[DbChangeListener] Không phát được tín hiệu resync.");
            }
        }
    }
}
