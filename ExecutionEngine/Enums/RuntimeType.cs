// -----------------------------------------------------------------------
// <copyright file="RuntimeType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Defines the runtime type for node execution.
/// </summary>
public enum RuntimeType
{
    /// <summary>
    /// Compiled C# assembly node.
    /// </summary>
    CSharp,

    /// <summary>
    /// C# script node using Roslyn scripting.
    /// </summary>
    CSharpScript,

    /// <summary>
    /// C# task node (supports both inline scripts and compiled executors).
    /// </summary>
    CSharpTask,

    /// <summary>
    /// PowerShell script node.
    /// </summary>
    PowerShell,

    /// <summary>
    /// PowerShell task node (supports both inline scripts and script files).
    /// </summary>
    PowerShellTask,

    /// <summary>
    /// If-else control flow node for conditional branching.
    /// </summary>
    IfElse,

    /// <summary>
    /// ForEach control flow node for collection iteration.
    /// </summary>
    ForEach
}
