namespace TripsTracker.Tools.Aop.Attributes;

/// <summary>
/// Marks a method for AOP FluentValidation interception.
/// Validates all method parameters that have a registered IValidator&lt;T&gt; before execution.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ValidateAttribute : Attribute;
