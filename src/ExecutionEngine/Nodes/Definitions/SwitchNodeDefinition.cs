// -----------------------------------------------------------------------
// <copyright file="SwitchNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class SwitchNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.Switch;

        public string Expression { get; set; } = string.Empty;

        public Dictionary<string, string> Cases { get; set; } = new();

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(this.Expression))
            {
                yield return new ValidationResult("Expression cannot be null or empty.", new[] { nameof(this.Expression) });
            }

            foreach (var kvp in this.Cases)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    yield return new ValidationResult("Case key cannot be null or empty.", new[] { nameof(this.Cases) });
                }

                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    yield return new ValidationResult("Case value cannot be null or empty.", new[] { nameof(this.Cases) });
                }
            }
        }
    }
}