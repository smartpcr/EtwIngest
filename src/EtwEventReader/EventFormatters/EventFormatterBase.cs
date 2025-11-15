//-----------------------------------------------------------------------
// <copyright file="EventFormatterBase.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.EventFormatters
{
    using System;
    using EtwEventReader.Models;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Abstract base class for applying event provider specific customizations to an EtwEventObject instance.
    /// </summary>
    internal abstract class EventFormatterBase
    {
        /// <summary>
        /// Gets the guid of the event provider that this event formatter implementation is applicable to.
        /// </summary>
        public abstract Guid ProviderGuid { get; }

        /// <summary>
        /// Apply event provider specific formatting to the supplied EtwEventObject instance.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <param name="asEventObject">The AsEventObject instance.</param>
        public void FormatEvent(TraceEvent traceEvent, EtwEventObject asEventObject)
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
        /// Apply event provider specific formatting to the supplied EtwEventObject instance.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        /// <param name="asEventObject">The AsEventObject instance.</param>
        protected abstract void Format(TraceEvent traceEvent, EtwEventObject asEventObject);
    }
}
