//-----------------------------------------------------------------------
// <copyright file="EtwScopeTracker.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Diagnostics.Tracing;

    /// <summary>
    /// Class to keep track of event scopes to pair start and stop events.
    /// </summary>
    /// <typeparam name="T">The type of the object which represents a wrapped event.</typeparam>
    public class EtwScopeTracker<T> where T : class
    {
        /// <summary>
        /// Keeps a list of all open scopes by event name.
        /// </summary>
        private Dictionary<string, Stack<T>> openScopesToEventObjects = new Dictionary<string, Stack<T>>();

        /// <summary>
        /// Indicates that a specific event name has started a scope and pushes that on the stack to await an end event.
        /// </summary>
        /// <param name="startEvent">The starting event.</param>
        /// <param name="startEventObject">The wrapped start event.</param>
        public void PushScope(TraceEvent startEvent, T startEventObject)
        {
            var uniqueEventName = this.GetUniqueEventName(startEvent);

            if (!this.openScopesToEventObjects.ContainsKey(uniqueEventName))
            {
                this.openScopesToEventObjects[uniqueEventName] = new Stack<T>();
            }

            this.openScopesToEventObjects[uniqueEventName].Push(startEventObject);
        }

        /// <summary>
        /// Retrieves the wrapped start event associated with this end event.
        /// </summary>
        /// <param name="endEvent">The end event.</param>
        /// <returns>The wrapped start event associated with this end event.</returns>
        public T? PopScope(TraceEvent endEvent)
        {
            var uniqueEventName = this.GetUniqueEventName(endEvent);
            T? result;

            if (!this.openScopesToEventObjects.ContainsKey(uniqueEventName) || this.openScopesToEventObjects[uniqueEventName].Count == 0)
            {
                result = default(T);
            }
            else
            {
                result = this.openScopesToEventObjects[uniqueEventName].Pop();
            }

            return result;
        }

        /// <summary>
        /// Gets a unique name for an event.
        /// </summary>
        /// <param name="traceEvent">An event.</param>
        /// <returns>The unique name for this event.</returns>
        private string GetUniqueEventName(TraceEvent traceEvent)
        {
            return traceEvent.ProviderName + "_" + traceEvent.TaskName + "_" + traceEvent.ProcessID + "_" + traceEvent.ActivityID;
        }
    }
}
