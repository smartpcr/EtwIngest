// -----------------------------------------------------------------------
// <copyright file="ConditionEvaluatorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Routing;

using ExecutionEngine.Contexts;
using ExecutionEngine.Routing;
using FluentAssertions;

[TestClass]
public class ConditionEvaluatorTests
{
    [TestMethod]
    public void Evaluate_WithNullCondition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        Action act = () => ConditionEvaluator.Evaluate(null!, context);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("condition");
    }

    [TestMethod]
    public void Evaluate_WithEmptyCondition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        Action act = () => ConditionEvaluator.Evaluate(string.Empty, context);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("condition");
    }

    [TestMethod]
    public void Evaluate_WithWhitespaceCondition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        Action act = () => ConditionEvaluator.Evaluate("   ", context);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("condition");
    }

    [TestMethod]
    public void Evaluate_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => ConditionEvaluator.Evaluate("true", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [TestMethod]
    public void Evaluate_WithTrueLiteral_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        var result = ConditionEvaluator.Evaluate("true", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithTrueLiteralUpperCase_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        var result = ConditionEvaluator.Evaluate("TRUE", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithFalseLiteral_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        var result = ConditionEvaluator.Evaluate("false", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithEqualityOperator_MatchingStrings_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["status"] = "success";

        // Act
        var result = ConditionEvaluator.Evaluate("output.status == 'success'", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithEqualityOperator_CaseInsensitive_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["status"] = "Success";

        // Act
        var result = ConditionEvaluator.Evaluate("output.status == 'success'", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithEqualityOperator_NonMatchingStrings_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["status"] = "failure";

        // Act
        var result = ConditionEvaluator.Evaluate("output.status == 'success'", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithEqualityOperator_MissingProperty_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        var result = ConditionEvaluator.Evaluate("output.status == 'success'", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithNotEqualOperator_NonMatchingStrings_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["status"] = "failure";

        // Act
        var result = ConditionEvaluator.Evaluate("output.status != 'success'", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithGreaterThanOperator_NumericComparison_True_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["count"] = 10;

        // Act
        var result = ConditionEvaluator.Evaluate("output.count > 5", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithGreaterThanOperator_NumericComparison_False_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["count"] = 3;

        // Act
        var result = ConditionEvaluator.Evaluate("output.count > 5", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithLessThanOperator_NumericComparison_True_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["count"] = 3;

        // Act
        var result = ConditionEvaluator.Evaluate("output.count < 5", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithGreaterThanOrEqualOperator_NumericComparison_Equal_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["count"] = 5;

        // Act
        var result = ConditionEvaluator.Evaluate("output.count >= 5", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithLessThanOrEqualOperator_NumericComparison_Equal_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["count"] = 5;

        // Act
        var result = ConditionEvaluator.Evaluate("output.count <= 5", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_BooleanTrue_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["isValid"] = true;

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_BooleanFalse_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["isValid"] = false;

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_StringTrue_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["isValid"] = "true";

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_MissingProperty_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void Evaluate_WithInvalidExpression_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var context = new NodeExecutionContext();

        // Act
        Action act = () => ConditionEvaluator.Evaluate("invalid expression", context);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid condition expression: invalid expression");
    }

    [TestMethod]
    public void Evaluate_WithGreaterThanOperator_StringComparison_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["name"] = "zebra";

        // Act
        var result = ConditionEvaluator.Evaluate("output.name > 'apple'", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_NonBooleanNonNullValue_ShouldReturnTrue()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["isValid"] = "some value";

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void Evaluate_WithBooleanPropertyCheck_NullValue_ShouldReturnFalse()
    {
        // Arrange
        var context = new NodeExecutionContext();
        context.OutputData["isValid"] = null!;

        // Act
        var result = ConditionEvaluator.Evaluate("output.isValid", context);

        // Assert
        result.Should().BeFalse();
    }
}
