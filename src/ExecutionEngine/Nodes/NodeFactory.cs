// -----------------------------------------------------------------------
// <copyright file="NodeFactory.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ExecutionEngine.Core;
using ExecutionEngine.Nodes.Definitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Factory for creating and instantiating nodes from various sources.
/// Supports C# assemblies, C# scripts (Roslyn), and PowerShell scripts.
/// Uses Dependency Injection when available to resolve node instances with ILogger dependencies.
/// </summary>
public class NodeFactory
{
    private readonly ILogger<NodeFactory> logger;
    private readonly IServiceProvider? serviceProvider;
    private readonly Dictionary<string, Type> assemblyNodeCache;

    /// <summary>
    /// Initializes a new instance of the NodeFactory class.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider for DI-based node creation and logging.</param>
    public NodeFactory(IServiceProvider? serviceProvider = null)
    {
        this.serviceProvider = serviceProvider;

        // Get ILoggerFactory from service provider if available, otherwise use NullLoggerFactory
        var loggerFactory = serviceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;
        this.logger = loggerFactory.CreateLogger<NodeFactory>();

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
            this.logger.LogError("Cannot create node: definition is null");
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.NodeId))
        {
            this.logger.LogError("Cannot create node: NodeId is null or whitespace");
            throw new ArgumentException("NodeId cannot be null or whitespace.", nameof(definition));
        }

        var validationContext = new ValidationContext(definition);
        var validationResults = definition.Validate(validationContext)?.ToList();
        if (validationResults?.Any() == true)
        {
            foreach (var validationResult in validationResults)
            {
                this.logger.LogError("Node definition validation error for NodeId {NodeId}: {ErrorMessage}",
                    definition.NodeId, validationResult.ErrorMessage);
            }

            throw new ValidationException($"Node definition for NodeId {definition.NodeId} is invalid: {string.Join(Environment.NewLine, validationResults.Select(r => r.ErrorMessage))}");
        }

        this.logger.LogDebug("Creating node {NodeId} with RuntimeType: {RuntimeType}", definition.NodeId, definition.RuntimeType);

        try
        {
            ExecutableNodeBase? node;
            switch (definition.RuntimeType)
            {
                case Enums.RuntimeType.CSharp:
                    node = this.CreateCSharpNode(definition);
                    break;
                case Enums.RuntimeType.CSharpScript:
                    node = this.CreateCSharpScriptNode(definition);
                    break;
                case Enums.RuntimeType.CSharpTask:
                    node = this.CreateCSharpTaskNode(definition);
                    break;
                case Enums.RuntimeType.PowerShell:
                    node = this.CreatePowerShellScriptNode(definition);
                    break;
                case Enums.RuntimeType.PowerShellTask:
                    node = this.CreatePowerShellTaskNode(definition);
                    break;
                case Enums.RuntimeType.IfElse:
                    node = this.CreateIfElseNode(definition);
                    break;
                case Enums.RuntimeType.ForEach:
                    node = this.CreateForEachNode(definition);
                    break;
                case Enums.RuntimeType.While:
                    node = this.CreateWhileNode(definition);
                    break;
                case Enums.RuntimeType.Switch:
                    node = this.CreateSwitchNode(definition);
                    break;
                case Enums.RuntimeType.Subflow:
                    node = this.CreateSubflowNode(definition);
                    break;
                case Enums.RuntimeType.Timer:
                    node = this.CreateTimerNode(definition);
                    break;
                case Enums.RuntimeType.Container:
                    node = this.CreateContainerNode(definition);
                    break;
                default:
                    this.logger.LogError("Runtime type '{RuntimeType}' is not supported for NodeId {NodeId}", definition.RuntimeType, definition.NodeId);
                    throw new NotSupportedException($"Runtime type '{definition.RuntimeType}' is not supported.");
            }

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
            this.logger.LogError("Cannot create CSharp node: definition is not a CSharpNodeDefinition");
            throw new ArgumentException("Definition must be a CSharpNodeDefinition for CSharp runtime type.", nameof(definition));
        }

        // Normalize path separators for cross-platform compatibility
        // On Linux, backslashes are not recognized as path separators
        var normalizedPath = csharpDef.AssemblyPath!.Replace('\\', '/');
        var assemblyPath = Path.GetFullPath(normalizedPath);

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
            this.logger.LogError("Type '{TypeName}' not found in assembly '{AssemblyPath}'", csharpDef.TypeName, assemblyPath);
            throw new TypeLoadException($"Type '{csharpDef.TypeName}' not found in assembly '{assemblyPath}'.");
        }

        if (!typeof(INode).IsAssignableFrom(type))
        {
            this.logger.LogError("Type '{TypeName}' does not implement INode interface", csharpDef.TypeName);
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
            this.logger.LogError("Cannot create CSharpScript node: definition is not a CSharpScriptNodeDefinition");
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
            this.logger.LogError("Cannot create PowerShell node: definition is not a PowerShellScriptNodeDefinition");
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
            this.logger.LogError("Cannot create PowerShellTask node: definition is not a PowerShellTaskNodeDefinition");
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
    /// Creates an instance from a type using Dependency Injection when available, or reflection as fallback.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>The created instance.</returns>
    private ExecutableNodeBase? CreateInstanceFromType(Type type)
    {
        object? instance;

        // Try to resolve from DI container first (supports ILogger<T> dependencies)
        if (this.serviceProvider != null)
        {
            try
            {
                instance = ActivatorUtilities.CreateInstance(this.serviceProvider, type);
                this.logger.LogDebug("Created instance of {TypeName} using DI", type.FullName);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to create instance of {TypeName} using DI, falling back to Activator", type.FullName);
                instance = Activator.CreateInstance(type);
            }
        }
        else
        {
            // Fallback to Activator when no service provider available
            instance = Activator.CreateInstance(type);
            this.logger.LogDebug("Created instance of {TypeName} using Activator (no DI)", type.FullName);
        }

        if (instance == null)
        {
            this.logger.LogError("Failed to create instance of type '{TypeName}': instance is null", type.FullName);
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
