using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace TripsTracker.Data;

/// <summary>
/// Protected query infrastructure for business classes.
/// Provides <see cref="BuildBaseQuery"/> as the mandatory entry point for all queries,
/// and execution helpers that consume typed <see cref="IQueryable{TDomain}"/> projections.
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type (database row).</typeparam>
/// <typeparam name="TDomain">The domain projection type returned to callers.</typeparam>
public abstract class QueryBase<TEntity, TDomain>
    where TEntity : class
    where TDomain : class
{
    protected readonly DbContext Context;

    protected QueryBase(DbContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Mandatory entry point for ALL queries in a business class.
    /// Override in subclasses to apply common filters (e.g. soft-delete, tenant isolation).
    /// Always applies AsNoTracking — reads never need change tracking.
    /// </summary>
    protected virtual IQueryable<TEntity> BuildBaseQuery()
        => Context.Set<TEntity>().AsNoTracking();

    /// <summary>
    /// Executes the projected query and returns all matching results.
    /// </summary>
    protected async Task<List<TDomain>> ToListAsync(
        IQueryable<TDomain> query,
        CancellationToken ct = default)
        => await query.ToListAsync(ct);

    /// <summary>
    /// Executes the projected query and returns the first match or null.
    /// </summary>
    protected async Task<TDomain?> FirstOrDefaultAsync(
        IQueryable<TDomain> query,
        CancellationToken ct = default)
        => await query.FirstOrDefaultAsync(ct);

    /// <summary>
    /// Applies a where-clause to <see cref="BuildBaseQuery"/> and projects to the domain type.
    /// Use this as the standard way to compose queries from the base query.
    /// </summary>
    protected IQueryable<TDomain> ApplyFilter(
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, TDomain>> projection)
        => BuildBaseQuery().Where(predicate).Select(projection);
}
