// -----------------------------------------------------------------------
// <copyright file="TimerNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class TimerNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.Timer;

        public string Schedule { get; set; } = string.Empty;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(this.Schedule))
            {
                yield return new ValidationResult("Schedule is required.", new[] { nameof(this.Schedule) });
            }
        }
    }
}