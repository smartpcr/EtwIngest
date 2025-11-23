// -----------------------------------------------------------------------
// <copyright file="PowerShellScriptNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;

    public class PowerShellScriptNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.PowerShell;

        /// <summary>
        /// Gets or sets the path to the PowerShell script to execute.
        /// </summary>
        [Required]
        public string ScriptPath { get; set; } = string.Empty;

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
            if (string.IsNullOrEmpty(this.ScriptPath))
            {
                yield return  new ValidationResult("Script path cannot be empty.", new[] { nameof(this.ScriptPath) });
            }
        }
    }
}