//-----------------------------------------------------------------------
// <copyright file="EventProcessor.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Generic;
    using EtwEventReader.Models;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Processes ETL log files and extracts events.
    /// </summary>
    public class EventProcessor
    {
        /// <summary>
        /// File handler for path resolution and zip extraction.
        /// </summary>
        private readonly IEventFileHandler fileHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventProcessor"/> class.
        /// </summary>
        public EventProcessor()
            : this(new EventFileHandler())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventProcessor"/> class.
        /// </summary>
        /// <param name="fileHandler">The file handler for path resolution.</param>
        public EventProcessor(IEventFileHandler fileHandler)
        {
            this.fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
        }

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
            using (this.fileHandler)
            {
                var resolvedPaths = this.fileHandler.ResolveAllPaths(paths);
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

            using ETWTraceEventSource traceEventSource = new ETWTraceEventSource(file);
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

    }
}
