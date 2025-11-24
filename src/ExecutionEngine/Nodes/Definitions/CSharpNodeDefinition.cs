// -----------------------------------------------------------------------
// <copyright file="CSharpNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using System.Reflection;
    using ExecutionEngine.Enums;

    public class CSharpNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.CSharp;

        /// <summary>
        /// Gets or sets the assembly path for compiled C# nodes.
        /// </summary>
        [Required]
        public string? AssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified type name for C# nodes.
        /// </summary>
        [Required]
        public string? TypeName { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(this.AssemblyPath) || string.IsNullOrEmpty(this.TypeName))
            {
                yield return new ValidationResult(
                    "AssemblyPath or TypeName are required for CSharpNodeDefinition.",
                    new[] { nameof(this.AssemblyPath), nameof(this.TypeName) });
                yield break;
            }

            // Normalize path separators for cross-platform compatibility
            // On Linux, backslashes are not recognized as path separators
            var normalizedPath = this.AssemblyPath!.Replace('\\', '/');
            this.AssemblyPath = Path.GetFullPath(normalizedPath);

            if (!File.Exists(this.AssemblyPath))
            {
                yield return new ValidationResult(
                    $"Assembly file {this.AssemblyPath} does not exist for CSharpNodeDefinition.",
                    new[] { nameof(this.AssemblyPath) });
            }

            var assembly = Assembly.LoadFrom(this.AssemblyPath);
            var type = assembly.GetType(this.TypeName!);
            if (type == null)
            {
                yield return new ValidationResult(
                    $"Type {this.TypeName} is not found in assembly {this.AssemblyPath} for CSharpNodeDefinition.",
                    new[] { nameof(this.TypeName) });
            }
        }

    }
}
