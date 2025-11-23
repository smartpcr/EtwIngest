// -----------------------------------------------------------------------
// <copyright file="CSharpTaskNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class CSharpTaskNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.CSharpTask;
        public string? ScriptContent { get; set; }

        public string? ExecutorTypeName { get; set; }

        public string? ExecutorAssemblyPath { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(this.ScriptContent))
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(this.ExecutorTypeName) || string.IsNullOrEmpty(this.ExecutorAssemblyPath))
            {
                yield return new ValidationResult(
                    "Both ExecutorAssemblyPath and ExecutorTypeName must be provided when ScriptContent is not available.",
                    new[] { nameof(this.ExecutorAssemblyPath), nameof(this.ExecutorTypeName) });
            }
        }

    }
}