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

    /// <summary>Lỗi ném ở lần gọi <see cref="SaveChangesAsync"/> thứ N (1-based) — cho handler save nhiều lần
    /// (vd revoke save rồi refresh save): phân biệt "first save throws" vs "second save throws" trong test-plan.</summary>
    private readonly Dictionary<int, Exception> _saveFails = new();

    /// <summary>Bắt lần <see cref="SaveChangesAsync"/> thứ <paramref name="call"/> ném lỗi — chainable.</summary>
    public InMemoryUnitOfWork FailSaveOn(int call, string message)
    {
        _saveFails[call] = new Exception(message);
        return this;
    }

    /// <summary>Nạp sẵn dữ liệu cho một loại entity (chainable).</summary>
    public InMemoryUnitOfWork Seed<T>(params T[] entities) where T : class
    {
        Repo<T>().Items.AddRange(entities);
        return this;
    }

    /// <summary>
    /// Bắt repository của một loại entity ném lỗi ở mọi thao tác (mô phỏng "repository throws Exception(...)"
    /// trong test-plan) — chainable. Các repo khác vẫn hoạt động bình thường nên test được đúng repo nào chết.
    /// </summary>
    public InMemoryUnitOfWork FailRepo<T>(string message = "DB Error") where T : class
    {
        Repo<T>().FailWith = new Exception(message);
        return this;
    }

    /// <summary>Bắt riêng một thao tác của repo ném lỗi (giữ các thao tác khác chạy bình thường) — để phân biệt
    /// "lookup throws" vs "AddAsync throws" vs "Update throws" như test-plan yêu cầu. Chainable.</summary>
    public InMemoryUnitOfWork FailFindFor<T>(string m) where T : class { Repo<T>().FailOnFind = new Exception(m); return this; }
    public InMemoryUnitOfWork FailGetByIdFor<T>(string m) where T : class { Repo<T>().FailOnGetById = new Exception(m); return this; }
    public InMemoryUnitOfWork FailAddFor<T>(string m) where T : class { Repo<T>().FailOnAdd = new Exception(m); return this; }
    public InMemoryUnitOfWork FailUpdateFor<T>(string m) where T : class { Repo<T>().FailOnUpdate = new Exception(m); return this; }
    public InMemoryUnitOfWork FailDeleteFor<T>(string m) where T : class { Repo<T>().FailOnDelete = new Exception(m); return this; }

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
        if (_saveFails.TryGetValue(SaveChangesCount, out var ex)) throw ex;
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

    /// <summary>Khi set: mọi thao tác của repo ném lỗi này — dùng cho case "repository throws" trong test-plan.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>Ném lỗi riêng cho từng thao tác (ưu tiên hơn <see cref="FailWith"/>) — phân biệt lookup/add/update chết.</summary>
    public Exception? FailOnGetById { get; set; }
    public Exception? FailOnFind { get; set; }
    public Exception? FailOnGetAll { get; set; }
    public Exception? FailOnAdd { get; set; }
    public Exception? FailOnUpdate { get; set; }
    public Exception? FailOnDelete { get; set; }

    private static Guid IdOf(T e) => (Guid)IdProp.GetValue(e)!;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if ((FailOnGetById ?? FailWith) is { } ex) throw ex;
        return Task.FromResult(Items.FirstOrDefault(e => IdOf(e) == id));
    }

    public Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
    {
        if ((FailOnGetAll ?? FailWith) is { } ex) throw ex;
        return Task.FromResult<IEnumerable<T>>(Items.ToList());
    }

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        if ((FailOnFind ?? FailWith) is { } ex) throw ex;
        return Task.FromResult<IEnumerable<T>>(Items.Where(predicate.Compile()).ToList());
    }

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        if ((FailOnAdd ?? FailWith) is { } ex) throw ex;
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        if ((FailOnUpdate ?? FailWith) is { } ex) throw ex;
        // Test giữ nguyên tham chiếu entity đã seed nên thay đổi đã hiện diện; chỉ thêm nếu là entity lạ.
        if (!Items.Contains(entity)) Items.Add(entity);
    }

    public void Delete(T entity) { if ((FailOnDelete ?? FailWith) is { } ex) throw ex; Items.Remove(entity); }

    public Task<List<TResult>> QueryAsync<TResult>(
        Func<IQueryable<T>, IQueryable<TResult>> shaper, CancellationToken ct = default)
    {
        if (FailWith is { } ex) throw ex;
        return Task.FromResult(shaper(Items.AsQueryable()).ToList());
    }

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        if (FailWith is { } ex) throw ex;
        return Task.FromResult(Items.Count(predicate.Compile()));
    }
}
