// -----------------------------------------------------------------------
// <copyright file="CSharpScriptNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class CSharpScriptNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.CSharpScript;

        /// <summary>
        /// Gets or sets the path to the C# script to execute.
        /// </summary>
        [Required]
        public string ScriptPath { get; set; } = string.Empty;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(this.ScriptPath))
            {
                yield return new ValidationResult(
                    $"Script file {this.ScriptPath} does not exist.",
                    new[] { nameof(this.ScriptPath) });
            }

            // Resolve relative paths by joining with current directory
            if (!Path.IsPathRooted(this.ScriptPath))
            {
                this.ScriptPath = Path.Combine(Directory.GetCurrentDirectory(), this.ScriptPath!);
            }

            if (!File.Exists(this.ScriptPath))
            {
                yield return new ValidationResult(
                    $"Assembly file {this.ScriptPath} does not exist for {nameof(CSharpScriptNodeDefinition)}.",
                    new[] { nameof(this.ScriptPath) });
            }
        }

    }
}