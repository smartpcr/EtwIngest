// -----------------------------------------------------------------------
// <copyright file="SubflowNodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions
{
    using System.ComponentModel.DataAnnotations;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Workflow;
    using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

    public class SubflowNodeDefinition : NodeDefinition
    {
        public override RuntimeType RuntimeType => RuntimeType.Subflow;

        public string WorkflowFilePath { get; set; } = string.Empty;

        public WorkflowDefinition? WorkflowDefinition { get; set; } = null;

        public Dictionary<string, string> InputMappings { get; set; } = new();

        public Dictionary<string, string> OutputMappings { get; set; } = new();

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        public bool SkipValidation { get; set; } = false;

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(this.WorkflowFilePath) && this.WorkflowDefinition == null)
            {
                yield return new ValidationResult(
                    "Either workflowFilePath or workflowDefinition are required.",
                    new[] { nameof(this.WorkflowFilePath), nameof(this.WorkflowDefinition) });
            }

            if (!string.IsNullOrEmpty(this.WorkflowFilePath))
            {
                if (!File.Exists(this.WorkflowFilePath))
                {
                    yield return new ValidationResult(
                        $"WorkflowFile does not exist in {this.WorkflowFilePath}.",
                        new[] { nameof(this.WorkflowFilePath) });
                }
            }
        }
    }
}