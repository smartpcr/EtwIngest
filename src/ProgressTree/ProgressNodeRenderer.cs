// -----------------------------------------------------------------------
// <copyright file="ProgressNodeEx.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ProgressTree
{
    using Spectre.Console;

    public static class ProgressNodeRenderer
    {
        private const int MaxProgressBarWidth = 42;

        public static void RefreshTaskStatus(IProgressNode node, ProgressTask task, NodeProgress progress)
        {
            var prefix = GetTreePrefix(node.Depth);
            prefix = $"{prefix} {node.Id}";
            switch (progress.Status)
            {
                case ProgressStatus.NotStarted:
                    task.Description = $"{prefix} [grey](Not Started)[/]";
                    break;
                case ProgressStatus.InProgress:
                    task.Description = $"{prefix} [yellow]({progress.StartTime!.Value:HH:mm:ss}: In Progress...{progress.StatusMessage})[/]";
                    break;
                case ProgressStatus.Completed:
                    var durationMs = progress.DurationMs;
                    var durationSec = durationMs / 1000;
                    var duration = durationMs > 1000 ? $"{durationSec:F1}s" : $"{durationMs:F0}ms";
                    task.Description = $"{prefix} [green]({duration}: Completed)[/]";
                    break;
                case ProgressStatus.Failed:
                    task.Description = $"{prefix} [red](Failed: {progress.ErrorMessage})[/]";
                    break;
                default:
                    task.Description = $"{prefix} (Unknown Status)";
                    break;
            }
        }

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

        #region completed rendering

        private static void RenderChildren(IProgressNode parent, IProgressNode root, List<string> lines, string prefix, bool isRoot)
        {
            var children = parent.Children.ToList();
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

                lines.Add(ProgressNodeRenderer.FormatChildNode(child, root, nodePrefix));

                // Recursively render this child's children
                if (child.Children.Any())
                {
                    RenderChildren(child, root, lines, childPrefix, false);
                }
            }
        }

        private static string FormatRootNode(IProgressNode node)
        {
            var status = node.Status == ProgressStatus.Completed ? "✓" : "●";
            var modeStr = node.Children.Any()
                ? "J " // job
                : "S "; // step

            var durationStr = FormatDuration(node.Duration);
            var progressBar = new string('━', MaxProgressBarWidth);
            var percentage = node.Status == ProgressStatus.Completed ? "100%" : $"{node.ProgressPercent * 100:F0}%";

            // Extract the base description (node Id) without markup
            var nodeId = ProgressNodeRenderer.GetNormalizedNodeId(node);

            // Calculate padding to align progress bars
            var baseDescLength = status.Length + 1 + nodeId.Length + 1 + $"({modeStr}{durationStr})".Length;
            var targetColumn = 50;
            var padding = Math.Max(1, targetColumn - baseDescLength);

            return $"{status} {nodeId} ({modeStr}{durationStr}){new string(' ', padding)}{progressBar} {percentage}";
        }

        private static string FormatChildNode(IProgressNode node, IProgressNode root, string prefix)
        {
            var status = node.Status == ProgressStatus.Completed ? "✓" : "●";
            var percentage = node.Status == ProgressStatus.Completed ? "100%" : $"{node.ProgressPercent * 100:F0}%";
            var nodeId = ProgressNodeRenderer.GetNormalizedNodeId(node);
            var modeStr = node.Children.Any() ? "J " : "S "; // default to step
            var durationDisplay = $"({modeStr}{FormatDuration(node.Duration)})";

            // Calculate padding to align the start of the progress bar area
            var baseDescLength = prefix.Length + status.Length + 1 + nodeId.Length + 1 + durationDisplay.Length;
            var targetColumn = 50; // Target column where progress bar area starts
            var descPadding = Math.Max(1, targetColumn - baseDescLength);

            // Create timeline-positioned progress bar
            var progressBar = ProgressNodeRenderer.CreateTimelineProgressBar(node, root);

            return $"{prefix}{status} {nodeId} {durationDisplay}{new string(' ', descPadding)}{progressBar} {percentage}";
        }

        #endregion

        #region utils
        private static string FormatDuration(double duration)
        {
            if (duration >= 60 * 1000)
            {
                var seconds = (int)(duration / 1000);
                var minutes = seconds / 60;
                seconds = seconds % 60;
                return $"{minutes}m{seconds:D2}s";
            }

            if (duration >= 1000)
            {
                var seconds = (int)(duration / 1000);
                return $"{seconds:F1}s";
            }

            if (duration > 0)
            {
                return $"{duration:F0}ms";
            }

            return "0ms";
        }

        private static string GetTreePrefix(int depth)
        {
            if (depth == 0)
            {
                return string.Empty;
            }

            if (depth == 1)
            {
                return "├── ";
            }

            // Build nested tree structure: "│  " for each parent level, then "├── " for this level
            var treeChars = string.Concat(Enumerable.Repeat("│  ", depth - 1));
            return $"{treeChars}├── ";
        }

        private static string GetNormalizedNodeId(IProgressNode node)
        {
            return node.Id.Replace("_", " ").Replace("-", " ");
        }

        private static string CreateTimelineProgressBar(IProgressNode node, IProgressNode root)
        {
            if (root.Duration <= 0)
            {
                return new string('━', 1);
            }

            // Calculate start offset based on parent's execution mode
            var startOffsetSeconds = node.StartTime.HasValue ? node.StartTime.Value.Subtract(root.StartTime!.Value).TotalMilliseconds : 0;
            var startProportion = startOffsetSeconds / root.Duration;

            var startPosition = (int)(MaxProgressBarWidth * startProportion);
            startPosition = Math.Max(0, Math.Min(MaxProgressBarWidth - 1, startPosition));

            // Calculate bar length (how long this task took relative to root)
            var durationProportion = node.Duration / root.Duration;
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
        #endregion
    }
}