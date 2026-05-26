using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
using ARISP.Infrastructure.Data;

namespace ARISP.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ARISPDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public Repository(ARISPDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _context.Set<T>().FindAsync(new object[] { id }, ct);
            if (entity is IMultiTenant tenantEntity && _currentUserService.OrganizationId.HasValue)
            {
                if (tenantEntity.OrganizationId != _currentUserService.OrganizationId.Value)
                    return null; // Enforce multi-tenant isolation
            }
            return entity;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        {
            IQueryable<T> query = _context.Set<T>();
            if (typeof(IMultiTenant).IsAssignableFrom(typeof(T)) && _currentUserService.OrganizationId.HasValue)
            {
                query = query.Where(CreateTenantExpression(_currentUserService.OrganizationId.Value));
            }
            return await query.ToListAsync(ct);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        {
            IQueryable<T> query = _context.Set<T>();
            if (typeof(IMultiTenant).IsAssignableFrom(typeof(T)) && _currentUserService.OrganizationId.HasValue)
            {
                query = query.Where(CreateTenantExpression(_currentUserService.OrganizationId.Value));
            }
            return await query.Where(predicate).ToListAsync(ct);
        }

        public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        {
            if (entity is IMultiTenant tenantEntity && _currentUserService.OrganizationId.HasValue)
            {
                tenantEntity.OrganizationId = _currentUserService.OrganizationId.Value;
            }
            await _context.Set<T>().AddAsync(entity, ct);
        }

        public virtual void Update(T entity)
        {
            if (entity is IMultiTenant tenantEntity && _currentUserService.OrganizationId.HasValue)
            {
                if (tenantEntity.OrganizationId != _currentUserService.OrganizationId.Value)
                    throw new InvalidOperationException("Cross-tenant update detected!");
            }
            _context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void Delete(T entity)
        {
            if (entity is IMultiTenant tenantEntity && _currentUserService.OrganizationId.HasValue)
            {
                if (tenantEntity.OrganizationId != _currentUserService.OrganizationId.Value)
                    throw new InvalidOperationException("Cross-tenant delete detected!");
            }
            _context.Set<T>().Remove(entity);
        }

        private Expression<Func<T, bool>> CreateTenantExpression(Guid orgId)
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            var property = Expression.Property(parameter, nameof(IMultiTenant.OrganizationId));
            var equal = Expression.Equal(property, Expression.Constant(orgId));
            return Expression.Lambda<Func<T, bool>>(equal, parameter);
        }
    }
}
