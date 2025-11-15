//-------------------------------------------------------------------------------
// <copyright file="EventIdentifier.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Models
{
    /// <summary>
    /// Uniquely identifies an event type by provider and event name
    /// </summary>
    public readonly record struct EventIdentifier(string ProviderName, string EventName)
    {
        /// <summary>
        /// Gets a safe identifier string suitable for file names or table names
        /// </summary>
        /// <param name="prefix">Optional prefix to prepend</param>
        /// <param name="separator">Separator between provider and event name (default: ".")</param>
        /// <returns>Safe identifier string</returns>
        public string ToSafeIdentifier(string? prefix = null, string separator = ".")
        {
            var safeName = $"{ProviderName}{separator}{EventName.Replace("/", "")}";
            return string.IsNullOrEmpty(prefix) ? safeName : $"{prefix}-{safeName}";
        }

        /// <summary>
        /// String representation
        /// </summary>
        public override string ToString() => $"{ProviderName}/{EventName}";
    }
}
