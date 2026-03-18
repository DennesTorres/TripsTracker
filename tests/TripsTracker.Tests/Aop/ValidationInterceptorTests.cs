using Castle.DynamicProxy;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Tools.Aop.Attributes;
using TripsTracker.Tools.Aop.Interceptors;
using ValidationException = TripsTracker.Interfaces.Exceptions.ValidationException;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class ValidationInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public record TestInput(string Name, string Email);

    private class TestInputValidator : AbstractValidator<TestInput>
    {
        public TestInputValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(x => x.Email).EmailAddress().WithMessage("Email is invalid.");
        }
    }

    public interface ITestService
    {
        void Execute(string value);
        [Validate] void ValidatedExecute(TestInput input);
        [Validate] void ValidatedNoRegisteredValidator(string raw);
        void NoAttributeExecute(TestInput input);
    }

    private class TestService : ITestService
    {
        public bool Executed;
        public void Execute(string value) => Executed = true;
        public void ValidatedExecute(TestInput input) => Executed = true;
        public void ValidatedNoRegisteredValidator(string raw) => Executed = true;
        public void NoAttributeExecute(TestInput input) => Executed = true;
    }

    private static ValidationInterceptor CreateInterceptor(bool registerValidator = true)
    {
        var services = new ServiceCollection();
        if (registerValidator)
            services.AddScoped<IValidator<TestInput>, TestInputValidator>();
        var provider = services.BuildServiceProvider();
        return new ValidationInterceptor(provider);
    }

    #endregion

    [TestMethod]
    public void WithoutAttribute_MethodProceeds_NoValidation()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor());

        // Empty name would fail validation if it were applied
        proxy.NoAttributeExecute(new TestInput("", "not-an-email"));

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_ValidInput_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor());

        proxy.ValidatedExecute(new TestInput("Alice", "alice@example.com"));

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_InvalidInput_ThrowsValidationException()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor());

        Assert.ThrowsExactly<ValidationException>(() =>
            proxy.ValidatedExecute(new TestInput("", "bad-email")));

        Assert.IsFalse(impl.Executed, "Method should not execute when validation fails.");
    }

    [TestMethod]
    public void WithAttribute_InvalidInput_ErrorsContainFailedFields()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor());

        var ex = Assert.ThrowsExactly<ValidationException>(() =>
            proxy.ValidatedExecute(new TestInput("", "bad-email")));

        Assert.IsTrue(ex.Errors.ContainsKey("Name"), "Errors should contain 'Name'.");
        Assert.IsTrue(ex.Errors.ContainsKey("Email"), "Errors should contain 'Email'.");
    }

    [TestMethod]
    public void WithAttribute_NoRegisteredValidator_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor(registerValidator: false));

        // No IValidator<string> registered — should skip validation and proceed
        proxy.ValidatedNoRegisteredValidator("raw value");

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_PartiallyInvalidInput_ReportsOnlyFailingFields()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            impl, CreateInterceptor());

        // Name is valid, Email is invalid
        var ex = Assert.ThrowsExactly<ValidationException>(() =>
            proxy.ValidatedExecute(new TestInput("Alice", "not-an-email")));

        Assert.IsFalse(ex.Errors.ContainsKey("Name"), "Name should be valid.");
        Assert.IsTrue(ex.Errors.ContainsKey("Email"), "Email should be invalid.");
    }
}
