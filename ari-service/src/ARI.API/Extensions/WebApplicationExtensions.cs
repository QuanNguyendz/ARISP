using System.Threading.Tasks;
using ARI.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.API.Extensions
{
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Auto-migrate (retry) + đảm bảo schema bootstrap khi app khởi động.
        /// </summary>
        public static async Task InitialiseDatabaseAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var initialiser = scope.ServiceProvider.GetRequiredService<AriDbContextInitialiser>();
            await initialiser.InitialiseAsync();
        }
    }
}
