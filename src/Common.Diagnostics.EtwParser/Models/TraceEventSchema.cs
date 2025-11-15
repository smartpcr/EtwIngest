//-------------------------------------------------------------------------------
// <copyright file="TraceEventSchema.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Models
{
    /// <summary>
    /// Schema definition for a trace event type
    /// </summary>
    public class TraceEventSchema
    {
        /// <summary>
        /// Gets or sets the event identifier
        /// </summary>
        public required EventIdentifier EventId { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of fields in this event schema
        /// </summary>
        public required List<FieldSchema> Fields { get; set; }

        /// <summary>
        /// Gets or sets sample payload values (for schema inference)
        /// </summary>
        public Dictionary<string, object?>? SamplePayload { get; set; }

        /// <summary>
        /// Gets the field by name
        /// </summary>
        public FieldSchema? GetField(string fieldName) =>
            Fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets all standard fields (TimeStamp, ProcessID, etc.)
        /// </summary>
        public IEnumerable<FieldSchema> StandardFields => Fields.Where(f => f.IsStandardField);

        /// <summary>
        /// Gets all payload fields (event-specific custom fields)
        /// </summary>
        public IEnumerable<FieldSchema> PayloadFields => Fields.Where(f => !f.IsStandardField);
    }
}
