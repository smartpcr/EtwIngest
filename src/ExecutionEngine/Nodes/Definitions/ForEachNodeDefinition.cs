// -----------------------------------------------------------------------
// <copyright file="ForEachNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class ForEachNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.ForEach;

        public string CollectionExpression { get; set; } = string.Empty;

        public string ItemVariableName { get; set; } = "item";

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(this.CollectionExpression))
            {
                yield return new ValidationResult(
                    $"CollectionExpression must be provided when ScriptContent is not available.",
                    new[] { nameof(this.CollectionExpression) });
            }
        }

    }
}