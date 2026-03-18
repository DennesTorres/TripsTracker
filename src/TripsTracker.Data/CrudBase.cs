using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace TripsTracker.Data;

/// <summary>
/// Generic CRUD operations for business classes.
/// Uses <see cref="DbContext.Set{TEntity}"/> so no repository interface is needed.
/// SaveChangesAsync is called after each write — state resets to Unchanged,
/// preventing cross-contamination between sequential business calls in the same scope.
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type.</typeparam>
/// <typeparam name="TDomain">The domain projection type.</typeparam>
public abstract class CrudBase<TEntity, TDomain> : QueryBase<TEntity, TDomain>
    where TEntity : class
    where TDomain : class
{
    protected CrudBase(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new entity and immediately flushes to the database.
    /// </summary>
    protected async Task InsertAsync(TEntity entity, CancellationToken ct = default)
    {
        Context.Set<TEntity>().Add(entity);
        await Context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns a single matching domain projection, or null if not found.
    /// Uses <see cref="QueryBase{TEntity,TDomain}.BuildBaseQuery"/> as the base — all common
    /// filters (soft-delete etc.) are automatically applied.
    /// </summary>
    protected async Task<TDomain?> GetByIdAsync(
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, TDomain>> projection,
        CancellationToken ct = default)
        => await BuildBaseQuery()
            .Where(predicate)
            .Select(projection)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Updates specific fields on all rows matching the predicate without loading any records.
    /// Returns the number of rows affected.
    /// The <paramref name="setPropertyCalls"/> action configures the setter builder:
    /// <c>s => { s.SetProperty(e => e.Name, newName); s.SetProperty(e => e.UpdatedAt, now); }</c>
    /// </summary>
    protected async Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
        CancellationToken ct = default)
        => await Context.Set<TEntity>()
            .Where(predicate)
            .ExecuteUpdateAsync(setPropertyCalls, ct);

    /// <summary>
    /// Deletes all rows matching the predicate without loading any records.
    /// Returns the number of rows deleted.
    /// </summary>
    protected async Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
        => await Context.Set<TEntity>()
            .Where(predicate)
            .ExecuteDeleteAsync(ct);
}
