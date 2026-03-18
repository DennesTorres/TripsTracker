using System.Transactions;

namespace TripsTracker.Tools.Transactions;

/// <summary>
/// Helper for creating TransactionScope instances with consistent settings.
/// Use this when fine-grained control over scope boundaries within a method is needed,
/// as an alternative to the <see cref="Aop.Attributes.TransactionAttribute"/> AOP interceptor.
/// </summary>
public static class TransactionScopeHelper
{
    /// <summary>
    /// Creates a required transaction scope (joins existing or creates new).
    /// ReadCommitted isolation, async flow enabled.
    /// </summary>
    public static TransactionScope Required()
        => Create(TransactionScopeOption.Required, IsolationLevel.ReadCommitted);

    /// <summary>
    /// Creates a suppressed transaction scope (executes outside any ambient transaction).
    /// Use for operations that must persist regardless of outer transaction outcome (e.g. audit logs).
    /// </summary>
    public static TransactionScope Suppress()
        => Create(TransactionScopeOption.Suppress, IsolationLevel.ReadCommitted);

    /// <summary>
    /// Creates a new independent transaction scope, regardless of any ambient transaction.
    /// </summary>
    public static TransactionScope RequiresNew()
        => Create(TransactionScopeOption.RequiresNew, IsolationLevel.ReadCommitted);

    private static TransactionScope Create(TransactionScopeOption option, IsolationLevel isolationLevel)
        => new(
            option,
            new TransactionOptions { IsolationLevel = isolationLevel },
            TransactionScopeAsyncFlowOption.Enabled);
}
