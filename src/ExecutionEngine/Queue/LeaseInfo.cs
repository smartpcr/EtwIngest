// -----------------------------------------------------------------------
// <copyright file="LeaseInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

/// <summary>
/// Contains information about a leased message.
/// </summary>
public class LeaseInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the handler that leased this message.
    /// </summary>
    public string HandlerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was checked out.
    /// </summary>
    public DateTime CheckoutTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the lease expiry time.
    /// After this time, the message becomes visible again for retry.
    /// </summary>
    public DateTime LeaseExpiry { get; set; }

    /// <summary>
    /// Gets or sets the number of times the lease has been extended.
    /// </summary>
    public int ExtensionCount { get; set; }
}
