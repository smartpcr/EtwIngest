// -----------------------------------------------------------------------
// <copyright file="ContainerNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Collections.Concurrent;
using System.Text.Json;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Messages;
using ExecutionEngine.Routing;
using ExecutionEngine.Workflow;

/// <summary>
/// Container node that groups related nodes into a logical unit with encapsulated execution flow.
/// Behaves like SubflowNode (OnComplete when all children succeed, OnFail when any child fails)
/// but defines children inline in the same workflow file instead of external file reference.
/// </summary>
public class ContainerNode : ExecutableNodeBase
{
    private List<NodeDefinition> childNodes = new List<NodeDefinition>();
    private List<NodeConnection> childConnections = new List<NodeConnection>();
    private string executionMode = "Mixed";
    private Dictionary<string, INode> childNodeInstances = new Dictionary<string, INode>();
    private ChildCompletionState completionState = new ChildCompletionState();
    private List<NodeDefinition> entryPointNodes = new List<NodeDefinition>();
    private List<NodeDefinition> exitPointNodes = new List<NodeDefinition>();
    private NodeFactory nodeFactory = new NodeFactory();
    private readonly object stateLock = new object();

    /// <summary>
    /// Gets or sets the child nodes contained within this container.
    /// </summary>
    public List<NodeDefinition> ChildNodes
    {
        get => this.childNodes;
        set => this.childNodes = value ?? new List<NodeDefinition>();
    }

    /// <summary>
    /// Gets or sets the connections between child nodes (defines internal flow).
    /// </summary>
    public List<NodeConnection> ChildConnections
    {
        get => this.childConnections;
        set => this.childConnections = value ?? new List<NodeConnection>();
    }

    /// <summary>
    /// Gets or sets the execution mode for children.
    /// Parallel, Sequential, or Mixed (determined by ChildConnections).
    /// </summary>
    public string ExecutionMode
    {
        get => this.executionMode;
        set => this.executionMode = value ?? "Mixed";
    }

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        Console.WriteLine($"[ContainerNode.Initialize] START for {definition.NodeId}");
        base.Initialize(definition);
        Console.WriteLine($"[ContainerNode.Initialize] base.Initialize completed");

        if (definition.Configuration != null)
        {
            // Load ChildNodes
            if (definition.Configuration.TryGetValue("ChildNodes", out var childNodesObj) && childNodesObj != null)
            {
                Console.WriteLine($"[ContainerNode.Initialize] Parsing {childNodesObj.GetType().Name} child nodes");
                this.childNodes = this.ParseChildNodes(childNodesObj);
                Console.WriteLine($"[ContainerNode.Initialize] Parsed {this.childNodes.Count} child nodes");
            }

            // Load ChildConnections
            if (definition.Configuration.TryGetValue("ChildConnections", out var childConnectionsObj))
            {
                Console.WriteLine($"[ContainerNode.Initialize] Parsing child connections");
                this.childConnections = this.ParseChildConnections(childConnectionsObj);
                Console.WriteLine($"[ContainerNode.Initialize] Parsed {this.childConnections.Count} child connections");
            }

            // Load ExecutionMode
            if (definition.Configuration.TryGetValue("ExecutionMode", out var execMode))
            {
                this.executionMode = execMode?.ToString() ?? "Mixed";
                Console.WriteLine($"[ContainerNode.Initialize] ExecutionMode: {this.executionMode}");
            }
        }

        // Validate configuration
        Console.WriteLine($"[ContainerNode.Initialize] Validating configuration");
        this.ValidateConfiguration();
        Console.WriteLine($"[ContainerNode.Initialize] Validation complete");

        // Detect entry and exit points
        Console.WriteLine($"[ContainerNode.Initialize] Detecting entry/exit points");
        this.DetectEntryAndExitPoints();
        Console.WriteLine($"[ContainerNode.Initialize] Entry/exit detection complete");

        // NOTE: Child nodes are NOT instantiated during Initialize() to avoid
        // circular dependencies and initialization hangs. They will be created
        // on-demand during ExecuteAsync().
        Console.WriteLine($"[ContainerNode.Initialize] COMPLETE for {definition.NodeId}");
    }

    /// <inheritdoc/>
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ContainerNode:{this.NodeId}] ExecuteAsync called");
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
            Console.WriteLine($"[ContainerNode:{this.NodeId}] Raising OnStart event");
            this.RaiseOnStart(new NodeStartEventArgs
            {
                NodeId = this.NodeId,
                NodeInstanceId = instance.NodeInstanceId,
                Timestamp = DateTime.UtcNow
            });

            // Instantiate child nodes on-demand (not during Initialize to avoid circular deps)
            Console.WriteLine($"[ContainerNode:{this.NodeId}] Instantiating {this.childNodes.Count} child nodes");
            this.InstantiateChildNodes();
            Console.WriteLine($"[ContainerNode:{this.NodeId}] Child nodes instantiated successfully");

            // Initialize completion state
            this.completionState = new ChildCompletionState
            {
                TotalChildren = this.childNodes.Count,
                ChildInstances = new Dictionary<string, NodeInstance>(),
                CompletedChildren = new HashSet<string>(),
                PendingChildren = new HashSet<string>(this.childNodes.Select(n => n.NodeId)),
                RunningChildren = new HashSet<string>()
            };

            Console.WriteLine($"[ContainerNode:{this.NodeId}] Starting {this.entryPointNodes.Count} entry point children");

            // Phase 1: Start entry-point children
            var entryPointTasks = new List<Task>();
            foreach (var entryPoint in this.entryPointNodes)
            {
                var task = this.StartChildNodeAsync(entryPoint, workflowContext, nodeContext, cancellationToken);
                entryPointTasks.Add(task);
            }

            // Wait for entry points to start
            await Task.WhenAll(entryPointTasks);

            // Phase 2: Monitor child execution and route messages
            var isComplete = false;
            var hasFailed = false;

            while (!isComplete && !hasFailed)
            {
                await Task.Delay(100, cancellationToken);

                // Check for completed children and route their messages
                await this.ProcessChildCompletionsAsync(workflowContext, cancellationToken);

                // Report overall progress
                lock (this.stateLock)
                {
                    var completedCount = this.completionState.CompletedChildren.Count;
                    var totalCount = this.completionState.TotalChildren;
                    var overallProgress = totalCount > 0 ? (completedCount * 100 / totalCount) : 0;

                    this.RaiseOnProgress(new ProgressEventArgs
                    {
                        Status = $"Container progress: {completedCount}/{totalCount} children completed",
                        ProgressPercent = overallProgress
                    });
                }

                // Check completion status with lock
                lock (this.stateLock)
                {
                    isComplete = this.completionState.IsComplete();
                    hasFailed = this.completionState.HasFailed;

                    // Check for failures (SubflowNode semantics: fail immediately on any child failure)
                    if (hasFailed)
                    {
                        instance.Status = NodeExecutionStatus.Failed;
                        instance.EndTime = DateTime.UtcNow;
                        instance.ErrorMessage = $"Child '{this.completionState.FailedChildId}' failed: {this.completionState.FailedChildError}";

                        // Set failure output data
                        nodeContext.OutputData["FailedChildId"] = this.completionState.FailedChildId ?? string.Empty;
                        nodeContext.OutputData["FailedChildError"] = this.completionState.FailedChildError ?? string.Empty;
                        nodeContext.OutputData["CompletedChildren"] = this.completionState.CompletedChildren.Count;
                        nodeContext.OutputData["TotalChildren"] = this.completionState.TotalChildren;

                        return instance;
                    }
                }
            }

            // Phase 3: All children succeeded - aggregate results and complete
            this.AggregateChildOutputs(nodeContext);
            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Container execution was cancelled";
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
        }

        return instance;
    }

    /// <summary>
    /// Validates the container configuration.
    /// </summary>
    private void ValidateConfiguration()
    {
        // Validate ChildNodes is not null or empty
        if (this.childNodes == null || this.childNodes.Count == 0)
        {
            throw new InvalidOperationException("ChildNodes cannot be null or empty. Container must have at least one child node.");
        }

        // Validate ExecutionMode
        var validModes = new[] { "Parallel", "Sequential", "Mixed" };
        if (!validModes.Contains(this.executionMode, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid ExecutionMode '{this.executionMode}'. Must be one of: {string.Join(", ", validModes)}");
        }

        // Validate all node IDs in ChildConnections exist in ChildNodes
        var childNodeIds = new HashSet<string>(this.childNodes.Select(n => n.NodeId));
        foreach (var connection in this.childConnections)
        {
            if (!childNodeIds.Contains(connection.SourceNodeId))
            {
                throw new InvalidOperationException($"Invalid ChildConnection: source node '{connection.SourceNodeId}' not found in ChildNodes.");
            }

            if (!childNodeIds.Contains(connection.TargetNodeId))
            {
                throw new InvalidOperationException($"Invalid ChildConnection: target node '{connection.TargetNodeId}' not found in ChildNodes.");
            }
        }

        // Detect circular references
        this.DetectCircularReferences();
    }

    /// <summary>
    /// Detects circular references in ChildConnections using DFS.
    /// </summary>
    private void DetectCircularReferences()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in this.childNodes)
        {
            if (this.HasCycle(node.NodeId, visited, recursionStack))
            {
                throw new InvalidOperationException("Circular reference detected in ChildConnections. Container cannot have cycles.");
            }
        }
    }

    /// <summary>
    /// DFS helper to detect cycles.
    /// </summary>
    private bool HasCycle(string nodeId, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
        {
            return true; // Cycle detected
        }

        if (visited.Contains(nodeId))
        {
            return false; // Already processed
        }

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        // Get outgoing connections
        var outgoingConnections = this.childConnections.Where(c => c.SourceNodeId == nodeId);
        foreach (var connection in outgoingConnections)
        {
            if (this.HasCycle(connection.TargetNodeId, visited, recursionStack))
            {
                return true;
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }

    /// <summary>
    /// Detects entry points (nodes with no incoming connections) and exit points (nodes with no outgoing connections).
    /// </summary>
    private void DetectEntryAndExitPoints()
    {
        var nodeIds = new HashSet<string>(this.childNodes.Select(n => n.NodeId));
        var nodesWithIncoming = new HashSet<string>(this.childConnections.Select(c => c.TargetNodeId));
        var nodesWithOutgoing = new HashSet<string>(this.childConnections.Select(c => c.SourceNodeId));

        // Entry points: nodes with no incoming connections
        this.entryPointNodes = this.childNodes
            .Where(n => !nodesWithIncoming.Contains(n.NodeId))
            .ToList();

        // Exit points: nodes with no outgoing connections
        this.exitPointNodes = this.childNodes
            .Where(n => !nodesWithOutgoing.Contains(n.NodeId))
            .ToList();

        // If no connections exist, all nodes are both entry and exit points
        if (this.childConnections.Count == 0)
        {
            this.entryPointNodes = new List<NodeDefinition>(this.childNodes);
            this.exitPointNodes = new List<NodeDefinition>(this.childNodes);
        }
    }

    /// <summary>
    /// Instantiates child nodes using NodeFactory.
    /// </summary>
    private void InstantiateChildNodes()
    {
        // Fix assembly paths for child nodes before instantiation
        // Use parent container's assembly path as the base for all children
        var parentAssemblyPath = this.definition?.AssemblyPath;

        foreach (var childDef in this.childNodes)
        {
            // If child has a relative assembly path and parent has an absolute path, use parent's path
            if (!string.IsNullOrEmpty(childDef.AssemblyPath) &&
                !string.IsNullOrEmpty(parentAssemblyPath) &&
                childDef.AssemblyPath.StartsWith("./"))
            {
                childDef.AssemblyPath = parentAssemblyPath;
            }

            var childNode = this.nodeFactory.CreateNode(childDef);
            this.childNodeInstances[childDef.NodeId] = childNode;
        }
    }

    /// <summary>
    /// Creates internal router for routing messages between children.
    /// </summary>
    private void CreateInternalRouter()
    {
        // Internal router is not needed in this implementation
        // We manually route child completion messages based on ChildConnections
        // This method is kept for future enhancement
    }

    /// <summary>
    /// Starts a child node execution.
    /// </summary>
    private async Task StartChildNodeAsync(
        NodeDefinition childDef,
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext parentContext,
        CancellationToken cancellationToken)
    {
        if (!this.childNodeInstances.TryGetValue(childDef.NodeId, out var childNode))
        {
            throw new InvalidOperationException($"Child node '{childDef.NodeId}' not instantiated.");
        }

        // Create child execution context
        var childContext = new NodeExecutionContext
        {
            InputData = new Dictionary<string, object>(parentContext.OutputData),
            OutputData = new Dictionary<string, object>()
        };

        // Mark as running
        lock (this.stateLock)
        {
            this.completionState.PendingChildren.Remove(childDef.NodeId);
            this.completionState.RunningChildren.Add(childDef.NodeId);
        }

        // Subscribe to child node events to bubble them up as progress
        childNode.OnStart += (sender, e) =>
        {
            this.RaiseOnProgress(new ProgressEventArgs
            {
                Status = $"[{childDef.NodeId}] Started",
                ProgressPercent = 0
            });
        };

        childNode.OnProgress += (sender, e) =>
        {
            // Forward child node progress with hierarchical key
            // Don't wrap e.Status if it's already a lifecycle message (contains brackets)
            // Only wrap raw progress messages from the child node itself
            var status = e.Status;
            if (!status.StartsWith("["))
            {
                // Raw progress message from child - wrap it with hierarchical key
                status = $"[{childDef.NodeId}] {status}";
            }
            // else: Already formatted by lifecycle/nested events, forward as-is

            this.RaiseOnProgress(new ProgressEventArgs
            {
                Status = status,
                ProgressPercent = e.ProgressPercent
            });
        };

        // Execute child node asynchronously (fire and forget with tracking)
        _ = Task.Run(async () =>
        {
            try
            {
                var childInstance = await childNode.ExecuteAsync(workflowContext, childContext, cancellationToken);

                lock (this.stateLock)
                {
                    this.completionState.ChildInstances[childDef.NodeId] = childInstance;

                    // Move from running to completed
                    this.completionState.RunningChildren.Remove(childDef.NodeId);

                    if (childInstance.Status == NodeExecutionStatus.Failed)
                    {
                        // Record failure (SubflowNode semantics: fail on ANY child failure)
                        this.completionState.FailedChildId = childDef.NodeId;
                        this.completionState.FailedChildError = childInstance.ErrorMessage ?? "Unknown error";

                        // Report failure progress
                        this.RaiseOnProgress(new ProgressEventArgs
                        {
                            Status = $"[{childDef.NodeId}] Failed: {childInstance.ErrorMessage}",
                            ProgressPercent = 100
                        });
                    }
                    else if (childInstance.Status == NodeExecutionStatus.Completed)
                    {
                        this.completionState.CompletedChildren.Add(childDef.NodeId);

                        // Report completion progress
                        var duration = childInstance.Duration?.TotalSeconds ?? 0;
                        this.RaiseOnProgress(new ProgressEventArgs
                        {
                            Status = $"[{childDef.NodeId}] Completed in {duration:F1}s",
                            ProgressPercent = 100
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Record failure
                lock (this.stateLock)
                {
                    this.completionState.RunningChildren.Remove(childDef.NodeId);
                    this.completionState.FailedChildId = childDef.NodeId;
                    this.completionState.FailedChildError = ex.Message;
                }

                // Report exception progress
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"[{childDef.NodeId}] Exception: {ex.Message}",
                    ProgressPercent = 100
                });
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Processes child completions and routes messages to dependent children.
    /// </summary>
    private async Task ProcessChildCompletionsAsync(
        WorkflowExecutionContext workflowContext,
        CancellationToken cancellationToken)
    {
        // Get newly completed children (with lock to safely read state)
        List<KeyValuePair<string, NodeInstance>> newlyCompleted;
        lock (this.stateLock)
        {
            newlyCompleted = this.completionState.ChildInstances
                .Where(kvp => kvp.Value != null &&
                              kvp.Value.Status == NodeExecutionStatus.Completed &&
                              this.completionState.CompletedChildren.Contains(kvp.Key))
                .ToList();
        }

        foreach (var (childId, childInstance) in newlyCompleted)
        {
            // Find outgoing connections from this child
            var outgoingConnections = this.childConnections
                .Where(c => c.SourceNodeId == childId && c.IsEnabled)
                .ToList();

            foreach (var connection in outgoingConnections)
            {
                var shouldStartChild = false;
                NodeDefinition? targetDef = null;

                // Check if target node is ready to execute (all its dependencies completed)
                lock (this.stateLock)
                {
                    if (this.IsChildReadyToExecute(connection.TargetNodeId) &&
                        this.completionState.PendingChildren.Contains(connection.TargetNodeId))
                    {
                        targetDef = this.childNodes.First(n => n.NodeId == connection.TargetNodeId);
                        shouldStartChild = true;
                    }
                }

                if (shouldStartChild && targetDef != null)
                {
                    var parentContext = new NodeExecutionContext
                    {
                        OutputData = childInstance.ExecutionContext?.OutputData ?? new Dictionary<string, object>()
                    };

                    await this.StartChildNodeAsync(targetDef, workflowContext, parentContext, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Checks if a child node is ready to execute (all dependencies completed).
    /// </summary>
    private bool IsChildReadyToExecute(string childId)
    {
        // Get all incoming connections to this child
        var incomingConnections = this.childConnections
            .Where(c => c.TargetNodeId == childId && c.IsEnabled)
            .ToList();

        // If no incoming connections, it's an entry point (already started)
        if (incomingConnections.Count == 0)
        {
            return false; // Already handled as entry point
        }

        // Check if all source nodes have completed
        return incomingConnections.All(c => this.completionState.CompletedChildren.Contains(c.SourceNodeId));
    }

    /// <summary>
    /// Aggregates child output data into container's output.
    /// </summary>
    private void AggregateChildOutputs(NodeExecutionContext nodeContext)
    {
        var childResults = new Dictionary<string, Dictionary<string, object>>();

        lock (this.stateLock)
        {
            foreach (var (childId, childInstance) in this.completionState.ChildInstances)
            {
                if (childInstance != null &&
                    childInstance.Status == NodeExecutionStatus.Completed &&
                    childInstance.ExecutionContext != null)
                {
                    childResults[childId] = new Dictionary<string, object>(childInstance.ExecutionContext.OutputData);
                }
            }

            nodeContext.OutputData["ChildResults"] = childResults;
            nodeContext.OutputData["TotalChildren"] = this.completionState.TotalChildren;
            nodeContext.OutputData["CompletedChildren"] = this.completionState.CompletedChildren.Count;
            nodeContext.OutputData["ExecutionMode"] = this.executionMode;
        }
    }

    /// <summary>
    /// Parses ChildNodes from configuration object.
    /// </summary>
    private List<NodeDefinition> ParseChildNodes(object childNodesObj)
    {
        Console.WriteLine($"[ContainerNode.ParseChildNodes] START, type={childNodesObj?.GetType().Name}");

        if (childNodesObj is List<NodeDefinition> nodeDefList)
        {
            Console.WriteLine($"[ContainerNode.ParseChildNodes] Direct cast successful, {nodeDefList.Count} nodes");
            return nodeDefList;
        }

        // Try casting as IEnumerable and converting elements
        if (childNodesObj is System.Collections.IEnumerable enumerable)
        {
            Console.WriteLine($"[ContainerNode.ParseChildNodes] Converting from IEnumerable");
            var result = new List<NodeDefinition>();
            var itemIndex = 0;
            foreach (var item in enumerable)
            {
                Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex}: type={item?.GetType().Name}");

                if (item is NodeDefinition nodeDef)
                {
                    result.Add(nodeDef);
                    Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex} cast to NodeDefinition successfully");
                }
                else if (item is System.Collections.IDictionary dict)
                {
                    // YAML deserializes nested nodes as dictionaries, convert them to NodeDefinition
                    try
                    {
                        // Use JSON serialization to convert dictionary to NodeDefinition
                        var json = System.Text.Json.JsonSerializer.Serialize(dict);
                        var jsonOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                        };
                        var nodeDefinition = System.Text.Json.JsonSerializer.Deserialize<NodeDefinition>(json, jsonOptions);

                        if (nodeDefinition != null)
                        {
                            result.Add(nodeDefinition);
                            Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex} converted from Dictionary to NodeDefinition: {nodeDefinition.NodeId}");
                        }
                        else
                        {
                            Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex} conversion resulted in null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex} conversion failed: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[ContainerNode.ParseChildNodes] Item {itemIndex} is NOT a NodeDefinition or Dictionary, skipping");
                }
                itemIndex++;
            }
            Console.WriteLine($"[ContainerNode.ParseChildNodes] Converted {result.Count} nodes from IEnumerable (total items: {itemIndex})");
            return result;
        }

        Console.WriteLine($"[ContainerNode.ParseChildNodes] WARNING: Unable to parse child nodes, returning empty list");
        return new List<NodeDefinition>();
    }

    /// <summary>
    /// Parses ChildConnections from configuration object.
    /// </summary>
    private List<NodeConnection> ParseChildConnections(object childConnectionsObj)
    {
        if (childConnectionsObj is List<NodeConnection> connList)
        {
            return connList;
        }

        // Try casting as IEnumerable and converting elements
        if (childConnectionsObj is System.Collections.IEnumerable enumerable)
        {
            var result = new List<NodeConnection>();
            foreach (var item in enumerable)
            {
                if (item is NodeConnection conn)
                {
                    result.Add(conn);
                }
            }
            return result;
        }

        return new List<NodeConnection>();
    }

    /// <summary>
    /// Tracks child completion state.
    /// </summary>
    private class ChildCompletionState
    {
        public Dictionary<string, NodeInstance> ChildInstances { get; set; } = new Dictionary<string, NodeInstance>();

        public HashSet<string> CompletedChildren { get; set; } = new HashSet<string>();

        public HashSet<string> PendingChildren { get; set; } = new HashSet<string>();

        public HashSet<string> RunningChildren { get; set; } = new HashSet<string>();

        public int TotalChildren { get; set; }

        public string? FailedChildId { get; set; }

        public string? FailedChildError { get; set; }

        public bool HasFailed => !string.IsNullOrEmpty(this.FailedChildId);

        public bool IsComplete()
        {
            // Container is complete when ALL children succeed
            // Container fails immediately when ANY child fails
            return this.CompletedChildren.Count == this.TotalChildren && !this.HasFailed;
        }
    }
}
