//-------------------------------------------------------------------------------
// <copyright file="EvtxFileParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

#pragma warning disable CA1416
namespace Common.Diagnostics.EtwParser.Parsers
{
    using System.Diagnostics;
    using Common.Diagnostics.EtwParser.Core;
    using Common.Diagnostics.EtwParser.Models;
    using evtx;

    /// <summary>
    /// Parser for Windows Event Log .evtx files
    /// </summary>
    public class EvtxFileParser : ITraceEventParser
    {
        private readonly string filePath;
        private readonly long fileSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="EvtxFileParser"/> class.
        /// </summary>
        /// <param name="filePath">Path to the .evtx file</param>
        public EvtxFileParser(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"EVTX file not found: {filePath}");
            }

            this.filePath = filePath;
            this.fileSize = new FileInfo(filePath).Length;
        }

        /// <inheritdoc/>
        public ParseResult DiscoverSchemas(IDictionary<EventIdentifier, TraceEventSchema> eventSchemas)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var eventLog = new evtx.EventLog(fs);

                // EVTX files have a consistent schema across all records
                var eventId = new EventIdentifier("WindowsEventLog", Path.GetFileNameWithoutExtension(filePath));
                var schema = CreateEvtxSchema(eventId);

                eventSchemas[eventId] = schema;

                stopwatch.Stop();
                return ParseResult.Successful(1, fileSize, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ParseResult.Failed($"Error parsing EVTX file: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public IDictionary<EventIdentifier, IList<TraceEventRecord>> ExtractEvents(
            IDictionary<EventIdentifier, TraceEventSchema> knownSchemas)
        {
            var result = new Dictionary<EventIdentifier, IList<TraceEventRecord>>();

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var eventLog = new evtx.EventLog(fs);

                var eventId = new EventIdentifier("WindowsEventLog", Path.GetFileNameWithoutExtension(filePath));
                var records = new List<TraceEventRecord>();

                foreach (var record in eventLog.GetEventRecords())
                {
                    var traceRecord = CreateRecordFromEvtxEvent(eventId, record);
                    records.Add(traceRecord);
                }

                result[eventId] = records;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting events from EVTX file: {ex.Message}");
            }

            return result;
        }

        private static TraceEventSchema CreateEvtxSchema(EventIdentifier eventId)
        {
            var fields = new List<FieldSchema>();
            var ordinal = 0;

            // Standard EVTX fields
            fields.Add(new FieldSchema
            {
                Name = "TimeStamp",
                FieldType = typeof(DateTime),
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "ProviderName",
                FieldType = typeof(string),
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "LogName",
                FieldType = typeof(string),
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "MachineName",
                FieldType = typeof(string),
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "EventId",
                FieldType = typeof(int),
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "Level",
                FieldType = typeof(string),
                IsNullable = true,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "Keywords",
                FieldType = typeof(string),
                IsNullable = true,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "ProcessId",
                FieldType = typeof(int),
                IsNullable = true,
                Ordinal = ordinal++,
                IsStandardField = true
            });

            fields.Add(new FieldSchema
            {
                Name = "Description",
                FieldType = typeof(string),
                IsNullable = true,
                Ordinal = ordinal++,
                IsStandardField = false
            });

            return new TraceEventSchema
            {
                EventId = eventId,
                Fields = fields
            };
        }

        private static TraceEventRecord CreateRecordFromEvtxEvent(EventIdentifier eventId, EventRecord record)
        {
            var fieldValues = new Dictionary<string, object?>
            {
                ["TimeStamp"] = record.TimeCreated.DateTime,
                ["ProviderName"] = record.Provider,
                ["LogName"] = record.Channel,
                ["MachineName"] = record.Computer,
                ["EventId"] = record.EventId,
                ["Level"] = record.Level,
                ["Keywords"] = record.Keywords,
                ["ProcessId"] = record.ProcessId,
                ["Description"] = record.MapDescription
            };

            return new TraceEventRecord
            {
                EventId = eventId,
                FieldValues = fieldValues,
                TimeStamp = record.TimeCreated.DateTime,
                ProcessID = record.ProcessId,
                ProcessName = null
            };
        }
    }
}
