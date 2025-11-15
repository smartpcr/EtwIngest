//-----------------------------------------------------------------------
// <copyright file="WorkflowTreeRenderer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Renders a completed workflow tree with timeline-positioned progress bars based on effective timing.
    /// </summary>
    public static class WorkflowTreeRenderer
    {
        private const int MaxProgressBarWidth = 42;

        /// <summary>
        /// Renders the completed workflow tree to console with timeline-positioned progress bars.
        /// </summary>
        /// <param name="root">The root node of the completed workflow.</param>
        public static void RenderCompleted(IProgressNode root)
        {
            var lines = new List<string>();

            // Render root
            lines.Add(FormatRootNode(root));

            // Render children recursively, passing root for timeline calculation
            RenderChildren(root, root, lines, string.Empty, true);

            // Print all lines
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Formats the root node with its progress bar.
        /// </summary>
        private static string FormatRootNode(IProgressNode node)
        {
            var status = node.IsCompleted ? "✓" : "●";
            var detectedMode = node.DetectedExecutionMode;
            var modeStr = detectedMode.HasValue
                ? $"{(detectedMode.Value == ExecutionMode.Sequential ? "S" : "P")} "
                : string.Empty;

            var durationStr = FormatDuration(node.EffectiveDuration);
            var progressBar = new string('━', MaxProgressBarWidth);
            var percentage = node.IsCompleted ? "100%" : $"{node.Value:F0}%";

            // Extract the base description (node Id) without markup
            var description = GetBaseDescription(node);

            // Calculate padding to align progress bars
            var baseDescLength = status.Length + 1 + description.Length + 1 + $"({modeStr}{durationStr})".Length;
            var targetColumn = 50;
            var padding = Math.Max(1, targetColumn - baseDescLength);

            return $"{status} {description} ({modeStr}{durationStr}){new string(' ', padding)}{progressBar} {percentage}";
        }

        /// <summary>
        /// Gets the base description without markup tags and timing info.
        /// </summary>
        private static string GetBaseDescription(IProgressNode node)
        {
            // The Id property is the clean name without markup
            return node.Id.Replace("_", " ").Replace("-", " ");
        }

        /// <summary>
        /// Recursively renders children nodes.
        /// </summary>
        private static void RenderChildren(IProgressNode parent, IProgressNode root, List<string> lines, string prefix, bool isRoot, double parentOffset = 0)
        {
            var children = parent.Children.ToList();

            // Calculate cumulative offset for sequential children
            double cumulativeOffset = 0;

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var isLast = i == children.Count - 1;

                // Build the tree structure prefix
                string nodePrefix;
                string childPrefix;

                if (isRoot)
                {
                    nodePrefix = isLast ? "└── " : "├── ";
                    childPrefix = isLast ? "    " : "│   ";
                }
                else
                {
                    nodePrefix = prefix + (isLast ? "└── " : "├── ");
                    childPrefix = prefix + (isLast ? "    " : "│   ");
                }

                // For sequential execution, accumulate offset from parent
                var parentMode = parent.ExecutionMode;
                var absoluteOffset = parentMode == ExecutionMode.Sequential ? parentOffset + cumulativeOffset : 0;

                lines.Add(FormatChildNode(child, root, parent, nodePrefix, absoluteOffset));

                // Update cumulative offset for next sequential sibling
                if (parentMode == ExecutionMode.Sequential)
                {
                    cumulativeOffset += child.EffectiveDuration;
                }

                // Recursively render this child's children
                if (child.Children.Any())
                {
                    // Calculate the offset to pass to child's children
                    double childOffset;
                    if (parentMode == ExecutionMode.Sequential)
                    {
                        // Sequential: child inherits the cumulative offset
                        childOffset = absoluteOffset;
                    }
                    else
                    {
                        // Parallel: calculate child's actual start time offset from root
                        childOffset = (child.EffectiveStartTime - root.EffectiveStartTime).TotalSeconds;
                    }

                    RenderChildren(child, root, lines, childPrefix, false, childOffset);
                }
            }
        }

        /// <summary>
        /// Formats a child node with timeline-positioned progress bar.
        /// </summary>
        private static string FormatChildNode(IProgressNode node, IProgressNode root, IProgressNode parent, string prefix, double offset)
        {
            var status = node.IsCompleted ? "✓" : "●";

            // Get clean description
            var description = GetBaseDescription(node);

            // Format duration
            string durationDisplay;
            if (node.Children.Any())
            {
                // Parent node - show detected mode if available
                var detectedMode = node.DetectedExecutionMode;
                var modeStr = detectedMode.HasValue
                    ? $"{(detectedMode.Value == ExecutionMode.Sequential ? "S" : "P")} "
                    : string.Empty;

                var actualDuration = node.ActualDuration;
                var effectiveDuration = node.EffectiveDuration;

                // Show both if they differ significantly
                if (actualDuration > 0 && Math.Abs(effectiveDuration - actualDuration) > 0.1)
                {
                    durationDisplay = $"({modeStr}{FormatDuration(effectiveDuration)}, actual: {FormatDuration(actualDuration)})";
                }
                else
                {
                    durationDisplay = $"({modeStr}{FormatDuration(effectiveDuration)})";
                }
            }
            else
            {
                // Leaf node - show actual duration
                var duration = node.ActualDuration > 0 ? node.ActualDuration : node.EffectiveDuration;
                durationDisplay = $"({FormatDuration(duration)})";
            }

            var percentage = node.IsCompleted ? "100%" : $"{node.Value:F0}%";

            // Calculate padding to align the start of the progress bar area
            var baseDescLength = prefix.Length + status.Length + 1 + description.Length + 1 + durationDisplay.Length;
            var targetColumn = 50; // Target column where progress bar area starts
            var descPadding = Math.Max(1, targetColumn - baseDescLength);

            // Create timeline-positioned progress bar
            var progressBar = CreateTimelineProgressBar(node, root, parent, offset);

            return $"{prefix}{status} {description} {durationDisplay}{new string(' ', descPadding)}{progressBar} {percentage}";
        }

        /// <summary>
        /// Creates a timeline-positioned progress bar based on when the task started and how long it took.
        /// All bars are positioned relative to root duration for consistent timeline visualization.
        /// </summary>
        private static string CreateTimelineProgressBar(IProgressNode node, IProgressNode root, IProgressNode parent, double offset)
        {
            if (root.EffectiveDuration <= 0)
            {
                return new string('━', 1);
            }

            // Calculate start offset based on parent's execution mode
            double startOffsetSeconds;
            if (parent.ExecutionMode == ExecutionMode.Sequential)
            {
                // For sequential execution, use cumulative offset (absolute from root)
                startOffsetSeconds = offset;
            }
            else
            {
                // For parallel execution, use actual start time relative to root
                startOffsetSeconds = (node.EffectiveStartTime - root.EffectiveStartTime).TotalSeconds;
            }

            var startProportion = startOffsetSeconds / root.EffectiveDuration;
            var startPosition = (int)(MaxProgressBarWidth * startProportion);
            startPosition = Math.Max(0, Math.Min(MaxProgressBarWidth - 1, startPosition));

            // Calculate bar length (how long this task took relative to root)
            var durationProportion = node.EffectiveDuration / root.EffectiveDuration;
            var barLength = (int)(MaxProgressBarWidth * durationProportion);
            barLength = Math.Max(1, barLength); // At least 1 char

            // Ensure start + length doesn't exceed max
            if (startPosition + barLength > MaxProgressBarWidth)
            {
                barLength = MaxProgressBarWidth - startPosition;
            }

            // Build the timeline bar:
            // [padding before][actual bar][padding after]
            var beforePadding = new string(' ', startPosition);
            var bar = new string('━', barLength);
            var afterLength = MaxProgressBarWidth - startPosition - barLength;
            var afterPadding = afterLength > 0 ? new string(' ', afterLength) : string.Empty;

            return beforePadding + bar + afterPadding;
        }

        /// <summary>
        /// Formats a duration in seconds to a human-readable string.
        /// </summary>
        private static string FormatDuration(double duration)
        {
            if (duration >= 60)
            {
                var minutes = (int)(duration / 60);
                var seconds = (int)(duration % 60);
                return $"{minutes}m{seconds:D2}s";
            }
            else if (duration >= 1.0)
            {
                return $"{duration:F1}s";
            }
            else if (duration > 0)
            {
                return $"{duration * 1000:F0}ms";
            }
            else
            {
                return "0ms";
            }
        }
    }
}
