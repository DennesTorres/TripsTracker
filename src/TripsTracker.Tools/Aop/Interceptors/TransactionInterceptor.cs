using Castle.DynamicProxy;
using System.Transactions;
using TripsTracker.Tools.Aop.Attributes;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that wraps method execution in a TransactionScope.
/// Activated by <see cref="TransactionAttribute"/> on a method or class.
/// Uses AsyncFlowEnabled to correctly flow across async/await boundaries.
/// </summary>
public class TransactionInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);
        if (attribute is null)
        {
            invocation.Proceed();
            return;
        }

        using var scope = new TransactionScope(
            attribute.ScopeOption,
            new TransactionOptions { IsolationLevel = attribute.IsolationLevel },
            TransactionScopeAsyncFlowOption.Enabled);

        invocation.Proceed();
        scope.Complete();
    }

    private static TransactionAttribute? GetAttribute(IInvocation invocation)
        => invocation.Method.GetCustomAttributes(typeof(TransactionAttribute), true).FirstOrDefault() as TransactionAttribute
           ?? invocation.TargetType.GetCustomAttributes(typeof(TransactionAttribute), true).FirstOrDefault() as TransactionAttribute;
}
