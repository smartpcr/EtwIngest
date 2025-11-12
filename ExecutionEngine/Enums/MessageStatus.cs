// -----------------------------------------------------------------------
// <copyright file="MessageStatus.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Represents the status of a message in the circular buffer queue.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message is ready to be processed.
    /// </summary>
    Ready,

    /// <summary>
    /// Message is currently being processed (leased).
    /// </summary>
    InFlight,

    /// <summary>
    /// Message has been completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Message has been superseded by a newer message.
    /// </summary>
    Superseded
}
