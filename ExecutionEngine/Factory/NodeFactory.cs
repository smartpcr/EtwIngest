// -----------------------------------------------------------------------
// <copyright file="NodeFactory.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Factory;

using System.Reflection;
using ExecutionEngine.Core;
using ExecutionEngine.Nodes;

/// <summary>
/// Factory for creating and instantiating nodes from various sources.
/// Supports C# assemblies, C# scripts (Roslyn), and PowerShell scripts.
/// </summary>
public class NodeFactory
{
    private readonly Dictionary<string, Type> assemblyNodeCache;

    /// <summary>
    /// Initializes a new instance of the NodeFactory class.
    /// </summary>
    public NodeFactory()
    {
        this.assemblyNodeCache = new Dictionary<string, Type>();
    }

    /// <summary>
    /// Creates a node instance from a node definition.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node instance.</returns>
    public INode CreateNode(NodeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.NodeId))
        {
            throw new ArgumentException("NodeId cannot be null or whitespace.", nameof(definition));
        }

        Console.WriteLine($"[NodeFactory.CreateNode] Creating node {definition.NodeId} with RuntimeType: {definition.RuntimeType}");

        INode node = definition.RuntimeType switch
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

        Console.WriteLine($"[NodeFactory.CreateNode] Node instance created for {definition.NodeId}, calling Initialize()");
        node.Initialize(definition);
        Console.WriteLine($"[NodeFactory.CreateNode] Initialize() completed for {definition.NodeId}");
        return node;
    }

    /// <summary>
    /// Creates a C# node from a compiled assembly.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private INode CreateCSharpNode(NodeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.AssemblyPath))
        {
            throw new ArgumentException("AssemblyPath is required for CSharp runtime type.", nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.TypeName))
        {
            throw new ArgumentException("TypeName is required for CSharp runtime type.", nameof(definition));
        }

        // Check cache first
        var cacheKey = $"{definition.AssemblyPath}::{definition.TypeName}";
        if (this.assemblyNodeCache.TryGetValue(cacheKey, out var cachedType))
        {
            return this.CreateInstanceFromType(cachedType);
        }

        // Load assembly and type
        if (!File.Exists(definition.AssemblyPath))
        {
            throw new FileNotFoundException($"Assembly not found: {definition.AssemblyPath}");
        }

        var assembly = Assembly.LoadFrom(definition.AssemblyPath);
        var type = assembly.GetType(definition.TypeName);

        if (type == null)
        {
            throw new TypeLoadException($"Type '{definition.TypeName}' not found in assembly '{definition.AssemblyPath}'.");
        }

        if (!typeof(INode).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Type '{definition.TypeName}' does not implement INode interface.");
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
    private INode CreateCSharpScriptNode(NodeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ScriptPath))
        {
            throw new ArgumentException("ScriptPath is required for CSharpScript runtime type.", nameof(definition));
        }

        return new CSharpScriptNode();
    }

    /// <summary>
    /// Creates a C# task node (supports both inline scripts and compiled executors).
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private INode CreateCSharpTaskNode(NodeDefinition definition)
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
    private INode CreatePowerShellScriptNode(NodeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ScriptPath))
        {
            throw new ArgumentException("ScriptPath is required for PowerShell runtime type.", nameof(definition));
        }

        return new PowerShellScriptNode();
    }

    /// <summary>
    /// Creates a PowerShell task node (supports both inline scripts and script files).
    /// </summary>
    /// <param name="definition">The node definition.</param>
    /// <returns>The created node.</returns>
    private INode CreatePowerShellTaskNode(NodeDefinition definition)
    {
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
    private INode CreateIfElseNode(NodeDefinition definition)
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
    private INode CreateForEachNode(NodeDefinition definition)
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
    private INode CreateWhileNode(NodeDefinition definition)
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
    private INode CreateSwitchNode(NodeDefinition definition)
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
    private INode CreateSubflowNode(NodeDefinition definition)
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
    private INode CreateTimerNode(NodeDefinition definition)
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
    private INode CreateContainerNode(NodeDefinition definition)
    {
        Console.WriteLine($"[NodeFactory] Creating Container node for {definition.NodeId}");
        // ContainerNode configuration (ChildNodes, ChildConnections, ExecutionMode) is provided via Configuration
        // Validation happens in Initialize/ExecuteAsync
        var node = new ContainerNode();
        Console.WriteLine($"[NodeFactory] Container node instance created for {definition.NodeId}");
        return node;
    }

    /// <summary>
    /// Creates an instance from a type using reflection.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>The created instance.</returns>
    private INode CreateInstanceFromType(Type type)
    {
        var instance = Activator.CreateInstance(type);
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of type '{type.FullName}'.");
        }

        return (INode)instance;
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
