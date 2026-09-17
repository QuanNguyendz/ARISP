using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Khoá "đang chấm" dùng chung cho MỌI đường chấm CV trong tiến trình (ADR-070) — luồng Portal ứng
    /// viên xem độ phù hợp và hàng đợi hồ sơ. Trước đây mỗi đường tự giữ trạng thái riêng, nên cùng một CV
    /// nộp ngay sau khi xem độ phù hợp bị gọi AI hai lần và ghi hai dòng.
    ///
    /// Đây là lớp chặn RẺ trong một tiến trình; chặn thật là UNIQUE (tin, CV, bộ tiêu chí) ở DB.
    ///
    /// Kèm luôn trạng thái lỗi gần nhất của từng khoá: lỗi AI không ghi DB (để lượt sau chấm lại được),
    /// nên Portal cần nơi đọc "vừa hỏng vì sao", và lượt quét cần biết để giãn nhịp thử lại thay vì gọi AI
    /// mỗi mười phút cho một file luôn hỏng.
    /// </summary>
    public sealed class CvScoringInFlight
    {
        private sealed class Entry
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public int Refs;
        }

        private readonly object _sync = new();
        private readonly Dictionary<string, Entry> _entries = new();
        private readonly ConcurrentDictionary<string, FailureState> _failures = new();

        /// <param name="Code">Mã lý do (<see cref="CvScoringErrors"/>); null = không phân loại được.</param>
        public sealed record FailureState(string Message, string? Code, int Attempts, DateTimeOffset RetryAfter);

        public static string Key(Guid jobPostingId, string cvHash, Guid rubricDocumentId)
            => $"{jobPostingId:N}:{cvHash}:{rubricDocumentId:N}";

        /// <summary>Khoá lỗi của một HỒ SƠ theo một phiên bản bộ tiêu chí — đổi bộ tiêu chí là khoá mới, lỗi cũ không còn liên quan.</summary>
        public static string ApplicationKey(Guid applicationId, Guid rubricDocumentId)
            => $"app:{applicationId:N}:{rubricDocumentId:N}";

        /// <summary>Có lượt chấm đang chạy (hoặc đang chờ) cho khoá này không.</summary>
        public bool IsRunning(string key)
        {
            lock (_sync) return _entries.ContainsKey(key);
        }

        /// <summary>Giữ khoá trong suốt lượt chấm. Người đến sau chờ rồi đọc kết quả người trước đã lưu.</summary>
        public async Task<IDisposable> AcquireAsync(string key, CancellationToken ct)
        {
            Entry entry;
            lock (_sync)
            {
                if (!_entries.TryGetValue(key, out entry!))
                {
                    entry = new Entry();
                    _entries[key] = entry;
                }
                entry.Refs++;
            }

            try
            {
                await entry.Gate.WaitAsync(ct);
            }
            catch
            {
                Leave(key, entry);
                throw;
            }
            return new Release(this, key, entry);
        }

        private void Leave(string key, Entry entry)
        {
            lock (_sync)
            {
                if (--entry.Refs == 0) _entries.Remove(key);
            }
        }

        public FailureState RecordFailure(string key, string message, string? code = null)
            => _failures.AddOrUpdate(key,
                _ => new FailureState(message, code, 1, DateTimeOffset.UtcNow + Backoff(1)),
                (_, old) => new FailureState(message, code, old.Attempts + 1, DateTimeOffset.UtcNow + Backoff(old.Attempts + 1)));

        public void ClearFailure(string key) => _failures.TryRemove(key, out _);

        public FailureState? LastFailure(string key) => _failures.TryGetValue(key, out var f) ? f : null;

        /// <summary>Lỗi gần đây và chưa tới giờ thử lại — lượt quét bỏ qua.</summary>
        public bool ShouldBackOff(string key)
            => _failures.TryGetValue(key, out var f) && f.RetryAfter > DateTimeOffset.UtcNow;

        /// <summary>15', 30', 1h, 2h, … trần 6h.</summary>
        private static TimeSpan Backoff(int attempts)
            => TimeSpan.FromMinutes(Math.Min(360, 15 * Math.Pow(2, Math.Max(0, attempts - 1))));

        private sealed class Release : IDisposable
        {
            private readonly CvScoringInFlight _owner;
            private readonly string _key;
            private readonly Entry _entry;
            private int _disposed;

            public Release(CvScoringInFlight owner, string key, Entry entry)
            {
                _owner = owner;
                _key = key;
                _entry = entry;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
                _entry.Gate.Release();
                _owner.Leave(_key, _entry);
            }
        }
    }
}
