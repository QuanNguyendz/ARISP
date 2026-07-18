using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<int> ExecuteSqlRawAsync(string sql, object[] parameters, CancellationToken ct = default);
    }
}
