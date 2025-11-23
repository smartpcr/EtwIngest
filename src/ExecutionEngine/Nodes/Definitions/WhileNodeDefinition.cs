// -----------------------------------------------------------------------
// <copyright file="WhileNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class WhileNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.While;

        public string ConditionExpression { get; set; } = string.Empty;

        public int MaxIterations { get; set; } = 1000;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(this.ConditionExpression))
            {
                yield return new ValidationResult(
                    "ConditionExpression cannot be null or empty.",
                    new[] { nameof(this.ConditionExpression) });
            }

            if (this.MaxIterations <= 0)
            {
                yield return new ValidationResult(
                    "MaxIterations must be greater than zero.",
                    new[] { nameof(this.MaxIterations) });
            }
        }
    }
}