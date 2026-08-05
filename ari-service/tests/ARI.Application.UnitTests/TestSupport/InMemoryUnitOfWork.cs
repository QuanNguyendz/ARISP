using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// UnitOfWork trong bộ nhớ cho unit test handler — mỗi loại entity một <see cref="InMemoryRepository{T}"/>.
/// Không DB/EF: tách logic nghiệp vụ (chấm điểm, gating, phân quyền…) khỏi hạ tầng để test thuần.
/// Dùng chung cho mọi flow test sau này, không riêng Online Test.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repos = new();

    /// <summary>Số lần <see cref="SaveChangesAsync"/> được gọi — để assert handler có persist hay không.</summary>
    public int SaveChangesCount { get; private set; }

    /// <summary>
    /// Hook cho <see cref="ExecuteSqlRawAsync"/> (mặc định trả 0). Test nào dùng SQL thô
    /// (vd chốt/nhả chỗ nguyên tử của Scheduling) tự cắm delegate giả lập hành vi + số dòng ảnh hưởng.
    /// </summary>
    public Func<string, object[], CancellationToken, Task<int>>? OnExecuteSqlRaw { get; set; }

    /// <summary>Khi true: <see cref="SaveChangesAsync"/> ném lỗi để test đường bù trừ (compensating) của handler.</summary>
    public bool ThrowOnSaveChanges { get; set; }

    /// <summary>Nạp sẵn dữ liệu cho một loại entity (chainable).</summary>
    public InMemoryUnitOfWork Seed<T>(params T[] entities) where T : class
    {
        Repo<T>().Items.AddRange(entities);
        return this;
    }

    /// <summary>Kho lưu trữ in-memory của một loại entity (để assert trực tiếp trong test).</summary>
    public InMemoryRepository<T> Repo<T>() where T : class
    {
        if (!_repos.TryGetValue(typeof(T), out var repo))
        {
            repo = new InMemoryRepository<T>();
            _repos[typeof(T)] = repo;
        }
        return (InMemoryRepository<T>)repo;
    }

    public IRepository<T> Repository<T>() where T : class => Repo<T>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (ThrowOnSaveChanges) throw new InvalidOperationException("save failed");
        SaveChangesCount++;
        return Task.FromResult(1);
    }

    public Task<int> ExecuteSqlRawAsync(string sql, object[] parameters, CancellationToken ct = default)
        => OnExecuteSqlRaw?.Invoke(sql, parameters, ct) ?? Task.FromResult(0);

    public void Dispose() { }
}

/// <summary>Repository in-memory: LINQ-to-objects thay cho EF, giữ tham chiếu entity nên Update là no-op.</summary>
public sealed class InMemoryRepository<T> : IRepository<T> where T : class
{
    private static readonly PropertyInfo IdProp =
        typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"{typeof(T).Name} thiếu thuộc tính Id.");

    public List<T> Items { get; } = new();

    private static Guid IdOf(T e) => (Guid)IdProp.GetValue(e)!;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(e => IdOf(e) == id));

    public Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IEnumerable<T>>(Items.ToList());

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IEnumerable<T>>(Items.Where(predicate.Compile()).ToList());

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        // Test giữ nguyên tham chiếu entity đã seed nên thay đổi đã hiện diện; chỉ thêm nếu là entity lạ.
        if (!Items.Contains(entity)) Items.Add(entity);
    }

    public void Delete(T entity) => Items.Remove(entity);

    public Task<List<TResult>> QueryAsync<TResult>(
        Func<IQueryable<T>, IQueryable<TResult>> shaper, CancellationToken ct = default)
        => Task.FromResult(shaper(Items.AsQueryable()).ToList());

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Items.Count(predicate.Compile()));
}
