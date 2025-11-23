// -----------------------------------------------------------------------
// <copyright file="NoopNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes
{
    using ExecutionEngine.Contexts;
    using ExecutionEngine.Core;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Nodes.Definitions;

    public class NoopNode : ExecutableNodeBase
    {
        public override void Initialize(NodeDefinition definition)
        {
            if (definition is not NoopNodeDefinition)
            {
                throw new ArgumentException($"Invalid node definition type: {definition.GetType().FullName}. Expected {typeof(NoopNodeDefinition).FullName}.");
            }

            this.Definition = definition;
        }

        public override async Task<NodeInstance> ExecuteAsync(WorkflowExecutionContext workflowContext, NodeExecutionContext nodeContext, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            await Task.Delay(10, cancellationToken);
            return new NodeInstance
            {
                NodeId = this.Definition!.NodeId,
                Status = NodeExecutionStatus.Completed,
                EndTime = DateTime.UtcNow,
                ExecutionContext = nodeContext,
                WorkflowInstanceId =  workflowContext.InstanceId,
                StartTime = startTime,
            };
        }
    }
}