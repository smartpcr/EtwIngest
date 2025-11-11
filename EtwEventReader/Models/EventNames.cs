//-----------------------------------------------------------------------
// <copyright file="EventNames.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Models
{
    using System;

    /// <summary>
    /// Records the names of well known events.
    /// </summary>
    public class EventNames
    {
        /// <summary>
        /// The EventSource base class event for injecting Manifest data.
        /// </summary>
        public const string ManifestEventName = "ManifestData";
    }
}
