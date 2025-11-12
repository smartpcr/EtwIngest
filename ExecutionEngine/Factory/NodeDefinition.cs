// -----------------------------------------------------------------------
// <copyright file="NodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Factory;

/// <summary>
/// Defines metadata for dynamically loading and instantiating nodes.
/// Supports both C# compiled assemblies and PowerShell scripts.
/// </summary>
public class NodeDefinition
{
    /// <summary>
    /// Gets or sets the node ID.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node display name.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node type (Task, IfElse, ForEach, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime type (CSharp, PowerShell, Subflow).
    /// </summary>
    public string RuntimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly path for compiled C# nodes.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified type name for C# nodes.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the script path for PowerShell nodes.
    /// </summary>
    public string? ScriptPath { get; set; }

    /// <summary>
    /// Gets or sets the required PowerShell modules.
    /// </summary>
    public List<string>? RequiredModules { get; set; }

    /// <summary>
    /// Gets or sets custom module paths for PowerShell modules.
    /// Key: module name, Value: module path
    /// </summary>
    public Dictionary<string, string>? ModulePaths { get; set; }

    /// <summary>
    /// Gets or sets additional configuration for the node.
    /// </summary>
    public Dictionary<string, object>? Configuration { get; set; }
}
