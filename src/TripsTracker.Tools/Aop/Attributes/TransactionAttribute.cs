using System.Transactions;

namespace TripsTracker.Tools.Aop.Attributes;

/// <summary>
/// Marks a method for AOP transaction scope interception.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class TransactionAttribute : Attribute
{
    /// <summary>
    /// Transaction scope option. Default: Required (join existing or create new).
    /// Use Suppress for methods that must run outside any transaction (e.g. logging).
    /// </summary>
    public TransactionScopeOption ScopeOption { get; set; } = TransactionScopeOption.Required;

    /// <summary>
    /// Isolation level. Default: ReadCommitted.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
}
