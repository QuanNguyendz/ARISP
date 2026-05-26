using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using ARISP.Infrastructure.Data;

namespace ARISP.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ARISPDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ConcurrentDictionary<string, object> _repositories;
        private bool _disposed;

        public UnitOfWork(ARISPDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _repositories = new ConcurrentDictionary<string, object>();
        }

        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T).Name;

            return (IRepository<T>)_repositories.GetOrAdd(type, _ => 
                new Repository<T>(_context, _currentUserService));
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return await _context.SaveChangesAsync(ct);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
