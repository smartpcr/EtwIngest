//-----------------------------------------------------------------------
// <copyright file="EventProcessor.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using EtwEventReader.Models;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Processes ETL log files and extracts events.
    /// </summary>
    public class EventProcessor
    {
        /// <summary>
        /// List of Temporary Paths
        /// </summary>
        private List<string> tempPaths = new List<string>();

        /// <summary>
        /// Gets a list of EventSource events.
        /// </summary>
        /// <param name="paths">The paths to ETL files or directories containing ETL files.</param>
        /// <param name="activityId">Optional Activity Id to filter on.</param>
        /// <param name="providerName">Optional provider name to filter on.</param>
        /// <param name="eventName">Optional event name to filter on.</param>
        /// <returns>List of EtwEventObject instances.</returns>
        public List<EtwEventObject> GetEvents(
            string[] paths,
            Guid activityId = default,
            string? providerName = null,
            string? eventName = null)
        {
            try
            {
                var resolvedPaths = this.ResolveAllPaths(paths);
                var allEvents = new List<EtwEventObject>();
                var wrapper = new EtwEventWrapper();

                Console.WriteLine("Inspecting log files...");

                foreach (string path in resolvedPaths)
                {
                    Console.WriteLine($"Processing: {path}");
                    this.ProcessLogFile(path, allEvents, wrapper, activityId, providerName, eventName);
                }

                Console.WriteLine("Processing events...");
                return allEvents;
            }
            finally
            {
                this.RemoveTempPaths();
            }
        }

        /// <summary>
        /// Resolves all paths from the Path parameter.
        /// </summary>
        /// <param name="paths">Array of file or directory paths.</param>
        /// <returns>List of resolved ETL file paths.</returns>
        private List<string> ResolveAllPaths(string[] paths)
        {
            var resolvedPaths = new List<string>();
            var zeroLengthFiles = new List<string>();

            for (int i = 0; i < paths.Length; i++)
            {
                string tempZipExtractionDirName = string.Empty;
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                try
                {
                    // Handle wildcards and zip files
                    if (paths[i].Contains("*") || paths[i].EndsWith(".zip"))
                    {
                        var directory = Path.GetDirectoryName(paths[i]) ?? Directory.GetCurrentDirectory();
                        var searchPattern = Path.GetFileName(paths[i]);
                        tempZipExtractionDirName = Path.Combine(directory, "TempPath", timestamp);

                        var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            resolvedPaths.Add(file);
                        }
                    }
                    else if (Directory.Exists(paths[i]))
                    {
                        tempZipExtractionDirName = Path.Combine(paths[i], "TempPath", timestamp);
                        resolvedPaths.AddRange(Directory.GetFiles(paths[i]));
                    }
                    else if (File.Exists(paths[i]))
                    {
                        resolvedPaths.Add(paths[i]);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Path not found: {paths[i]}");
                        continue;
                    }

                    // Process zip files
                    var zipFiles = resolvedPaths.Where(f => f.EndsWith(".zip")).ToList();
                    foreach (var zipFile in zipFiles)
                    {
                        FileInfo fileInfo = new FileInfo(zipFile);

                        if (fileInfo.Length > 0)
                        {
                            if (!Directory.Exists(tempZipExtractionDirName))
                            {
                                Directory.CreateDirectory(tempZipExtractionDirName);
                                this.tempPaths.Add(tempZipExtractionDirName);
                            }

                            this.ExtractFileToDirectory(zipFile, tempZipExtractionDirName);
                        }
                        else
                        {
                            zeroLengthFiles.Add(zipFile);
                            Console.WriteLine($"Warning: Ignoring zero length file: {zipFile}");
                        }
                    }

                    if (!string.IsNullOrEmpty(tempZipExtractionDirName) && Directory.Exists(tempZipExtractionDirName))
                    {
                        var addFiles = Directory.GetFiles(tempZipExtractionDirName, "*.etl", SearchOption.AllDirectories);
                        resolvedPaths.AddRange(addFiles);
                        resolvedPaths.RemoveAll(x => x.EndsWith(".zip"));
                    }

                    resolvedPaths.RemoveAll(x => zeroLengthFiles.Contains(x));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing path {paths[i]}: {ex.Message}");
                }
            }

            return resolvedPaths.Distinct().ToList();
        }

        /// <summary>
        /// Processes a log file wrapping every event based on the filter specified.
        /// </summary>
        /// <param name="file">The file to process.</param>
        /// <param name="eventList">A list onto which processed events will be added.</param>
        /// <param name="wrapper">A wrapper to convert the trace events into AS Events.</param>
        /// <param name="activityId">Activity ID filter.</param>
        /// <param name="providerName">Provider name filter.</param>
        /// <param name="eventName">Event name filter.</param>
        private void ProcessLogFile(
            string file,
            List<EtwEventObject> eventList,
            EtwEventWrapper wrapper,
            Guid activityId,
            string? providerName,
            string? eventName)
        {
            Action<TraceEvent> action = data =>
            {
                if (data.EventName != EventNames.ManifestEventName)
                {
                    // Filter out events per the activityID request filter.
                    if (activityId != Guid.Empty && data.ActivityID != activityId)
                    {
                        // Activities under a scope which start a new scope have an activityId which represents the new scope and the old scope in related activity id.
                        if (data.Opcode != TraceEventOpcode.Start || data.RelatedActivityID != activityId)
                        {
                            return;
                        }
                    }

                    // Wrap the ETW event.
                    EtwEventObject wrappedEvent = wrapper.CreateFromTraceEvent(data);

                    eventList.Add(wrappedEvent);
                }
            };

            using (ETWTraceEventSource traceEventSource = new ETWTraceEventSource(file))
            {
                bool filterOnProviderName = !string.IsNullOrEmpty(providerName);
                bool filterOnEventName = !string.IsNullOrEmpty(eventName);

                if (filterOnEventName && filterOnProviderName)
                {
                    traceEventSource.Dynamic.AddCallbackForProviderEvents(
                        (eventProviderName, eventEventName) =>
                        {
                            if (string.Compare(eventProviderName, providerName, StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                return EventFilterResponse.RejectProvider;
                            }

                            if (string.Compare(eventEventName, eventName, StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                return EventFilterResponse.RejectEvent;
                            }

                            return EventFilterResponse.AcceptEvent;
                        },
                    action);
                }
                else if (filterOnProviderName)
                {
                    traceEventSource.Dynamic.AddCallbackForProviderEvents(
                        (eventProviderName, eventEventName) =>
                        {
                            if (string.Compare(eventProviderName, providerName, StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                return EventFilterResponse.RejectProvider;
                            }

                            return EventFilterResponse.AcceptEvent;
                        },
                    action);
                }
                else if (filterOnEventName)
                {
                    traceEventSource.Dynamic.AddCallbackForProviderEvents(
                        (eventProviderName, eventEventName) =>
                        {
                            if (string.Compare(eventEventName, eventName, StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                return EventFilterResponse.RejectEvent;
                            }

                            return EventFilterResponse.AcceptEvent;
                        },
                    action);
                }
                else
                {
                    traceEventSource.Dynamic.All += action;
                }

                // Register a handler for all unhandled events.
                traceEventSource.UnhandledEvents += traceEvent => this.ProcessUnhandledEvent(traceEvent, eventList, wrapper, providerName, eventName);

                Console.WriteLine("Reading events...");
                traceEventSource.Process();
            }
        }

        /// <summary>
        /// Process an unhandled event.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <param name="eventList">The collection to append a processed event to.</param>
        /// <param name="wrapper">Instance of EtwEventWrapper.</param>
        /// <param name="providerName">Provider name filter.</param>
        /// <param name="eventName">Event name filter.</param>
        private void ProcessUnhandledEvent(
            TraceEvent traceEvent,
            List<EtwEventObject> eventList,
            EtwEventWrapper wrapper,
            string? providerName,
            string? eventName)
        {
            var providerNameMatches = string.IsNullOrEmpty(providerName) ||
                                    (providerName?.Equals(traceEvent.ProviderName, StringComparison.OrdinalIgnoreCase) ?? false);
            var eventNameMatches = string.IsNullOrEmpty(eventName) ||
                                 (eventName?.Equals(traceEvent.EventName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (providerNameMatches && eventNameMatches)
            {
                EtwEventObject wrappedEvent = wrapper.CreateFromTraceEvent(traceEvent);
                eventList.Add(wrappedEvent);
            }
        }

        /// <summary>
        /// Remove the Temp Directories
        /// </summary>
        private void RemoveTempPaths()
        {
            try
            {
                if (this.tempPaths != null && this.tempPaths.Count != 0)
                {
                    this.tempPaths.ForEach(tempDir =>
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to delete temp directory {tempDir}: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing temp directories: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract Zip file to a directory
        /// </summary>
        /// <param name="zipName">Zip File Name</param>
        /// <param name="extractDirectory">Directory for extracting zip file</param>
        private void ExtractFileToDirectory(string zipName, string extractDirectory)
        {
            using (var zip = ZipFile.OpenRead(zipName))
            {
                zip.ExtractToDirectory(extractDirectory);
            }
        }
    }
}
