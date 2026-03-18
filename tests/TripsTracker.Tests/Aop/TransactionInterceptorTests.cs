using Castle.DynamicProxy;
using System.Transactions;
using TripsTracker.Tools.Aop.Attributes;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class TransactionInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public interface ITestService
    {
        void Execute();
        [Transaction] void TransactedExecute();
        [Transaction(ScopeOption = TransactionScopeOption.Suppress)] void SuppressedExecute();
        [Transaction] void ThrowingTransactedExecute();
    }

    private class TestService : ITestService
    {
        public bool Executed;
        public bool WasInTransaction;

        public void Execute() => Executed = true;

        public void TransactedExecute()
        {
            Executed = true;
            WasInTransaction = Transaction.Current != null;
        }

        public void SuppressedExecute()
        {
            Executed = true;
            WasInTransaction = Transaction.Current != null;
        }

        public void ThrowingTransactedExecute() => throw new InvalidOperationException("fail");
    }

    private static TransactionInterceptor CreateInterceptor() => new();

    #endregion

    [TestMethod]
    public void WithoutAttribute_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.Execute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_MethodExecutesInsideTransaction()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.TransactedExecute();

        Assert.IsTrue(impl.Executed);
        Assert.IsTrue(impl.WasInTransaction, "Method should execute within a TransactionScope.");
    }

    [TestMethod]
    public void WithSuppressOption_NoAmbientTransactionInsideMethod()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        // Create an ambient transaction so Suppress has something to suppress
        using var outer = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled);

        proxy.SuppressedExecute();

        Assert.IsTrue(impl.Executed);
        Assert.IsFalse(impl.WasInTransaction, "Suppressed scope should hide the ambient transaction.");
    }

    [TestMethod]
    public void WhenMethodThrows_ExceptionIsRethrown()
    {
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            new TestService(), CreateInterceptor());

        Assert.ThrowsExactly<InvalidOperationException>(() => proxy.ThrowingTransactedExecute());
    }
}
