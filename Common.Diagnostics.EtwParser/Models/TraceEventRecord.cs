//-------------------------------------------------------------------------------
// <copyright file="TraceEventRecord.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Models
{
    /// <summary>
    /// Represents a single trace event record with its field values
    /// </summary>
    public class TraceEventRecord
    {
        /// <summary>
        /// Gets or sets the event identifier
        /// </summary>
        public required EventIdentifier EventId { get; set; }

        /// <summary>
        /// Gets or sets the field values keyed by field name
        /// </summary>
        public required Dictionary<string, object?> FieldValues { get; set; }

        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessID { get; set; }

        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string? ProcessName { get; set; }

        /// <summary>
        /// Gets a field value by name
        /// </summary>
        public object? GetValue(string fieldName)
        {
            return FieldValues.TryGetValue(fieldName, out var value) ? value : null;
        }

        /// <summary>
        /// Gets a typed field value by name
        /// </summary>
        public T? GetValue<T>(string fieldName)
        {
            var value = GetValue(fieldName);
            if (value == null) return default;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
    }
}
