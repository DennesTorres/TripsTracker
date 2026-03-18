using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Tests.Exceptions;

[TestClass]
public class ExceptionHierarchyTests
{
    #region NotFoundException

    [TestMethod]
    public void NotFoundException_WithResourceAndKey_FormatsMessage()
    {
        var ex = new NotFoundException("Trip", 42);

        Assert.AreEqual("Trip with key '42' was not found.", ex.Message);
        Assert.AreEqual("NOT_FOUND", ex.ErrorCode);
        Assert.IsInstanceOfType<AppException>(ex);
    }

    [TestMethod]
    public void NotFoundException_WithMessage_UsesProvidedMessage()
    {
        var ex = new NotFoundException("Custom not found message");

        Assert.AreEqual("Custom not found message", ex.Message);
        Assert.AreEqual("NOT_FOUND", ex.ErrorCode);
    }

    #endregion

    #region BusinessRuleException

    [TestMethod]
    public void BusinessRuleException_UsesDefaultErrorCode()
    {
        var ex = new BusinessRuleException("Cannot delete an active trip.");

        Assert.AreEqual("BUSINESS_RULE_VIOLATION", ex.ErrorCode);
        Assert.AreEqual("Cannot delete an active trip.", ex.Message);
        Assert.IsInstanceOfType<AppException>(ex);
    }

    [TestMethod]
    public void BusinessRuleException_WithCustomErrorCode_UsesCustomCode()
    {
        var ex = new BusinessRuleException("message", "CUSTOM_RULE");

        Assert.AreEqual("CUSTOM_RULE", ex.ErrorCode);
    }

    #endregion

    #region UnauthorizedException

    [TestMethod]
    public void UnauthorizedException_HasDefaultMessageAndErrorCode()
    {
        var ex = new UnauthorizedException();

        Assert.AreEqual("UNAUTHORIZED", ex.ErrorCode);
        Assert.IsFalse(string.IsNullOrEmpty(ex.Message));
        Assert.IsInstanceOfType<AppException>(ex);
    }

    [TestMethod]
    public void UnauthorizedException_WithMessage_UsesProvidedMessage()
    {
        var ex = new UnauthorizedException("Access denied to this resource.");

        Assert.AreEqual("Access denied to this resource.", ex.Message);
        Assert.AreEqual("UNAUTHORIZED", ex.ErrorCode);
    }

    #endregion

    #region ValidationException

    [TestMethod]
    public void ValidationException_WithDictionary_HasAllErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required.", "Name is too short."],
            ["Email"] = ["Email is invalid."]
        };

        var ex = new ValidationException(errors);

        Assert.AreEqual("VALIDATION_ERROR", ex.ErrorCode);
        Assert.HasCount(2, ex.Errors);
        CollectionAssert.AreEqual(
            new[] { "Name is required.", "Name is too short." },
            (string[])ex.Errors["Name"]);
        Assert.AreEqual("Email is invalid.", ex.Errors["Email"][0]);
    }

    [TestMethod]
    public void ValidationException_WithFieldAndError_HasSingleError()
    {
        var ex = new ValidationException("Name", "Name is required.");

        Assert.HasCount(1, ex.Errors);
        Assert.IsTrue(ex.Errors.ContainsKey("Name"));
        Assert.AreEqual("Name is required.", ex.Errors["Name"][0]);
    }

    [TestMethod]
    public void ValidationException_ErrorsAreReadOnly()
    {
        var ex = new ValidationException("F", "E");

        Assert.IsInstanceOfType<IReadOnlyDictionary<string, string[]>>(ex.Errors);
    }

    #endregion

    #region AppException base contract

    [TestMethod]
    public void AppException_IsAbstract()
    {
        Assert.IsTrue(typeof(AppException).IsAbstract);
    }

    [TestMethod]
    public void AppException_Subclasses_InheritFromException()
    {
        Assert.IsTrue(typeof(AppException).IsSubclassOf(typeof(Exception)));
        Assert.IsTrue(typeof(NotFoundException).IsSubclassOf(typeof(AppException)));
        Assert.IsTrue(typeof(BusinessRuleException).IsSubclassOf(typeof(AppException)));
        Assert.IsTrue(typeof(UnauthorizedException).IsSubclassOf(typeof(AppException)));
        Assert.IsTrue(typeof(ValidationException).IsSubclassOf(typeof(AppException)));
    }

    #endregion
}
