//-------------------------------------------------------------------------------
// <copyright file="IEventExporter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Core
{
    using Common.Diagnostics.EtwParser.Models;

    /// <summary>
    /// Interface for exporting trace events to various formats
    /// </summary>
    public interface IEventExporter
    {
        /// <summary>
        /// Export events to the target format/destination
        /// </summary>
        /// <param name="events">Events grouped by identifier</param>
        /// <param name="schemas">Schemas for the events</param>
        /// <returns>Export result</returns>
        Task<ExportResult> ExportAsync(
            IDictionary<EventIdentifier, IList<TraceEventRecord>> events,
            IDictionary<EventIdentifier, TraceEventSchema> schemas);
    }

    /// <summary>
    /// Result of an export operation
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the export succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets error message if export failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the number of records exported
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Gets or sets the output location (file path, table name, etc.)
        /// </summary>
        public string? OutputLocation { get; set; }

        /// <summary>
        /// Creates a successful export result
        /// </summary>
        public static ExportResult Successful(int recordCount, string outputLocation) => new()
        {
            Success = true,
            RecordCount = recordCount,
            OutputLocation = outputLocation
        };

        /// <summary>
        /// Creates a failed export result
        /// </summary>
        public static ExportResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
