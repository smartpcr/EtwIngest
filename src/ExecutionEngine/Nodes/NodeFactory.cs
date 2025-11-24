// -----------------------------------------------------------------------
// <copyright file="NodeFactory.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Reflection;
using ExecutionEngine.Core;
using ExecutionEngine.Nodes.Definitions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Factory for creating and instantiating nodes from various sources.
/// Supports C# assemblies, C# scripts (Roslyn), and PowerShell scripts.
/// </summary>
public class NodeFactory
{
    private readonly ILogger<NodeFactory> logger;
    private readonly Dictionary<string, Type> assemblyNodeCache;

    /// <summary>
    /// Initializes a new instance of the NodeFactory class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public NodeFactory(ILogger<NodeFactory>? logger = null)
    {
        this.logger = logger ?? NullLogger<NodeFactory>.Instance;
        this.assemblyNodeCache = new Dictionary<string, Type>();
    }

    /// <summary>
    /// Creates a node instance from a node definition.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node instance.</returns>
    public ExecutableNodeBase? CreateNode(NodeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.NodeId))
        {
            this.logger.LogError("Cannot create node: NodeId is null or whitespace");
            throw new ArgumentException("NodeId cannot be null or whitespace.", nameof(definition));
        }

        this.logger.LogDebug("Creating node {NodeId} with RuntimeType: {RuntimeType}", definition.NodeId, definition.RuntimeType);

        try
        {
            var node = definition.RuntimeType switch
            {
                Enums.RuntimeType.CSharp => this.CreateCSharpNode(definition),
                Enums.RuntimeType.CSharpScript => this.CreateCSharpScriptNode(definition),
                Enums.RuntimeType.CSharpTask => this.CreateCSharpTaskNode(definition),
                Enums.RuntimeType.PowerShell => this.CreatePowerShellScriptNode(definition),
                Enums.RuntimeType.PowerShellTask => this.CreatePowerShellTaskNode(definition),
                Enums.RuntimeType.IfElse => this.CreateIfElseNode(definition),
                Enums.RuntimeType.ForEach => this.CreateForEachNode(definition),
                Enums.RuntimeType.While => this.CreateWhileNode(definition),
                Enums.RuntimeType.Switch => this.CreateSwitchNode(definition),
                Enums.RuntimeType.Subflow => this.CreateSubflowNode(definition),
                Enums.RuntimeType.Timer => this.CreateTimerNode(definition),
                Enums.RuntimeType.Container => this.CreateContainerNode(definition),
                _ => throw new NotSupportedException($"Runtime type '{definition.RuntimeType}' is not supported.")
            };

            this.logger.LogDebug("Node instance created for {NodeId}, calling Initialize()", definition.NodeId);
            node?.Initialize(definition);

            this.logger.LogInformation("Node {NodeId} initialized successfully (Type: {NodeType})",
                definition.NodeId, node?.NodeName);
            return node;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to create node {NodeId} with RuntimeType {RuntimeType}",
                definition.NodeId, definition.RuntimeType);
            throw;
        }
    }

    /// <summary>
    /// Creates a C# node from a compiled assembly.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase? CreateCSharpNode(NodeDefinition definition)
    {
        var csharpDef = definition as CSharpNodeDefinition;
        if (csharpDef == null)
        {
            throw new ArgumentException("Definition must be a CSharpNodeDefinition for CSharp runtime type.", nameof(definition));
        }

        // Resolve relative paths by joining with current directory
        var assemblyPath = csharpDef.AssemblyPath;
        if (!Path.IsPathRooted(assemblyPath))
        {
            assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), assemblyPath!);
            this.logger.LogDebug("Resolved relative path '{OriginalPath}' to '{ResolvedPath}'", csharpDef.AssemblyPath, assemblyPath);
        }

        // Check cache first (use resolved path for cache key)
        var cacheKey = $"{assemblyPath}::{csharpDef.TypeName}";
        if (this.assemblyNodeCache.TryGetValue(cacheKey, out var cachedType))
        {
            this.logger.LogDebug("Using cached type for {TypeName} from {AssemblyPath}", csharpDef.TypeName, assemblyPath);
            return this.CreateInstanceFromType(cachedType);
        }

        this.logger.LogDebug("Loading assembly from {AssemblyPath}", assemblyPath);

        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType(csharpDef.TypeName!);

        if (type == null)
        {
            throw new TypeLoadException($"Type '{csharpDef.TypeName}' not found in assembly '{assemblyPath}'.");
        }

        if (!typeof(INode).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Type '{csharpDef.TypeName}' does not implement INode interface.");
        }

        // Cache the type
        this.assemblyNodeCache[cacheKey] = type;

        return this.CreateInstanceFromType(type);
    }

    /// <summary>
    /// Creates a C# script node using Roslyn scripting.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase CreateCSharpScriptNode(NodeDefinition definition)
    {
        var scriptDefinition = definition as CSharpScriptNodeDefinition;
        if (scriptDefinition == null)
        {
            throw new ArgumentException("Definition must be a CSharpScriptNodeDefinition for CSharpScript runtime type.", nameof(definition));
        }

        return new CSharpScriptNode();
    }

    /// <summary>
    /// Creates a C# task node (supports both inline scripts and compiled executors).
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase CreateCSharpTaskNode(NodeDefinition definition)
    {
        // CSharpTaskNode supports both inline scripts (via Configuration["script"])
        // and compiled executors (set via SetExecutor after initialization)
        // No validation needed here - validation happens in Initialize/ExecuteAsync
        return new CSharpTaskNode();
    }

    /// <summary>
    /// Creates a PowerShell script node.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase CreatePowerShellScriptNode(NodeDefinition definition)
    {
        var scriptDefinition = definition as PowerShellScriptNodeDefinition;
        if (scriptDefinition == null)
        {
            throw new ArgumentException("Definition must be a PowerShellScriptNodeDefinition for PowerShell runtime type.", nameof(definition));
        }

        return new PowerShellScriptNode();
    }

    /// <summary>
    /// Creates a PowerShell task node (supports both inline scripts and script files).
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase CreatePowerShellTaskNode(NodeDefinition definition)
    {
        var psTaskDef = definition as PowerShellTaskNodeDefinition;
        if (psTaskDef == null)
        {
            throw new ArgumentException("Definition must be a PowerShellTaskNodeDefinition for PowerShellTask runtime type.", nameof(definition));
        }

        // PowerShellTaskNode supports both inline scripts (via Configuration["script"])
        // and script files (via ScriptPath)
        // No validation needed here - validation happens in Initialize/ExecuteAsync
        return new PowerShellTaskNode();
    }

    /// <summary>
    /// Creates an if-else control flow node.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private ExecutableNodeBase CreateIfElseNode(NodeDefinition definition)
    {
        // IfElseNode condition is provided via Configuration["Condition"]
        // Validation happens in Initialize/ExecuteAsync
        return new IfElseNode();
    }

    /// <summary>
    /// Creates a ForEach node for collection iteration.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created ForEach node.</returns>
    private ExecutableNodeBase CreateForEachNode(NodeDefinition definition)
    {
        // ForEachNode configuration (CollectionExpression, ItemVariableName) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        return new ForEachNode();
    }

    /// <summary>
    /// Creates a While node for condition-based iteration.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created While node.</returns>
    private ExecutableNodeBase CreateWhileNode(NodeDefinition definition)
    {
        // WhileNode configuration (Condition, MaxIterations) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        return new WhileNode();
    }

    /// <summary>
    /// Creates a Switch node for multi-way branching based on expression value.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created Switch node.</returns>
    private ExecutableNodeBase CreateSwitchNode(NodeDefinition definition)
    {
        // SwitchNode configuration (Expression, Cases) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        return new SwitchNode();
    }

    /// <summary>
    /// Creates a Subflow node for executing child workflows.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created Subflow node.</returns>
    private ExecutableNodeBase CreateSubflowNode(NodeDefinition definition)
    {
        // SubflowNode configuration (WorkflowFilePath, InputMappings, OutputMappings, Timeout) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        return new SubflowNode();
    }

    /// <summary>
    /// Creates a Timer node for scheduled workflow execution.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created Timer node.</returns>
    private ExecutableNodeBase CreateTimerNode(NodeDefinition definition)
    {
        // TimerNode configuration (Schedule, TriggerOnStart) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        return new TimerNode();
    }

    /// <summary>
    /// Creates a Container node for grouping related nodes.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created Container node.</returns>
    private ExecutableNodeBase CreateContainerNode(NodeDefinition definition)
    {
        return new ContainerNode();
    }

    /// <summary>
    /// Creates an instance from a type using reflection.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>The created instance.</returns>
    private ExecutableNodeBase? CreateInstanceFromType(Type type)
    {
        var instance = Activator.CreateInstance(type);
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of type '{type.FullName}'.");
        }

        return instance as ExecutableNodeBase;
    }

    /// <summary>
    /// Clears the assembly node cache.
    /// </summary>
    public void ClearCache()
    {
        this.assemblyNodeCache.Clear();
    }

    /// <summary>
    /// Gets the number of cached node types.
    /// </summary>
    public int CachedNodeCount => this.assemblyNodeCache.Count;
}
