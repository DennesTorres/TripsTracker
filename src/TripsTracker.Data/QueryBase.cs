using Microsoft.EntityFrameworkCore;

namespace TripsTracker.Data;

/// <summary>
/// Protected query infrastructure for business classes.
/// Provides <see cref="BuildBaseQuery"/> as the mandatory entry point for all queries.
/// Always applies AsNoTracking — reads never need change tracking.
/// Override in subclasses to add common filters (e.g. soft-delete, tenant isolation).
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type (database row).</typeparam>
public abstract class QueryBase<TEntity>
    where TEntity : class
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
}
