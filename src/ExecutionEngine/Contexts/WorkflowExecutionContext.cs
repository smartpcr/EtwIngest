// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionContext.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Contexts;

using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Text.Json.Serialization;
using ExecutionEngine.Enums;
using ExecutionEngine.Events;
using ExecutionEngine.Queue;
using ExecutionEngine.Routing;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents the execution context for an entire workflow instance.
/// Contains workflow-level state, per-node message queues, and routing infrastructure.
/// Implements IDisposable for proper cleanup of observable subscriptions.
/// </summary>
public class WorkflowExecutionContext : IDisposable
{
    private readonly ReplaySubject<WorkflowEvent> eventsSubject = new();
    private readonly ReplaySubject<ProgressUpdate> progressSubject = new();
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the WorkflowExecutionContext class.
    /// </summary>
    public WorkflowExecutionContext()
    {
        this.InstanceId = Guid.NewGuid();
        this.StartTime = DateTime.UtcNow;
        this.Status = WorkflowExecutionStatus.Pending;
    }

    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the workflow graph ID.
    /// </summary>
    public string GraphId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow execution status.
    /// </summary>
    public WorkflowExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets the workflow start time.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets or sets the workflow end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the workflow-level global variables.
    /// Accessible by all nodes in the workflow.
    /// </summary>
    public ConcurrentDictionary<string, object> Variables { get; } = new();

    /// <summary>
    /// Gets the per-node message queues.
    /// Each node has its own dedicated queue for message isolation.
    /// Key: NodeId, Value: NodeMessageQueue
    /// </summary>
    public ConcurrentDictionary<string, object> NodeQueues { get; } = new();

    /// <summary>
    /// Gets or sets the message router for routing messages to target node queues.
    /// </summary>
    [JsonIgnore]
    public IMessageRouter? Router { get; set; }

    /// <summary>
    /// Gets or sets the dead letter queue for failed messages.
    /// </summary>
    [JsonIgnore]
    public IDeadLetterQueue? DeadLetterQueue { get; set; }

    /// <summary>
    /// Gets or sets the logger factory for creating loggers.
    /// Used by SubflowNode to create child workflow engines with logging.
    /// </summary>
    [JsonIgnore]
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets the duration of workflow execution.
    /// </summary>
    public TimeSpan? Duration => this.EndTime.HasValue ? this.EndTime.Value - this.StartTime : null;

    /// <summary>
    /// Gets the observable stream of workflow events.
    /// Subscribe to this to receive real-time state change notifications.
    /// </summary>
    [JsonIgnore]
    public IObservable<WorkflowEvent> Events => this.eventsSubject;

    /// <summary>
    /// Gets the observable stream of progress updates.
    /// Subscribe to this to receive real-time progress calculations.
    /// </summary>
    [JsonIgnore]
    public IObservable<ProgressUpdate> Progress => this.progressSubject;

    /// <summary>
    /// Publishes a workflow event to all subscribers.
    /// </summary>
    /// <param name="workflowEvent">The event to publish.</param>
    public void PublishEvent(WorkflowEvent workflowEvent)
    {
        if (!this.disposed)
        {
            this.eventsSubject.OnNext(workflowEvent);
        }
    }

    /// <summary>
    /// Publishes a progress update to all subscribers.
    /// </summary>
    /// <param name="progress">The progress update to publish.</param>
    public void PublishProgress(ProgressUpdate progress)
    {
        if (!this.disposed)
        {
            this.progressSubject.OnNext(progress);
        }
    }

    /// <summary>
    /// Disposes the workflow execution context and completes all observable streams.
    /// </summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.eventsSubject.OnCompleted();
            this.progressSubject.OnCompleted();
            this.eventsSubject.Dispose();
            this.progressSubject.Dispose();
        }
    }
}
