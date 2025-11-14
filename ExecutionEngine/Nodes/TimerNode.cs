// -----------------------------------------------------------------------
// <copyright file="TimerNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using NCrontab;

/// <summary>
/// Node that triggers workflow execution on a schedule using cron expressions.
/// </summary>
public class TimerNode : ExecutableNodeBase
{
    /// <summary>
    /// Gets or sets the cron schedule expression (e.g., "0 2 * * *" for 2 AM daily).
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the timer should trigger immediately on start.
    /// </summary>
    public bool TriggerOnStart { get; set; } = false;

    private CrontabSchedule? crontabSchedule;
    private DateTime? lastTrigger;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get schedule from definition configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("Schedule", out var scheduleValue))
        {
            this.Schedule = scheduleValue?.ToString() ?? string.Empty;
        }

        // Get trigger on start setting
        if (definition.Configuration != null && definition.Configuration.TryGetValue("TriggerOnStart", out var triggerOnStartValue))
        {
            if (bool.TryParse(triggerOnStartValue?.ToString(), out var triggerOnStart))
            {
                this.TriggerOnStart = triggerOnStart;
            }
        }

        // Parse and validate the cron schedule
        if (!string.IsNullOrWhiteSpace(this.Schedule))
        {
            try
            {
                this.crontabSchedule = CrontabSchedule.Parse(this.Schedule);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid cron schedule '{this.Schedule}': {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        var instance = new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = this.NodeId,
            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Running,
            StartTime = DateTime.UtcNow,
            ExecutionContext = nodeContext
        };

        try
        {
            this.RaiseOnStart(new NodeStartEventArgs
            {
                NodeId = this.NodeId,
                NodeInstanceId = instance.NodeInstanceId,
                Timestamp = DateTime.UtcNow
            });

            if (string.IsNullOrWhiteSpace(this.Schedule))
            {
                throw new InvalidOperationException("Timer schedule is not defined.");
            }

            if (this.crontabSchedule == null)
            {
                throw new InvalidOperationException("Timer schedule is not initialized.");
            }

            var now = DateTime.Now;

            // Check if we should trigger
            bool shouldTrigger = false;

            if (this.TriggerOnStart && !this.lastTrigger.HasValue)
            {
                // Trigger immediately on first run if configured
                shouldTrigger = true;
            }
            else
            {
                // Get the next scheduled occurrence from the last trigger (or now if no last trigger)
                var baseTime = this.lastTrigger ?? now.AddMinutes(-1);
                var nextOccurrence = this.crontabSchedule.GetNextOccurrence(baseTime);

                // If the next occurrence is in the past or now, trigger
                if (nextOccurrence <= now)
                {
                    shouldTrigger = true;
                }
            }

            if (shouldTrigger)
            {
                this.lastTrigger = now;

                // Set output data
                nodeContext.OutputData["TriggerTime"] = now;
                nodeContext.OutputData["Schedule"] = this.Schedule;
                nodeContext.OutputData["Triggered"] = true;

                instance.Status = NodeExecutionStatus.Completed;
            }
            else
            {
                // Not time to trigger yet
                var nextOccurrence = this.crontabSchedule.GetNextOccurrence(now);
                nodeContext.OutputData["NextTriggerTime"] = nextOccurrence;
                nodeContext.OutputData["Triggered"] = false;

                instance.Status = NodeExecutionStatus.Completed;
            }

            instance.EndTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
        }

        return await Task.FromResult(instance);
    }

    /// <summary>
    /// Gets the next scheduled trigger time.
    /// </summary>
    /// <returns>The next occurrence time.</returns>
    public DateTime? GetNextTriggerTime()
    {
        if (this.crontabSchedule == null)
        {
            return null;
        }

        var baseTime = this.lastTrigger ?? DateTime.Now;
        return this.crontabSchedule.GetNextOccurrence(baseTime);
    }
}
