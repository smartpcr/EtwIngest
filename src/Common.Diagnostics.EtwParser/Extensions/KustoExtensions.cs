//-------------------------------------------------------------------------------
// <copyright file="KustoExtensions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Extensions
{
    using System.Text;
    using Common.Diagnostics.EtwParser.Models;
    using Common.Diagnostics.EtwParser.Schema;

    /// <summary>
    /// Extension methods for Kusto/Azure Data Explorer integration
    /// </summary>
    public static class KustoExtensions
    {
        /// <summary>
        /// Generates a Kusto CREATE TABLE command from an event schema
        /// </summary>
        /// <param name="schema">The event schema</param>
        /// <param name="tableName">Optional table name override</param>
        /// <returns>Kusto DDL command string</returns>
        public static string ToKustoCreateTableCommand(this TraceEventSchema schema, string? tableName = null)
        {
            tableName ??= schema.EventId.ToSafeIdentifier("ETL");

            var sb = new StringBuilder($".create table ['{tableName}'] ({Environment.NewLine}");

            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var field = schema.Fields[i];
                var kustoType = TypeMapper.ToKustoType(field.FieldType);

                sb.Append($"  {field.Name} : {kustoType}");

                if (i < schema.Fields.Count - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }

            sb.Append(")");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a Kusto CSV ingestion mapping command
        /// </summary>
        /// <param name="schema">The event schema</param>
        /// <param name="tableName">Optional table name override</param>
        /// <param name="mappingName">Mapping name (default: "CsvMapping")</param>
        /// <returns>Kusto ingestion mapping command string</returns>
        public static string ToKustoCsvMappingCommand(
            this TraceEventSchema schema,
            string? tableName = null,
            string mappingName = "CsvMapping")
        {
            tableName ??= schema.EventId.ToSafeIdentifier("ETL");

            var sb = new StringBuilder($".create-or-alter table ['{tableName}'] ingestion csv mapping '{mappingName}' '[");

            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var field = schema.Fields[i];
                var kustoType = TypeMapper.ToKustoType(field.FieldType);

                sb.Append($"{{\"column\":\"{field.Name}\",\"datatype\":\"{kustoType}\",\"Ordinal\":{i}}}");

                if (i < schema.Fields.Count - 1)
                {
                    sb.Append(",");
                }
            }

            sb.Append("]'");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the recommended Kusto table name for an event
        /// </summary>
        public static string ToKustoTableName(this EventIdentifier eventId, string prefix = "ETL")
        {
            return eventId.ToSafeIdentifier(prefix);
        }
    }
}
