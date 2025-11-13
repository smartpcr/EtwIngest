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

        if (string.IsNullOrWhiteSpace(definition.RuntimeType))
        {
            throw new ArgumentException("RuntimeType cannot be null or whitespace.", nameof(definition));
        }

        INode node = definition.RuntimeType.ToLowerInvariant() switch
        {
            "csharp" => this.CreateCSharpNode(definition),
            "csharpscript" => this.CreateCSharpScriptNode(definition),
            "csharptask" => this.CreateCSharpTaskNode(definition),
            "powershell" => this.CreatePowerShellScriptNode(definition),
            "powershelltask" => this.CreatePowerShellTaskNode(definition),
            _ => throw new NotSupportedException($"Runtime type '{definition.RuntimeType}' is not supported.")
        };

        node.Initialize(definition);
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
