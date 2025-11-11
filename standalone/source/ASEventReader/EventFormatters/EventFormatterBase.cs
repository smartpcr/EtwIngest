//-----------------------------------------------------------------------
// <copyright file="EventFormatterBase.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReader.EventFormatters
{
    using System;
    using ASEventReader.Models;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Abstract base class for applying event provider specific customizations to an ASEventObject instance.
    /// </summary>
    internal abstract class EventFormatterBase
    {
        /// <summary>
        /// Gets the guid of the event provider that this event formatter implementation is applicable to.
        /// </summary>
        public abstract Guid ProviderGuid { get; }

        /// <summary>
        /// Apply event provider specific formatting to the supplied ASEventObject instance.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <param name="asEventObject">The AsEventObject instance.</param>
        public void FormatEvent(TraceEvent traceEvent, ASEventObject asEventObject)
        {
            if (traceEvent.ProviderGuid == this.ProviderGuid)
            {
                try
                {
                    asEventObject.AddProperty(PropertyNames.EventFormatterName, this.GetType());

                    this.Format(traceEvent, asEventObject);
                }
                catch
                {
                    // Swallow all exceptions raised while formatting the event object.
                }
            }
        }

        /// <summary>
        /// Apply event provider specific formatting to the supplied ASEventObject instance.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <param name="asEventObject">The AsEventObject instance.</param>
        protected abstract void Format(TraceEvent traceEvent, ASEventObject asEventObject);
    }
}
