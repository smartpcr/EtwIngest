// -----------------------------------------------------------------------
// <copyright file="CSharpTaskNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using System.Reflection;
    using ExecutionEngine.Enums;

    public class CSharpTaskNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.CSharpTask;
        public string? ScriptContent { get; set; }

        public string? ExecutorTypeName { get; set; }

        public string? ExecutorAssemblyPath { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (this.MaxConcurrentExecutions < 0)
            {
                yield return new ValidationResult(
                    "MaxConcurrentExecutions cannot be negative.",
                    new[] { nameof(this.MaxConcurrentExecutions) });
            }

            if (string.IsNullOrEmpty(this.ScriptContent) &&
                (string.IsNullOrEmpty(this.ExecutorTypeName) || string.IsNullOrEmpty(this.ExecutorAssemblyPath)))
            {
                yield return new ValidationResult(
                    $"Either {nameof(this.ScriptContent)} or ExecutorAssemblyPath/ExecutorTypeName must be provided.",
                    new[] { nameof(this.ExecutorAssemblyPath), nameof(this.ExecutorTypeName) });
                yield break;
            }

            if (!string.IsNullOrEmpty(this.ExecutorAssemblyPath) && !string.IsNullOrEmpty(this.ExecutorTypeName))
            {
                // Normalize path separators for cross-platform compatibility
                // On Linux, backslashes are not recognized as path separators
                var normalizedPath = this.ExecutorAssemblyPath.Replace('\\', '/');
                this.ExecutorAssemblyPath = Path.GetFullPath(normalizedPath);
                if (!File.Exists(this.ExecutorAssemblyPath))
                {
                    yield return new ValidationResult(
                        $"Executor assembly file {this.ExecutorAssemblyPath} does not exist for {nameof(CSharpTaskNodeDefinition)}.",
                        new[] { nameof(this.ExecutorAssemblyPath) });
                }

                var assembly = Assembly.LoadFrom(this.ExecutorAssemblyPath);
                var type = assembly.GetType(this.ExecutorTypeName!);
                if (type == null)
                {
                    yield return new ValidationResult(
                        $"Type {this.ExecutorTypeName} is not found in assembly {this.ExecutorAssemblyPath} for CSharpNodeDefinition.",
                        new[] { nameof(this.ExecutorTypeName) });
                }
            }
        }
    }
}