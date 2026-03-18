using Castle.DynamicProxy;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Tools.Aop.Attributes;
using ValidationException = TripsTracker.Interfaces.Exceptions.ValidationException;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that validates method parameters using FluentValidation.
/// Activated by <see cref="ValidateAttribute"/> on a method or class.
/// Resolves IValidator&lt;T&gt; for each parameter type from DI and validates before execution.
/// </summary>
public class ValidationInterceptor : IInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Intercept(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);
        if (attribute is null)
        {
            invocation.Proceed();
            return;
        }

        var errors = new Dictionary<string, string[]>();

        foreach (var argument in invocation.Arguments)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validator = _serviceProvider.GetService(validatorType) as IValidator;

            if (validator is null) continue;

            var context = new ValidationContext<object>(argument);
            var result = validator.Validate(context);

            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    if (!errors.ContainsKey(error.PropertyName))
                        errors[error.PropertyName] = [];

                    errors[error.PropertyName] = [.. errors[error.PropertyName], error.ErrorMessage];
                }
            }
        }

        if (errors.Count > 0)
            throw new ValidationException(errors);

        invocation.Proceed();
    }

    private static ValidateAttribute? GetAttribute(IInvocation invocation)
        => invocation.Method.GetCustomAttributes(typeof(ValidateAttribute), true).FirstOrDefault() as ValidateAttribute
           ?? invocation.TargetType.GetCustomAttributes(typeof(ValidateAttribute), true).FirstOrDefault() as ValidateAttribute;
}
