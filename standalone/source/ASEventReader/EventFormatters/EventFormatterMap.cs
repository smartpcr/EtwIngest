//-----------------------------------------------------------------------
// <copyright file="EventFormatterMap.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReader.EventFormatters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// A dictionary mapping event provider guids to their corresponding event formatters.
    /// </summary>
    internal class EventFormatterMap
    {
        /// <summary>
        /// Lazy-initialized event provider guid to event formatter map.
        /// </summary>
        private static readonly Lazy<Dictionary<Guid, EventFormatterBase>> LazyEventFormatterMap = new Lazy<Dictionary<Guid, EventFormatterBase>>(
            () =>
            {
                var map = new Dictionary<Guid, EventFormatterBase>();
                IEnumerable<Type> eventFormatterTypes = Assembly.GetAssembly(typeof(EventFormatterBase))!.GetTypes()
                    .Where(eventFormatterType => eventFormatterType.IsClass && !eventFormatterType.IsAbstract && eventFormatterType.IsSubclassOf(typeof(EventFormatterBase)));

                foreach (Type eventFormatterType in eventFormatterTypes)
                {
                    var eventFormatterInstance = (EventFormatterBase)Activator.CreateInstance(eventFormatterType)!;
                    map[eventFormatterInstance.ProviderGuid] = eventFormatterInstance;
                }

                return map;
            });

        /// <summary>
        /// Event provider guid to event formatter map.
        /// </summary>
        private Dictionary<Guid, EventFormatterBase> Map => LazyEventFormatterMap.Value;

        /// <summary>
        /// Gets the EventFormatterBase instance corresponding to the specified event provider guid.
        /// </summary>
        /// <param name="providerGuid">Event provider guid.</param>
        /// <returns>Returns the EventFormatterBase instance corresponding to the specified event provider guid.</returns>
        public EventFormatterBase? this[Guid providerGuid] => this.Map.ContainsKey(providerGuid) ? this.Map[providerGuid] : default;
    }
}
