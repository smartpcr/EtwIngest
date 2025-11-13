// -----------------------------------------------------------------------
// <copyright file="WorkflowCheckpoint.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Collections.Generic;
using ExecutionEngine.Enums;

/// <summary>
/// Represents a serializable snapshot of workflow execution state.
/// Used for checkpoint creation, pause/resume, and failure recovery.
/// </summary>
public class WorkflowCheckpoint
{
    /// <summary>
    /// Gets or sets the unique identifier for the workflow instance.
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized workflow definition (JSON).
    /// Required for resuming execution from checkpoint.
    /// </summary>
    public string WorkflowDefinitionJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow execution status at checkpoint time.
    /// </summary>
    public WorkflowExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the workflow start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the checkpoint creation timestamp.
    /// </summary>
    public DateTime CheckpointTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the workflow-level global variables.
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// Gets or sets the serialized node instance states.
    /// Key: NodeId, Value: NodeInstance state
    /// </summary>
    public Dictionary<string, NodeInstanceState> NodeStates { get; set; } = new();

    /// <summary>
    /// Gets or sets the serialized message queue states.
    /// Key: NodeId, Value: Queue messages
    /// </summary>
    public Dictionary<string, List<SerializedMessage>> MessageQueues { get; set; } = new();

    /// <summary>
    /// Gets or sets error messages collected during execution.
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metadata for the checkpoint.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the serializable state of a node instance.
/// </summary>
public class NodeInstanceState
{
    /// <summary>
    /// Gets or sets the node ID.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node execution status.
    /// </summary>
    public NodeExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the node start time.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the node end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the error message if the node failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the input data for the node.
    /// </summary>
    public Dictionary<string, object> InputData { get; set; } = new();

    /// <summary>
    /// Gets or sets the output data from the node.
    /// </summary>
    public Dictionary<string, object> OutputData { get; set; } = new();
}

/// <summary>
/// Represents a serialized message in the queue.
/// </summary>
public class SerializedMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the message type (Complete, Fail, Progress).
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized message payload.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets when the message becomes visible again (for retries).
    /// </summary>
    public DateTime? NotBefore { get; set; }
}
