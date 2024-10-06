//-------------------------------------------------------------------------------
// <copyright file="EtlFile.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Steps
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Parsers;

    public class EtlFile
    {
        private readonly string etlFile;

        public EtlFile(string etlFile)
        {
            this.etlFile = etlFile;
        }

        public void Parse(ConcurrentDictionary<(string providerName, string eventName), EtwEvent> eventSchema, ref bool failed)
        {
            try
            {
                using var source = new ETWTraceEventSource(this.etlFile);
                var parser = new DynamicTraceEventParser(source);
                parser.All += traceEvent =>
                {
                    var providerName = traceEvent.ProviderName;
                    var eventName = traceEvent.EventName;
                    if (!eventSchema.ContainsKey((providerName, eventName)))
                    {
                        var etwEvent = new EtwEvent
                        {
                            ProviderName = providerName,
                            EventName = eventName,
                            PayloadSchema = new List<(string fieldName, Type fieldType)>(),
                            Payload = new Dictionary<string, object>(),
                        };

                        etwEvent.PayloadSchema.Add((nameof(traceEvent.TimeStamp), typeof(DateTime)));
                        etwEvent.PayloadSchema.Add((nameof(traceEvent.ProcessID), typeof(int)));
                        etwEvent.PayloadSchema.Add((nameof(traceEvent.ProcessName), typeof(string)));
                        etwEvent.PayloadSchema.Add((nameof(traceEvent.Level), typeof(int)));
                        etwEvent.PayloadSchema.Add((nameof(traceEvent.Opcode), typeof(string)));
                        etwEvent.PayloadSchema.Add((nameof(traceEvent.OpcodeName), typeof(string)));

                        etwEvent.Payload.Add(nameof(traceEvent.TimeStamp), traceEvent.TimeStamp);
                        etwEvent.Payload.Add(nameof(traceEvent.ProcessID), traceEvent.ProcessID);
                        etwEvent.Payload.Add(nameof(traceEvent.ProcessName), traceEvent.ProcessName);
                        etwEvent.Payload.Add(nameof(traceEvent.Level), traceEvent.Level);
                        etwEvent.Payload.Add(nameof(traceEvent.Opcode), traceEvent.Opcode);
                        etwEvent.Payload.Add(nameof(traceEvent.OpcodeName), traceEvent.OpcodeName);

                        foreach (var item in traceEvent.PayloadNames)
                        {
                            if (etwEvent.Payload.TryAdd(item, traceEvent.PayloadByName(item)))
                            {
                                etwEvent.PayloadSchema.Add((item, traceEvent.PayloadByName(item)?.GetType() ?? typeof(string)));
                            }
                        }
                        eventSchema.TryAdd((providerName, eventName), etwEvent);
                    }

                };

                source.Process();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing ETL file: {ex.Message}");
                failed = true;
            }
        }

        public void Process(
            Dictionary<(string providerName, string eventName), EtwEvent> eventSchemas,
            Dictionary<(string providerName, string eventName), StreamWriter> eventWriters)
        {
            using var source = new ETWTraceEventSource(this.etlFile);
            var parser = new DynamicTraceEventParser(source);
            parser.All += traceEvent =>
            {
                var providerName = traceEvent.ProviderName;
                var eventName = traceEvent.EventName;
                if (eventWriters.TryGetValue((providerName, eventName), out var writer) &&
                    eventSchemas.TryGetValue((providerName, eventName), out var eventSchema))
                {
                    var rowBuilder = new StringBuilder();
                    for (var i = 0; i < eventSchema.PayloadSchema.Count; i++)
                    {
                        var (fieldName, fieldType) = eventSchema.PayloadSchema[i];
                        switch (fieldName)
                        {
                            case nameof(traceEvent.TimeStamp):
                                rowBuilder.Append(traceEvent.TimeStamp);
                                break;
                            case nameof(traceEvent.ProcessID):
                                rowBuilder.Append(traceEvent.ProcessID);
                                break;
                            case nameof(traceEvent.ProcessName):
                                rowBuilder.Append(traceEvent.ProcessName);
                                break;
                            case nameof(traceEvent.Level):
                                rowBuilder.Append(traceEvent.Level);
                                break;
                            case nameof(traceEvent.Opcode):
                                rowBuilder.Append(traceEvent.Opcode);
                                break;
                            case nameof(traceEvent.OpcodeName):
                                rowBuilder.Append(traceEvent.OpcodeName);
                                break;
                            default:
                                if (fieldType == typeof(string) && traceEvent.PayloadByName(fieldName) is string fieldValue)
                                {
                                    bool containsSpecialCharacters =
                                        fieldValue.Contains("\"") ||
                                        fieldValue.Contains(",") ||
                                        fieldValue.Contains("\n") ||
                                        fieldValue.Contains("\r");
                                    if (containsSpecialCharacters)
                                    {
                                        // Escape quotes by doubling them
                                        string escapedField = fieldValue.Replace("\"", "\"\"");

                                        // Wrap the field in quotes
                                        rowBuilder.Append($"\"{escapedField}\"");
                                    }
                                }
                                else
                                {
                                    rowBuilder.Append(traceEvent.PayloadByName(fieldName));
                                }

                                break;
                        }

                        if (i < eventSchema.PayloadSchema.Count - 1)
                        {
                            rowBuilder.Append(",");
                        }
                    }
                    writer.WriteLine(rowBuilder.ToString());
                }
            };

            source.Process();
        }

    }
}
