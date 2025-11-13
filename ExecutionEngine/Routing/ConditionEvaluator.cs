// -----------------------------------------------------------------------
// <copyright file="ConditionEvaluator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Routing;

using System.Text.RegularExpressions;
using ExecutionEngine.Contexts;

/// <summary>
/// Evaluates simple conditional expressions for routing decisions.
/// Supports basic property access and comparisons (e.g., "output.status == 'success'").
/// Phase 2.3: Simple expression evaluator - not a full scripting engine.
/// </summary>
public class ConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition expression against a node execution context.
    /// </summary>
    /// <param name="condition">The condition expression to evaluate.</param>
    /// <param name="context">The node execution context containing output data.</param>
    /// <returns>True if the condition evaluates to true, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when condition or context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the expression is invalid.</exception>
    public static bool Evaluate(string condition, NodeExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentNullException(nameof(condition));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Trim whitespace
        condition = condition.Trim();

        // Pattern: output.propertyName == "value" or output.propertyName == 'value'
        // Also supports: !=, >, <, >=, <=
        // IMPORTANT: Match longer operators (>=, <=) before shorter ones (>, <)
        var equalityPattern = @"^output\.(\w+)\s*(==|!=|>=|<=|>|<)\s*['""]?([^'""]+)['""]?$";
        var match = Regex.Match(condition, equalityPattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var propertyName = match.Groups[1].Value;
            var operatorStr = match.Groups[2].Value;
            var expectedValue = match.Groups[3].Value;

            // Get the actual value from output data
            if (!context.OutputData.TryGetValue(propertyName, out var actualValue))
            {
                // Property not found - condition is false
                return false;
            }

            return EvaluateComparison(actualValue, operatorStr, expectedValue);
        }

        // Pattern: output.propertyName (boolean property check)
        var booleanPattern = @"^output\.(\w+)$";
        match = Regex.Match(condition, booleanPattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var propertyName = match.Groups[1].Value;

            if (!context.OutputData.TryGetValue(propertyName, out var value))
            {
                return false;
            }

            // Try to convert to boolean
            if (value is bool boolValue)
            {
                return boolValue;
            }

            // Try parsing as boolean
            if (bool.TryParse(value?.ToString(), out var parsedBool))
            {
                return parsedBool;
            }

            // Non-null value is truthy
            return value != null;
        }

        // Pattern: true or false literals
        if (condition.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"Invalid condition expression: {condition}");
    }

    private static bool EvaluateComparison(object? actualValue, string operatorStr, string expectedValue)
    {
        var actualStr = actualValue?.ToString() ?? string.Empty;

        switch (operatorStr)
        {
            case "==":
                return actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);

            case "!=":
                return !actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);

            case ">":
            case "<":
            case ">=":
            case "<=":
                // Try numeric comparison
                if (double.TryParse(actualStr, out var actualNum) &&
                    double.TryParse(expectedValue, out var expectedNum))
                {
                    return operatorStr switch
                    {
                        ">" => actualNum > expectedNum,
                        "<" => actualNum < expectedNum,
                        ">=" => actualNum >= expectedNum,
                        "<=" => actualNum <= expectedNum,
                        _ => false
                    };
                }

                // Fallback to string comparison
                var compareResult = string.Compare(actualStr, expectedValue, StringComparison.OrdinalIgnoreCase);
                return operatorStr switch
                {
                    ">" => compareResult > 0,
                    "<" => compareResult < 0,
                    ">=" => compareResult >= 0,
                    "<=" => compareResult <= 0,
                    _ => false
                };

            default:
                throw new InvalidOperationException($"Unsupported operator: {operatorStr}");
        }
    }
}
