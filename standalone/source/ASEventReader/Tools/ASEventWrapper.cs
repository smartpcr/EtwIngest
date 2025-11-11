//-----------------------------------------------------------------------
// <copyright file="ASEventWrapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReader.Tools
{
    using System;
    using System.Linq;
    using ASEventReader.EventFormatters;
    using ASEventReader.Models;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Tool to get ETW traces and wrap them as event objects.
    /// </summary>
    public class ASEventWrapper
    {
        /// <summary>
        /// Tracks events which have been created so far and uses this information to populate Start/Stop pairs.
        /// </summary>
        private EtwScopeTracker<ASEventObject> tracker;

        /// <summary>
        /// Event formatter map.
        /// </summary>
        private EventFormatterMap eventFormatMap = new EventFormatterMap();

        /// <summary>
        /// Initializes a new instance of the <see cref="ASEventWrapper"/> class.
        /// </summary>
        public ASEventWrapper()
        {
            this.tracker = new EtwScopeTracker<ASEventObject>();
        }

        /// <summary>
        /// Creates an ASEventObject representation of a trace event.
        /// </summary>
        /// <param name="traceEvent">The event to process</param>
        /// <returns>An ASEventObject representing the Trace Event.</returns>
        public ASEventObject CreateFromTraceEvent(TraceEvent traceEvent)
        {
            var result = new ASEventObject();
            result.DefaultDisplayNames.AddRange(traceEvent.PayloadNames);

            foreach (var payloadName in traceEvent.PayloadNames)
            {
                object? payload = null;

                try
                {
                    payload = traceEvent.PayloadByName(payloadName);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                if (payload != null)
                {
                    result.AddProperty(payloadName, payload);
                }
            }

            var eventSource = traceEvent.Source as ETWTraceEventSource;

            if (eventSource == null)
            {
                throw new ArgumentNullException("traceEvent", "Event source was not an ETW trace event source and so the log file location could not be determined from the event object.");
            }

            result.DefaultDisplayNames.Add(PropertyNames.EventType);
            result.DefaultDisplayNames.Add(PropertyNames.TimeStamp);
            result.DefaultDisplayNames.Add(PropertyNames.FormattedMessage);

            result.AddProperty(PropertyNames.ActivityId, traceEvent.ActivityID);
            result.AddProperty(PropertyNames.RelatedActivityId, traceEvent.RelatedActivityID);
            result.AddProperty(PropertyNames.EventType, traceEvent.EventName);
            result.AddProperty(PropertyNames.ProviderName, traceEvent.ProviderName);
            result.AddProperty(PropertyNames.Path, eventSource.LogFileName);
            result.AddProperty(PropertyNames.TimeStamp, traceEvent.TimeStamp);
            result.AddProperty(PropertyNames.ProcessID, traceEvent.ProcessID);
            result.AddProperty(PropertyNames.ThreadID, traceEvent.ThreadID);

            if (traceEvent.FormattedMessage != null)
            {
                result.AddProperty(PropertyNames.FormattedMessage, traceEvent.FormattedMessage);
            }

            if (traceEvent.Opcode == TraceEventOpcode.Start)
            {
                this.tracker.PushScope(traceEvent, result);
            }
            else if (traceEvent.Opcode == TraceEventOpcode.Stop)
            {
                ASEventObject? wrappedStartEvent = this.tracker.PopScope(traceEvent);

                if (wrappedStartEvent != null)
                {
                    this.UpdateStartAndEndPair(wrappedStartEvent, result);
                }
            }

            // If there is an EventFormatter implementation for the event provider, apply those customizations to the result object.
            this.eventFormatMap[traceEvent.ProviderGuid]?.FormatEvent(traceEvent, result);

            return result;
        }

        /// <summary>
        /// When a pair of start/end scope objects are found, update each wrapper such that they have the necessary information for easy diagnosis.
        /// Adds the properties from the end event to the start event so they are immediately available.
        /// Adds duration to both properties if its not already there.
        /// </summary>
        /// <param name="startEvent">The ASEventObject wrapping the start event.</param>
        /// <param name="endEvent">The ASEventObject wrapping the end event.</param>
        private void UpdateStartAndEndPair(ASEventObject startEvent, ASEventObject endEvent)
        {
            // The end event often has the meat of the properties such as failure information. Copy this to the start event for ease of parsing when the
            // user looks at the events.
            foreach (var property in endEvent.Properties)
            {
                // AS provided properties need not be transfered from the end event to the start event.
                if (PropertyNames.AsProvidedProperties.Contains(property.Key))
                {
                    continue;
                }

                if (!startEvent.Properties.ContainsKey(property.Key))
                {
                    startEvent.AddProperty(property.Key, property.Value);
                    if (!startEvent.DefaultDisplayNames.Contains(property.Key))
                    {
                        startEvent.DefaultDisplayNames.Add(property.Key);
                    }
                }
            }

            if (!endEvent.DurationMs.HasValue)
            {
                long duration = Convert.ToInt64((endEvent.TimeStamp - startEvent.TimeStamp).TotalMilliseconds);

                endEvent.AddProperty(PropertyNames.DurationMs, duration);
                if (!endEvent.DefaultDisplayNames.Contains(PropertyNames.DurationMs))
                {
                    endEvent.DefaultDisplayNames.Add(PropertyNames.DurationMs);
                }

                if (!startEvent.DurationMs.HasValue)
                {
                    startEvent.AddProperty(PropertyNames.DurationMs, duration);
                    if (!startEvent.DefaultDisplayNames.Contains(PropertyNames.DurationMs))
                    {
                        startEvent.DefaultDisplayNames.Add(PropertyNames.DurationMs);
                    }
                }
            }
        }
    }
}
