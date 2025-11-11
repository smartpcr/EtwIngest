//-----------------------------------------------------------------------
// <copyright file="ASEventObject.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReader.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A helper class which holds the data about trace events.
    /// </summary>
    public class ASEventObject
    {
        /// <summary>
        /// The type name for ASEvent objects.
        /// </summary>
        public const string ASEventTypeName = "Microsoft.WindowsAzure.Diagnostics.Event";

        /// <summary>
        /// The payload properties of this traced event.
        /// </summary>
        private Dictionary<string, object> properties;

        /// <summary>
        /// Indicator that this event is the last sibling at a certain hierarchy level under a given parent.
        /// </summary>
        private ASEventObject? lastSiblingObject;

        /// <summary>
        /// The record of whether the operation succeeded or not.
        /// </summary>
        private bool? success;

        /// <summary>
        /// Initializes a new instance of the <see cref="ASEventObject"/> class.
        /// </summary>
        public ASEventObject()
        {
            this.DefaultDisplayNames = new List<string>();
            this.properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success
        {
            get
            {
                if (this.success.HasValue)
                {
                    return this.success.Value;
                }
                else
                {
                    return string.IsNullOrEmpty(this.ErrorMessage);
                }
            }

            set
            {
                this.success = value;
            }
        }

        /// <summary>
        /// Gets or sets the duration in milliseconds that the operation took to complete.
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// Gets or sets an error or warning message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the call stack for any error or warning.
        /// </summary>
        public string? CallStack { get; private set; }

        /// <summary>
        /// Gets the event type for this event.
        /// </summary>
        public string? EventType { get; private set; }

        /// <summary>
        /// Gets a collection of properties from the event.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties
        {
            get
            {
                return this.properties;
            }
        }

        /// <summary>
        /// Gets the list of display names which will be visible by default.
        /// </summary>
        public List<string> DefaultDisplayNames { get; } = new List<string>();

        /// <summary>
        /// Gets the time the event occurred.
        /// </summary>
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Gets the level in the hierarchy tree of this event with 0 being a root event.
        /// </summary>
        public int HierarchyLevel { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this event is the last event among siblings in a hierarchy level.
        /// </summary>
        public bool IsLastSibling { get; private set; }

        /// <summary>
        /// Gets the parent event for this event.
        /// </summary>
        public ASEventObject? Parent { get; private set; }

        /// <summary>
        /// Adds a property to the collection of properties.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value for the property.</param>
        public void AddProperty(string name, object value)
        {
            if (name == PropertyNames.Success)
            {
                try
                {
                    // Attempt to convert the success field. It is possible someone created a non boolean field and called it success,
                    // in this case we just ignore it as a success/fail flag and add it as a simple property.
                    this.Success = Convert.ToBoolean(value);
                }
                catch
                {
                    this.properties.Add(name, value);
                }
            }
            else
            {
                // every property except success is a direct map.
                this.properties.Add(name, value);

                if (name == PropertyNames.ErrorMessage)
                {
                    this.ErrorMessage = value as string;
                }
                else if (name == PropertyNames.CallStack)
                {
                    this.CallStack = value as string;
                }
                else if (name == PropertyNames.EventType)
                {
                    this.EventType = value as string;
                }
                else if (name == PropertyNames.TimeStamp)
                {
                    try
                    {
                        this.TimeStamp = Convert.ToDateTime(value);
                    }
                    catch
                    {
                    }
                }
                else if (name == PropertyNames.DurationMs)
                {
                    try
                    {
                        this.DurationMs = Convert.ToInt64(value);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Adds a child event under this event.
        /// </summary>
        /// <param name="child">The child event.</param>
        public void AddChild(ASEventObject child)
        {
            child.HierarchyLevel = this.HierarchyLevel + 1;

            if (this.lastSiblingObject != null)
            {
                this.lastSiblingObject.IsLastSibling = false;
            }

            this.lastSiblingObject = child;
            child.IsLastSibling = true;
            child.Parent = this;
        }

        /// <summary>
        /// Converts the event to a formatted string representation.
        /// </summary>
        /// <returns>String representation of this event.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"EventType: {this.GetTreeEventType()}");
            sb.AppendLine($"TimeStamp: {this.TimeStamp}");

            if (this.success.HasValue || this.ErrorMessage != null)
            {
                sb.AppendLine($"Success: {this.Success}");
            }

            if (this.DurationMs.HasValue)
            {
                sb.AppendLine($"DurationMs: {this.DurationMs}");
            }

            foreach (var kvp in this.Properties)
            {
                // Skip properties already displayed above
                if (kvp.Key == PropertyNames.EventType ||
                    kvp.Key == PropertyNames.TimeStamp ||
                    kvp.Key == PropertyNames.Success ||
                    kvp.Key == PropertyNames.DurationMs)
                {
                    continue;
                }

                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            sb.AppendLine($"HierarchyLevel: {this.HierarchyLevel}");
            sb.AppendLine("---");

            return sb.ToString();
        }

        /// <summary>
        /// Computes an event type name with prefix characters to be able to show as part of a tree.
        /// </summary>
        /// <returns>The event type with prefixed characters to display this as a tree.</returns>
        private string GetTreeEventType()
        {
            var result = this.EventType ?? string.Empty;
            var currentNode = this;
            var currentParent = this.Parent;

            while (currentParent != null)
            {
                if (currentNode.IsLastSibling)
                {
                    if (currentNode == this)
                    {
                        result = "└───" + result;
                    }
                    else
                    {
                        result = "    " + result;
                    }
                }
                else
                {
                    if (currentNode == this)
                    {
                        result = "├───" + result;
                    }
                    else
                    {
                        result = "│   " + result;
                    }
                }

                currentNode = currentParent;
                currentParent = currentParent.Parent;
            }

            return result;
        }
    }
}
