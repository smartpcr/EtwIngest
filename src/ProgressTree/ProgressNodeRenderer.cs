// -----------------------------------------------------------------------
// <copyright file="ProgressNodeEx.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ProgressTree
{
    using System.Text;
    using Spectre.Console;
    using Spectre.Console.Rendering;

    public static class ProgressNodeRenderer
    {
        private const int TimelineWidth = 40;
        private const int NameColumnWidth = 45;

        #region live rendering
        /// <summary>
        /// real time progress status
        /// </summary>
        public static void RefreshTaskStatus(IProgressNode node, ProgressTask task)
        {
            var prefix = GetTreePrefixFromNode(node);
            var statusIcon = GetStatusIcon(node.Status);
            var statusColor = GetStatusColor(node.Status);

            var hasChildren = node.Children.Count > 0;
            var modeIndicator = hasChildren ? (node.RunChildrenInParallel ? "P " : "S ") : "";

            var currentDurationMs = node.Status == ProgressStatus.InProgress && node.StartTime.HasValue
                ? (DateTime.UtcNow - node.StartTime.Value).TotalMilliseconds
                : node.EffectiveDurationMs;

            var duration = FormatDuration(currentDurationMs);
            var nodeInfo = hasChildren ? $"({modeIndicator}{duration})" : $"({duration})";

            // Just description - Spectre.Console Progress handles the bar
            var line = $"{prefix}{statusIcon} [{statusColor}]{node.Name}[/] {nodeInfo}";
            task.Description(line);
            task.Value = node.ProgressPercentage;
        }

        private static IRenderable BuildLiveRenderable(ProgressNode root)
        {
            var rootStart = root.EffectiveStartTime ?? DateTime.UtcNow;
            var rootEnd = root.EffectiveStopTime ?? DateTime.UtcNow;

            // For in-progress, use current time as end
            if (root.Status == ProgressStatus.InProgress)
                rootEnd = DateTime.UtcNow;

            var totalDurationMs = (rootEnd - rootStart).TotalMilliseconds;
            if (totalDurationMs <= 0) totalDurationMs = 1;

            var lines = new List<string>();
            BuildNodeLines(root, rootStart, totalDurationMs, "", true, true, lines);

            var markup = string.Join("\n", lines);
            return new Markup(markup);
        }

        private static void BuildNodeLines(
            IProgressNode node,
            DateTime rootStart,
            double totalDurationMs,
            string prefix,
            bool isLast,
            bool isRoot,
            List<string> lines)
        {
            string treePrefix;
            string childPrefix;

            if (isRoot)
            {
                treePrefix = "";
                childPrefix = "";
            }
            else
            {
                treePrefix = prefix + (isLast ? "└── " : "├── ");
                childPrefix = prefix + (isLast ? "    " : "│   ");
            }

            var statusIcon = GetStatusIcon(node.Status);
            var statusColor = GetStatusColor(node.Status);

            var hasChildren = node.Children.Count > 0;
            var modeIndicator = hasChildren ? (node.RunChildrenInParallel ? "P " : "S ") : "";

            // Calculate current duration for in-progress nodes
            double currentDurationMs;
            DateTime nodeEnd;

            if (node.Status == ProgressStatus.InProgress)
            {
                nodeEnd = DateTime.UtcNow;
                currentDurationMs = node.StartTime.HasValue
                    ? (nodeEnd - node.StartTime.Value).TotalMilliseconds
                    : 0;
            }
            else
            {
                nodeEnd = node.EffectiveStopTime ?? rootStart;
                currentDurationMs = node.EffectiveDurationMs;
            }

            var duration = FormatDuration(currentDurationMs);
            var nodeInfo = hasChildren ? $"({modeIndicator}{duration})" : $"({duration})";

            var nameSection = $"{treePrefix}{node.Name} {nodeInfo}";
            var padding = Math.Max(0, NameColumnWidth - nameSection.Length);

            // Timeline calculation
            var nodeStart = node.EffectiveStartTime ?? rootStart;
            if (node.Status == ProgressStatus.InProgress)
                nodeEnd = DateTime.UtcNow;
            else
                nodeEnd = node.EffectiveStopTime ?? nodeStart;

            var nodeDurationMs = (nodeEnd - nodeStart).TotalMilliseconds;
            var startOffset = (nodeStart - rootStart).TotalMilliseconds / totalDurationMs;
            var barWidth = nodeDurationMs / totalDurationMs;

            var barStartPos = (int)(startOffset * TimelineWidth);
            var barLength = Math.Max(1, (int)(barWidth * TimelineWidth));

            if (barStartPos + barLength > TimelineWidth)
                barLength = TimelineWidth - barStartPos;

            // Different bar style for in-progress
            var barChar = node.Status == ProgressStatus.InProgress ? '─' : '━';
            var timelineBar = new string(' ', barStartPos) +
                              new string(barChar, barLength) +
                              new string(' ', TimelineWidth - barStartPos - barLength);

            var percentage = $"{node.ProgressPercentage:F0}%";
            var line = $"{treePrefix}{statusIcon} [{statusColor}]{node.Name}[/] {nodeInfo}{new string(' ', padding)} [{statusColor}]{timelineBar}[/] {percentage}";
            lines.Add(line);

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var isLastChild = i == node.Children.Count - 1;
                BuildNodeLines(child, rootStart, totalDurationMs, childPrefix, isLastChild, false, lines);
            }
        }

        #endregion

        #region completed rendering

        public static void RenderTree(ProgressNode root)
        {
            var rootStart = root.EffectiveStartTime ?? DateTime.UtcNow;
            var rootEnd = root.EffectiveStopTime ?? DateTime.UtcNow;
            var totalDurationMs = (rootEnd - rootStart).TotalMilliseconds;

            if (totalDurationMs <= 0) totalDurationMs = 1;

            // Render root node
            RenderNode(root, rootStart, totalDurationMs, "", true, true);
        }

        private static void RenderNode(
            IProgressNode node,
            DateTime rootStart,
            double totalDurationMs,
            string prefix,
            bool isLast,
            bool isRoot)
        {
            // Build the tree prefix
            string treePrefix;
            string childPrefix;

            if (isRoot)
            {
                treePrefix = "";
                childPrefix = "";
            }
            else
            {
                treePrefix = prefix + (isLast ? "└── " : "├── ");
                childPrefix = prefix + (isLast ? "    " : "│   ");
            }

            // Status icon
            var statusIcon = GetStatusIcon(node.Status);
            var statusColor = GetStatusColor(node.Status);

            // Node name with mode and duration
            var hasChildren = node.Children.Count > 0;
            var modeIndicator = hasChildren ? (node.RunChildrenInParallel ? "P " : "S ") : "";
            var duration = FormatDuration(node.EffectiveDurationMs);
            var displayName = GetNormalizedNodeId(node);
            var nodeName = $"[{statusColor}]{displayName}[/]";
            var nodeInfo = hasChildren ? $"({modeIndicator}{duration})" : $"({duration})";

            // Calculate name section width (include 2 chars for "✓ " status icon)
            // Calculate plain text width: treePrefix + icon(1) + space(1) + name + space(1) + nodeInfo
            var plainTextLength = treePrefix.Length + 2 + displayName.Length + 1 + nodeInfo.Length;
            var padding = Math.Max(0, NameColumnWidth - plainTextLength);

            // Calculate timeline bar position and width
            var nodeStart = node.EffectiveStartTime ?? rootStart;
            var nodeEnd = node.EffectiveStopTime ?? nodeStart;
            var nodeDurationMs = (nodeEnd - nodeStart).TotalMilliseconds;

            var startOffset = (nodeStart - rootStart).TotalMilliseconds / totalDurationMs;
            var barWidth = nodeDurationMs / totalDurationMs;

            var barStartPos = (int)(startOffset * TimelineWidth);
            var barLength = Math.Max(1, (int)(barWidth * TimelineWidth));

            // Ensure bar doesn't exceed timeline
            if (barStartPos + barLength > TimelineWidth)
                barLength = TimelineWidth - barStartPos;

            // Build timeline bar
            var timelineBar =
                new string(' ', barStartPos) +
                new string('━', barLength) +
                new string(' ', TimelineWidth - barStartPos - barLength);

            // Right-align percentage in 4 chars
            var percentage = $"{node.ProgressPercentage,4:F0}%";

            // Build the full line
            var markup = $"{treePrefix}{statusIcon} {nodeName} {nodeInfo}{new string(' ', padding)} [{statusColor}]{timelineBar}[/] {percentage}";

            AnsiConsole.MarkupLine(markup);

            // Render children
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var isLastChild = i == node.Children.Count - 1;
                RenderNode(child, rootStart, totalDurationMs, childPrefix, isLastChild, false);
            }
        }

        #endregion

        #region utils
        public static string GetStatusIcon(ProgressStatus status) => status switch
        {
            ProgressStatus.NotStarted => "[grey]○[/]",
            ProgressStatus.InProgress => "[blue]◐[/]",
            ProgressStatus.Completed => "[green]✓[/]",
            ProgressStatus.Failed => "[red]✗[/]",
            ProgressStatus.Cancelled => "[yellow]⊘[/]",
            _ => " "
        };

        public static string GetStatusColor(ProgressStatus status) => status switch
        {
            ProgressStatus.NotStarted => "grey",
            ProgressStatus.InProgress => "blue",
            ProgressStatus.Completed => "green",
            ProgressStatus.Failed => "red",
            ProgressStatus.Cancelled => "yellow",
            _ => "white"
        };

        public static string FormatDuration(double milliseconds)
        {
            if (milliseconds >= 60 * 1000)
            {
                var seconds = (int)(milliseconds / 1000);
                var minutes = seconds / 60;
                seconds = seconds % 60;
                return $"{minutes}m{seconds:D2}s";
            }

            if (milliseconds >= 1000)
            {
                var seconds = milliseconds / 1000;
                return $"{seconds:F1}s";
            }

            if (milliseconds > 0)
            {
                return $"{milliseconds:F0}ms";
            }

            return "0ms";
        }

        private static string GetTreePrefixFromNode(IProgressNode node)
        {
            if (node.Parent == null)
                return "";  // Root node

            var prefixParts = new List<string>();
            var current = node;
            var ancestors = new List<(IProgressNode node, bool isLast)>();

            // Walk up to build ancestor chain
            while (current.Parent != null)
            {
                var parent = current.Parent;
                var siblings = parent.Children;
                var isLast = siblings.IndexOf(current) == siblings.Count - 1;
                ancestors.Add((current, isLast));
                current = parent;
            }

            // Reverse to go from root down
            ancestors.Reverse();

            var sb = new StringBuilder();
            for (int i = 0; i < ancestors.Count; i++)
            {
                var (_, isLast) = ancestors[i];
                if (i == ancestors.Count - 1)
                {
                    // This node's connector
                    sb.Append(isLast ? "└── " : "├── ");
                }
                else
                {
                    // Ancestor's continuation line
                    sb.Append(isLast ? "    " : "│   ");
                }
            }

            return sb.ToString();
        }

        private static string GetNormalizedNodeId(IProgressNode node)
        {
            return node.Id.Replace("_", " ").Replace("-", " ").Trim();
        }

        #endregion
    }
}