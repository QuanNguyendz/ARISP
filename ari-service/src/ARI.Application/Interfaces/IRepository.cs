using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
        Task AddAsync(T entity, CancellationToken ct = default);
        void Update(T entity);
        void Delete(T entity);

        /// <summary>
        /// Truy vấn có shaping ở tầng SQL (Where/Select/GroupBy…) — chỉ kéo đúng cột cần,
        /// tránh nạp cả entity (vd cột text lớn như CvText). EF dịch và thực thi ở Infrastructure.
        /// </summary>
        Task<List<TResult>> QueryAsync<TResult>(
            Func<IQueryable<T>, IQueryable<TResult>> shaper,
            CancellationToken ct = default);

        /// <summary>Đếm bản ghi khớp điều kiện ở tầng SQL (COUNT), không nạp entity.</summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    }
}
