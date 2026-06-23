using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ARISP.Infrastructure.Data
{
    /// <summary>
    /// Factory dùng cho design-time (dotnet ef migrations / database update).
    /// Không kết nối DB khi chỉ `migrations add` — chỉ cần provider Npgsql được cấu hình.
    /// Tránh phải nạp secrets / chạy Program.cs lúc tạo migration.
    /// </summary>
    public class ARISPDbContextFactory : IDesignTimeDbContextFactory<ARISPDbContext>
    {
        public ARISPDbContext CreateDbContext(string[] args)
        {
            // Design-time `migrations add` không kết nối DB → chỉ cần provider Npgsql.
            // Không nhúng secret: ưu tiên env var, fallback dùng chuỗi tối thiểu KHÔNG có mật khẩu.
            var connectionString =
                System.Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                ?? "Host=localhost;Database=arisp_design;Username=postgres";

            var options = new DbContextOptionsBuilder<ARISPDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations_history"))
                .Options;

            return new ARISPDbContext(options);
        }
    }
}
