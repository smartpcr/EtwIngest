//-------------------------------------------------------------------------------
// <copyright file="ITraceEventParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Core
{
    using Common.Diagnostics.EtwParser.Models;

    /// <summary>
    /// Interface for trace event parsers (ETL, EVTX, etc.)
    /// </summary>
    public interface ITraceEventParser
    {
        /// <summary>
        /// Parse the trace file to discover event schemas
        /// </summary>
        /// <param name="eventSchemas">Dictionary to populate with discovered event schemas</param>
        /// <returns>Parse result indicating success or failure</returns>
        ParseResult DiscoverSchemas(IDictionary<EventIdentifier, TraceEventSchema> eventSchemas);

        /// <summary>
        /// Extract events from the trace file based on known schemas
        /// </summary>
        /// <param name="knownSchemas">Dictionary of known event schemas to extract</param>
        /// <returns>Dictionary of events grouped by event identifier</returns>
        IDictionary<EventIdentifier, IList<TraceEventRecord>> ExtractEvents(
            IDictionary<EventIdentifier, TraceEventSchema> knownSchemas);
    }
}
