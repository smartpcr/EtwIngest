// -----------------------------------------------------------------------
// <copyright file="PowerShellTaskNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class PowerShellTaskNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.PowerShellTask;

        /// <summary>
        /// Gets or sets the path to the PowerShell script to execute.
        /// </summary>
        public string? ScriptPath { get; set; } = string.Empty;

        public string? ScriptContent { get; set; }

        /// <summary>
        /// Gets or sets the list of required PowerShell modules.
        /// </summary>
        public List<string>? RequiredModules { get; set; }

        /// <summary>
        /// Gets or sets custom module paths for PowerShell modules.
        /// Key: module name, Value: module path
        /// </summary>
        public Dictionary<string, string>? ModulePaths { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(this.ScriptPath) && string.IsNullOrEmpty(this.ScriptContent))
            {
                yield return  new ValidationResult(
                    "Script path and content cannot both be empty.",
                    new[] { nameof(this.ScriptPath), nameof(this.ScriptContent) });
            }

            if (!string.IsNullOrEmpty(this.ScriptPath))
            {
                // Normalize path separators for cross-platform compatibility
                // On Linux, backslashes are not recognized as path separators
                var normalizedPath = this.ScriptPath.Replace('\\', '/');
                this.ScriptPath = Path.GetFullPath(normalizedPath);

                if (!File.Exists(this.ScriptPath))
                {
                    yield return new  ValidationResult(
                        $"Script file does not exist on {this.ScriptPath}.",
                        new[] { nameof(this.ScriptPath) });
                }
            }
        }
    }
}