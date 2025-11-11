//-------------------------------------------------------------------------------
// <copyright file="FieldSchema.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Models
{
    /// <summary>
    /// Describes a field in a trace event schema
    /// </summary>
    public class FieldSchema
    {
        /// <summary>
        /// Gets or sets the field name
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the .NET CLR type of the field
        /// </summary>
        public required Type FieldType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the field is nullable
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Gets or sets the ordinal position of the field in the schema
        /// </summary>
        public int Ordinal { get; set; }

        /// <summary>
        /// Gets or sets whether this is a standard field (TimeStamp, ProcessID, etc.) or payload field
        /// </summary>
        public bool IsStandardField { get; set; }
    }
}
