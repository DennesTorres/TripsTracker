using Microsoft.EntityFrameworkCore;

namespace TripsTracker.Data;

/// <summary>
/// Top-level base class for all business classes.
/// Combines query infrastructure from <see cref="QueryBase{TEntity}"/>
/// with generic CRUD operations from <see cref="CrudBase{TEntity}"/>.
///
/// Business class usage:
/// <code>
/// public class TripBusiness : BusinessBase&lt;TripEntity&gt;, ITripBusiness
/// {
///     public TripBusiness(AppDbContext context) : base(context) { }
///
///     protected override IQueryable&lt;TripEntity&gt; BuildBaseQuery()
///         => base.BuildBaseQuery().Where(t => !t.IsDeleted);
///
///     public async Task&lt;TripDto?&gt; GetByIdAsync(int id, CancellationToken ct = default)
///         => await BuildBaseQuery()
///             .Where(t => t.Id == id)
///             .Select(t => new TripDto(...))
///             .FirstOrDefaultAsync(ct);
/// }
/// </code>
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type (strong table owned by this class).</typeparam>
public abstract class BusinessBase<TEntity> : CrudBase<TEntity>
    where TEntity : class
{
    protected BusinessBase(DbContext context) : base(context) { }
}
