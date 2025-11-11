//-------------------------------------------------------------------------------
// <copyright file="CsvExporter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Extensions
{
    using System.Text;
    using Common.Diagnostics.EtwParser.Core;
    using Common.Diagnostics.EtwParser.Models;

    /// <summary>
    /// Exports trace events to CSV files
    /// </summary>
    public class CsvExporter : IEventExporter
    {
        private readonly string outputDirectory;
        private readonly string filePrefix;
        private readonly bool includeHeaders;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvExporter"/> class.
        /// </summary>
        /// <param name="outputDirectory">Directory to write CSV files</param>
        /// <param name="filePrefix">Optional prefix for CSV file names</param>
        /// <param name="includeHeaders">Whether to include header row (default: true)</param>
        public CsvExporter(string outputDirectory, string? filePrefix = null, bool includeHeaders = true)
        {
            this.outputDirectory = outputDirectory;
            this.filePrefix = filePrefix ?? string.Empty;
            this.includeHeaders = includeHeaders;

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        /// <inheritdoc/>
        public async Task<ExportResult> ExportAsync(
            IDictionary<EventIdentifier, IList<TraceEventRecord>> events,
            IDictionary<EventIdentifier, TraceEventSchema> schemas)
        {
            int totalRecords = 0;

            try
            {
                foreach (var kvp in events)
                {
                    var eventId = kvp.Key;
                    var records = kvp.Value;

                    if (!schemas.TryGetValue(eventId, out var schema))
                    {
                        continue;
                    }

                    var fileName = GetCsvFileName(eventId);
                    var filePath = Path.Combine(outputDirectory, fileName);

                    await WriteCsvFileAsync(filePath, records, schema);
                    totalRecords += records.Count;
                }

                return ExportResult.Successful(totalRecords, outputDirectory);
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"CSV export failed: {ex.Message}");
            }
        }

        private string GetCsvFileName(EventIdentifier eventId)
        {
            var safeName = eventId.ToSafeIdentifier(filePrefix);
            return $"{safeName}.csv";
        }

        private async Task WriteCsvFileAsync(
            string filePath,
            IList<TraceEventRecord> records,
            TraceEventSchema schema)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Write header
            if (includeHeaders)
            {
                var headerLine = string.Join(",", schema.Fields.Select(f => f.Name));
                await writer.WriteLineAsync(headerLine);
            }

            // Write records
            foreach (var record in records)
            {
                var line = BuildCsvLine(record, schema);
                await writer.WriteLineAsync(line);
            }
        }

        private static string BuildCsvLine(TraceEventRecord record, TraceEventSchema schema)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                var field = schema.Fields[i];
                var value = record.GetValue(field.Name);

                if (value != null)
                {
                    var stringValue = value.ToString() ?? string.Empty;

                    // Escape CSV special characters
                    if (field.FieldType == typeof(string) && NeedsEscaping(stringValue))
                    {
                        stringValue = EscapeCsvValue(stringValue);
                    }

                    sb.Append(stringValue);
                }

                if (i < schema.Fields.Count - 1)
                {
                    sb.Append(',');
                }
            }

            return sb.ToString();
        }

        private static bool NeedsEscaping(string value)
        {
            return value.Contains('"')
                || value.Contains(',')
                || value.Contains('\n')
                || value.Contains('\r');
        }

        private static string EscapeCsvValue(string value)
        {
            // Escape quotes by doubling them
            var escaped = value.Replace("\"", "\"\"");

            // Wrap in quotes
            return $"\"{escaped}\"";
        }
    }
}
