//-------------------------------------------------------------------------------
// <copyright file="EtlFileParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Parsers
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using Common.Diagnostics.EtwParser.Core;
    using Common.Diagnostics.EtwParser.Models;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Parsers;

    /// <summary>
    /// Parser for ETW .etl trace files
    /// </summary>
    public class EtlFileParser : ITraceEventParser
    {
        private readonly string filePath;
        private readonly long fileSize;
        private readonly int inactivityTimeoutSeconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtlFileParser"/> class.
        /// </summary>
        /// <param name="filePath">Path to the .etl file</param>
        /// <param name="inactivityTimeoutSeconds">Timeout in seconds to stop processing if no events received (default: 10)</param>
        public EtlFileParser(string filePath, int inactivityTimeoutSeconds = 10)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"ETL file not found: {filePath}");
            }

            this.filePath = filePath;
            this.fileSize = new FileInfo(filePath).Length;
            this.inactivityTimeoutSeconds = inactivityTimeoutSeconds;
        }

        /// <inheritdoc/>
        public ParseResult DiscoverSchemas(IDictionary<EventIdentifier, TraceEventSchema> eventSchemas)
        {
            var stopwatch = Stopwatch.StartNew();
            var concurrentSchemas = new ConcurrentDictionary<EventIdentifier, TraceEventSchema>();

            try
            {
                using var source = new ETWTraceEventSource(filePath);
                var parser = new DynamicTraceEventParser(source);

                var lastEventTime = DateTime.UtcNow;
                var timer = new System.Timers.Timer(inactivityTimeoutSeconds * 1000);
                timer.Elapsed += (sender, e) =>
                {
                    if ((DateTime.UtcNow - lastEventTime).TotalSeconds >= inactivityTimeoutSeconds)
                    {
                        Console.WriteLine($"No events received in the last {inactivityTimeoutSeconds} seconds. Stopping processing {filePath} (file size: {fileSize} bytes) after {Math.Round(stopwatch.Elapsed.TotalSeconds)} seconds.");
                        source.StopProcessing();
                        timer.Stop();
                    }
                };
                timer.Start();

                parser.All += traceEvent =>
                {
                    try
                    {
                        lastEventTime = DateTime.UtcNow;
                        var eventId = new EventIdentifier(traceEvent.ProviderName, traceEvent.EventName);

                        if (!concurrentSchemas.ContainsKey(eventId))
                        {
                            var schema = CreateSchemaFromEvent(eventId, traceEvent);
                            concurrentSchemas.TryAdd(eventId, schema);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing event: {ex.Message}");
                    }
                };

                source.Process();

                timer.Stop();
                timer.Dispose();

                // Copy to provided dictionary
                foreach (var kvp in concurrentSchemas)
                {
                    eventSchemas[kvp.Key] = kvp.Value;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                stopwatch.Stop();
                return ParseResult.Successful(concurrentSchemas.Count, fileSize, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ParseResult.Failed($"Error parsing ETL file: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public IDictionary<EventIdentifier, IList<TraceEventRecord>> ExtractEvents(
            IDictionary<EventIdentifier, TraceEventSchema> knownSchemas)
        {
            var result = new ConcurrentDictionary<EventIdentifier, IList<TraceEventRecord>>();

            try
            {
                using var source = new ETWTraceEventSource(filePath);
                var parser = new DynamicTraceEventParser(source);

                var stopwatch = Stopwatch.StartNew();
                var lastEventTime = DateTime.UtcNow;
                var timer = new System.Timers.Timer(inactivityTimeoutSeconds * 1000);
                timer.Elapsed += (sender, e) =>
                {
                    if ((DateTime.UtcNow - lastEventTime).TotalSeconds >= inactivityTimeoutSeconds)
                    {
                        Console.WriteLine($"No events received in the last {inactivityTimeoutSeconds} seconds. Stopping processing {filePath} (file size: {fileSize} bytes) after {Math.Round(stopwatch.Elapsed.TotalSeconds)} seconds.");
                        source.StopProcessing();
                        timer.Stop();
                    }
                };
                timer.Start();

                parser.All += traceEvent =>
                {
                    lastEventTime = DateTime.UtcNow;
                    var eventId = new EventIdentifier(traceEvent.ProviderName, traceEvent.EventName);

                    if (knownSchemas.TryGetValue(eventId, out var schema))
                    {
                        var record = CreateRecordFromEvent(eventId, traceEvent, schema);

                        var list = result.GetOrAdd(eventId, _ => new List<TraceEventRecord>());
                        lock (list)
                        {
                            list.Add(record);
                        }
                    }
                };

                source.Process();

                timer.Stop();
                timer.Dispose();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting events from ETL file: {ex.Message}");
            }

            return result;
        }

        private static TraceEventSchema CreateSchemaFromEvent(EventIdentifier eventId, TraceEvent traceEvent)
        {
            var fields = new List<FieldSchema>();
            var samplePayload = new Dictionary<string, object?>();

            int ordinal = 0;

            // Standard fields
            AddStandardField(fields, ref ordinal, nameof(traceEvent.TimeStamp), typeof(DateTime));
            AddStandardField(fields, ref ordinal, nameof(traceEvent.ProcessID), typeof(int));
            AddStandardField(fields, ref ordinal, nameof(traceEvent.ProcessName), typeof(string));
            AddStandardField(fields, ref ordinal, nameof(traceEvent.Level), typeof(int));
            AddStandardField(fields, ref ordinal, nameof(traceEvent.Opcode), typeof(int));
            AddStandardField(fields, ref ordinal, nameof(traceEvent.OpcodeName), typeof(string));

            // Sample values for standard fields
            samplePayload[nameof(traceEvent.TimeStamp)] = traceEvent.TimeStamp;
            samplePayload[nameof(traceEvent.ProcessID)] = traceEvent.ProcessID;
            samplePayload[nameof(traceEvent.ProcessName)] = traceEvent.ProcessName;
            samplePayload[nameof(traceEvent.Level)] = (int)traceEvent.Level;
            samplePayload[nameof(traceEvent.Opcode)] = (int)traceEvent.Opcode;
            samplePayload[nameof(traceEvent.OpcodeName)] = traceEvent.OpcodeName;

            // Payload fields
            foreach (var payloadName in traceEvent.PayloadNames)
            {
                var value = traceEvent.PayloadByName(payloadName);
                var fieldType = value?.GetType() ?? typeof(string);

                fields.Add(new FieldSchema
                {
                    Name = payloadName,
                    FieldType = fieldType,
                    IsNullable = true,
                    Ordinal = ordinal++,
                    IsStandardField = false
                });

                samplePayload[payloadName] = value;
            }

            return new TraceEventSchema
            {
                EventId = eventId,
                Fields = fields,
                SamplePayload = samplePayload
            };
        }

        private static void AddStandardField(List<FieldSchema> fields, ref int ordinal, string name, Type type)
        {
            fields.Add(new FieldSchema
            {
                Name = name,
                FieldType = type,
                IsNullable = false,
                Ordinal = ordinal++,
                IsStandardField = true
            });
        }

        private static TraceEventRecord CreateRecordFromEvent(
            EventIdentifier eventId,
            TraceEvent traceEvent,
            TraceEventSchema schema)
        {
            var fieldValues = new Dictionary<string, object?>();

            foreach (var field in schema.Fields)
            {
                object? value = field.Name switch
                {
                    nameof(TraceEvent.TimeStamp) => traceEvent.TimeStamp,
                    nameof(TraceEvent.ProcessID) => traceEvent.ProcessID,
                    nameof(TraceEvent.ProcessName) => traceEvent.ProcessName,
                    nameof(TraceEvent.Level) => (int)traceEvent.Level,
                    nameof(TraceEvent.Opcode) => (int)traceEvent.Opcode,
                    nameof(TraceEvent.OpcodeName) => traceEvent.OpcodeName,
                    _ => traceEvent.PayloadByName(field.Name)
                };

                fieldValues[field.Name] = value;
            }

            return new TraceEventRecord
            {
                EventId = eventId,
                FieldValues = fieldValues,
                TimeStamp = traceEvent.TimeStamp,
                ProcessID = traceEvent.ProcessID,
                ProcessName = traceEvent.ProcessName
            };
        }
    }
}
