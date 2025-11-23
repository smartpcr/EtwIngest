// -----------------------------------------------------------------------
// <copyright file="IfElseNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class IfElseNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.IfElse;

        public string Condition { get; set; } = string.Empty;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(this.Condition))
            {
                yield return new ValidationResult("Condition is required.", new[] { nameof(this.Condition) });
            }
        }
    }
}