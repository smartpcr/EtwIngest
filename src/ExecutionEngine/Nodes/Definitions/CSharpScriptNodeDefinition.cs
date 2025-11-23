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
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content of the C# script to execute.
        /// </summary>
        public string ScriptContent { get; set; } = string.Empty;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(this.ScriptPath) && string.IsNullOrEmpty(this.ScriptContent))
            {
                yield return new ValidationResult(
                    $"Script file {this.ScriptPath} or script content not provided.",
                    new[] { nameof(this.ScriptPath), nameof(this.ScriptContent) });
            }

            if (!string.IsNullOrEmpty(this.ScriptPath))
            {
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
}