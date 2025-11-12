// -----------------------------------------------------------------------
// <copyright file="MessageType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Represents the type of message produced by a node.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Message indicating node completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Message indicating node failed with an error.
    /// </summary>
    Fail,

    /// <summary>
    /// Message indicating node progress update.
    /// </summary>
    Progress
}
