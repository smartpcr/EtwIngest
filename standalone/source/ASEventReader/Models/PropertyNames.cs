//-----------------------------------------------------------------------
// <copyright file="PropertyNames.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReader.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Well known properties
    /// </summary>
    public class PropertyNames
    {
        /// <summary>
        /// The duration of an operation in milliseconds.
        /// </summary>
        public const string DurationMs = "durationMs";

        /// <summary>
        /// The error message for a failure or warning.
        /// </summary>
        public const string ErrorMessage = "errorMessage";

        /// <summary>
        /// The call stack for a failure or warning.
        /// </summary>
        public const string CallStack = "callStack";

        /// <summary>
        /// a boolean indicating whether the operation succeeded or failed.
        /// </summary>
        public const string Success = "success";

        /// <summary>
        /// The activity Id.
        /// </summary>
        public const string ActivityId = "ActivityId";

        /// <summary>
        /// The related activity Id.
        /// </summary>
        public const string RelatedActivityId = "RelatedActivityId";

        /// <summary>
        /// The Event Name.
        /// </summary>
        public const string EventType = "EventType";

        /// <summary>
        /// The provider Name.
        /// </summary>
        public const string ProviderName = "ProviderName";

        /// <summary>
        /// The path to the log file.
        /// </summary>
        public const string Path = "Path";

        /// <summary>
        /// The time the event occurred.
        /// </summary>
        public const string TimeStamp = "TimeStamp";

        /// <summary>
        /// The process Id for the event.
        /// </summary>
        public const string ProcessID = "ProcessID";

        /// <summary>
        /// The thread id for the event.
        /// </summary>
        public const string ThreadID = "ThreadID";

        /// <summary>
        /// The friendly message for the event.
        /// </summary>
        public const string FormattedMessage = "FormattedMessage";

        /// <summary>
        /// The name of the hierarchy level property.
        /// </summary>
        public const string HierarchyLevel = "AS_HierarchyLevel";

        /// <summary>
        /// The type of the event with tree prefix characters for display.
        /// </summary>
        public const string TreeEventType = "AS_TreeEventType";

        /// <summary>
        /// If this event is on the hot path it contains the time this event started effecting the hot path.
        /// </summary>
        public const string HotPathStartTime = "HotPathStartTime";

        /// <summary>
        /// If this event is on the hot path it contains the time this event stopped effecting the hot path.
        /// </summary>
        public const string HotPathEndTime = "HotPathEndTime";

        /// <summary>
        /// If this event is on the hot path it contains the duration of time this event contributed to the hot path.
        /// </summary>
        public const string HotPathDurationMs = "HotPathDurationMs";

        /// <summary>
        /// The name of the event formatter.
        /// </summary>
        public const string EventFormatterName = nameof(EventFormatterName);

        /// <summary>
        /// Gets the properties provided by the generic Azure Stack tools. These properties are added to every wrapped event object.
        /// </summary>
        public static IReadOnlyList<string> AsProvidedProperties { get; } = (new List<string>
        {
            ActivityId,
            RelatedActivityId,
            EventType,
            ProviderName,
            Path,
            TimeStamp,
            ProcessID,
            ThreadID,
            FormattedMessage,
            HierarchyLevel,
            TreeEventType
        }).AsReadOnly();
    }
}
