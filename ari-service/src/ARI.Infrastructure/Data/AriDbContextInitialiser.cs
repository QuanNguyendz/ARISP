using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Data
{
    /// <summary>
    /// Khởi tạo database lúc app boot (pattern theo template Jason Taylor CleanArchitecture).
    /// Toàn bộ schema do EF migrations sở hữu — schema bootstrap raw SQL cũ đã được hợp nhất
    /// vào migration ReconcileStartupBootstrap.
    /// </summary>
    public class AriDbContextInitialiser
    {
        private readonly AriDbContext _context;
        private readonly ILogger<AriDbContextInitialiser> _logger;

        public AriDbContextInitialiser(AriDbContext context, ILogger<AriDbContextInitialiser> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Auto database migration on startup — thử lại vài lần vì kết nối Supabase đôi khi
        // chậm/timeout auth lúc khởi động (lỗi thoáng qua, không phải sai migration).
        public async Task InitialiseAsync()
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Console.WriteLine($"Applying EF Core migrations (attempt {attempt}/3)...");
                    await _context.Database.MigrateAsync();
                    Console.WriteLine("Migrations applied.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply migrations (attempt {Attempt}/3).", attempt);
                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
        }
    }
}
