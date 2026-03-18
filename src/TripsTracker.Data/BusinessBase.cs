using Microsoft.EntityFrameworkCore;

namespace TripsTracker.Data;

/// <summary>
/// Top-level base class for all business classes.
/// Combines query infrastructure from <see cref="QueryBase{TEntity,TDomain}"/>
/// with generic CRUD operations from <see cref="CrudBase{TEntity,TDomain}"/>.
///
/// Business class usage:
/// <code>
/// public class TripBusiness : BusinessBase&lt;TripEntity, TripDomain&gt;, ITripBusiness
/// {
///     public TripBusiness(AppDbContext context) : base(context) { }
///
///     protected override IQueryable&lt;TripEntity&gt; BuildBaseQuery()
///         => base.BuildBaseQuery().Where(t => !t.IsDeleted);
///
///     public async Task&lt;TripDomain?&gt; GetByIdAsync(int id, CancellationToken ct = default)
///         => await GetByIdAsync(t => t.Id == id, t => new TripDomain(...), ct);
/// }
/// </code>
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type (strong table owned by this class).</typeparam>
/// <typeparam name="TDomain">The primary domain projection type.</typeparam>
public abstract class BusinessBase<TEntity, TDomain> : CrudBase<TEntity, TDomain>
    where TEntity : class
    where TDomain : class
{
    protected BusinessBase(DbContext context) : base(context) { }
}
