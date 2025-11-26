// -----------------------------------------------------------------------
// <copyright file="ProgressNodeRenderer.cs" company="Microsoft Corp.">
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

        #region Data Structures

        private readonly record struct NodeRenderContext(
            DateTime RootStart,
            double TotalDurationMs);

        private readonly record struct NodeRenderData(
            string TreePrefix,
            string ChildPrefix,
            string StatusIcon,
            string StatusColor,
            string DisplayName,
            string NodeInfo,
            double CurrentDurationMs,
            DateTime EffectiveStart,
            DateTime EffectiveEnd);

        #endregion

        #region Public API

        /// <summary>
        /// Renders completed tree to console.
        /// </summary>
        public static void RenderTree(ProgressNode root)
        {
            var context = CreateContext(root);
            RenderNodeRecursive(root, context, "", isLast: true, isRoot: true);
        }

        /// <summary>
        /// Refreshes a single task's status for live Spectre.Console Progress display.
        /// </summary>
        public static void RefreshTaskStatus(IProgressNode node, ProgressTask task)
        {
            var prefix = GetTreePrefixFromNode(node);
            var data = CalculateNodeData(node, prefix, "");

            var line = $"{prefix}{data.StatusIcon} [{data.StatusColor}]{node.Name}[/] {data.NodeInfo}";
            task.Description(line);
            task.Value = node.ProgressPercentage;
        }

        /// <summary>
        /// Builds a live renderable for the entire tree.
        /// </summary>
        public static IRenderable BuildLiveRenderable(ProgressNode root)
        {
            var context = CreateContext(root);
            var lines = new List<string>();
            BuildNodeLinesRecursive(root, context, "", isLast: true, isRoot: true, lines);
            return new Markup(string.Join("\n", lines));
        }

        #endregion

        #region Core Rendering Logic

        private static NodeRenderContext CreateContext(ProgressNode root)
        {
            var rootStart = root.EffectiveStartTime ?? DateTime.UtcNow;
            var rootEnd = root.Status == ProgressStatus.InProgress
                ? DateTime.UtcNow
                : root.EffectiveStopTime ?? DateTime.UtcNow;

            var totalDurationMs = Math.Max(1, (rootEnd - rootStart).TotalMilliseconds);
            return new NodeRenderContext(rootStart, totalDurationMs);
        }

        private static NodeRenderData CalculateNodeData(
            IProgressNode node,
            string treePrefix,
            string childPrefix)
        {
            var statusIcon = GetStatusIcon(node.Status);
            var statusColor = GetStatusColor(node.Status);
            var displayName = GetNormalizedNodeId(node);

            var hasChildren = node.Children.Count > 0;
            var modeIndicator = hasChildren ? (node.RunChildrenInParallel ? "P " : "S ") : "";

            // Calculate duration based on status
            double currentDurationMs;
            DateTime effectiveEnd;

            if (node.Status == ProgressStatus.InProgress && node.StartTime.HasValue)
            {
                effectiveEnd = DateTime.UtcNow;
                currentDurationMs = (effectiveEnd - node.StartTime.Value).TotalMilliseconds;
            }
            else
            {
                effectiveEnd = node.EffectiveStopTime ?? node.EffectiveStartTime ?? DateTime.UtcNow;
                currentDurationMs = node.EffectiveDurationMs;
            }

            var duration = FormatDuration(currentDurationMs);
            var nodeInfo = hasChildren ? $"({modeIndicator}{duration})" : $"({duration})";

            var effectiveStart = node.EffectiveStartTime ?? DateTime.UtcNow;

            return new NodeRenderData(
                treePrefix,
                childPrefix,
                statusIcon,
                statusColor,
                displayName,
                nodeInfo,
                currentDurationMs,
                effectiveStart,
                effectiveEnd);
        }

        private static (string treePrefix, string childPrefix) CalculatePrefixes(
            string parentPrefix,
            bool isLast,
            bool isRoot)
        {
            if (isRoot)
                return ("", "");

            var treePrefix = parentPrefix + (isLast ? "└── " : "├── ");
            var childPrefix = parentPrefix + (isLast ? "    " : "│   ");
            return (treePrefix, childPrefix);
        }

        private static string BuildTimelineBar(
            NodeRenderData data,
            NodeRenderContext context,
            bool useInProgressStyle)
        {
            var startOffset = (data.EffectiveStart - context.RootStart).TotalMilliseconds / context.TotalDurationMs;
            var nodeDurationMs = (data.EffectiveEnd - data.EffectiveStart).TotalMilliseconds;
            var barWidthRatio = nodeDurationMs / context.TotalDurationMs;

            var barStartPos = Math.Max(0, (int)(startOffset * TimelineWidth));
            var barLength = Math.Max(1, (int)(barWidthRatio * TimelineWidth));

            if (barStartPos + barLength > TimelineWidth)
                barLength = TimelineWidth - barStartPos;

            var barChar = useInProgressStyle ? '─' : '━';

            return new string(' ', barStartPos) +
                   new string(barChar, barLength) +
                   new string(' ', TimelineWidth - barStartPos - barLength);
        }

        private static string BuildFullLine(
            IProgressNode node,
            NodeRenderData data,
            NodeRenderContext context,
            bool includeTimeline)
        {
            var plainTextLength = data.TreePrefix.Length + 2 + data.DisplayName.Length + 1 + data.NodeInfo.Length;
            var padding = Math.Max(0, NameColumnWidth - plainTextLength);

            var sb = new StringBuilder();
            sb.Append(data.TreePrefix);
            sb.Append(data.StatusIcon);
            sb.Append($" [{data.StatusColor}]{data.DisplayName}[/] ");
            sb.Append(data.NodeInfo);

            if (includeTimeline)
            {
                var isInProgress = node.Status == ProgressStatus.InProgress;
                var timelineBar = BuildTimelineBar(data, context, isInProgress);
                var percentage = $"{node.ProgressPercentage,4:F0}%";

                sb.Append(new string(' ', padding));
                sb.Append($" [{data.StatusColor}]{timelineBar}[/] ");
                sb.Append(percentage);
            }

            return sb.ToString();
        }

        #endregion

        #region Recursive Traversal

        private static void RenderNodeRecursive(
            IProgressNode node,
            NodeRenderContext context,
            string prefix,
            bool isLast,
            bool isRoot)
        {
            var (treePrefix, childPrefix) = CalculatePrefixes(prefix, isLast, isRoot);
            var data = CalculateNodeData(node, treePrefix, childPrefix);
            var line = BuildFullLine(node, data, context, includeTimeline: true);

            AnsiConsole.MarkupLine(line);

            for (var i = 0; i < node.Children.Count; i++)
            {
                var isLastChild = i == node.Children.Count - 1;
                RenderNodeRecursive(node.Children[i], context, childPrefix, isLastChild, isRoot: false);
            }
        }

        private static void BuildNodeLinesRecursive(
            IProgressNode node,
            NodeRenderContext context,
            string prefix,
            bool isLast,
            bool isRoot,
            List<string> lines)
        {
            var (treePrefix, childPrefix) = CalculatePrefixes(prefix, isLast, isRoot);
            var data = CalculateNodeData(node, treePrefix, childPrefix);
            var line = BuildFullLine(node, data, context, includeTimeline: true);

            lines.Add(line);

            for (var i = 0; i < node.Children.Count; i++)
            {
                var isLastChild = i == node.Children.Count - 1;
                BuildNodeLinesRecursive(node.Children[i], context, childPrefix, isLastChild, isRoot: false, lines);
            }
        }

        #endregion

        #region Utilities

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
            return milliseconds switch
            {
                >= 60_000 => $"{(int)(milliseconds / 60_000)}m{(int)(milliseconds / 1000) % 60:D2}s",
                >= 1000 => $"{milliseconds / 1000:F1}s",
                > 0 => $"{milliseconds:F0}ms",
                _ => "0ms"
            };
        }

        private static string GetTreePrefixFromNode(IProgressNode node)
        {
            if (node.Parent == null)
                return "";

            var ancestors = new List<bool>(); // true = isLast
            var current = node;

            while (current.Parent != null)
            {
                var siblings = current.Parent.Children;
                ancestors.Add(siblings.IndexOf(current) == siblings.Count - 1);
                current = current.Parent;
            }

            ancestors.Reverse();

            var sb = new StringBuilder();
            for (var i = 0; i < ancestors.Count; i++)
            {
                var isLast = ancestors[i];
                sb.Append(i == ancestors.Count - 1
                    ? (isLast ? "└── " : "├── ")
                    : (isLast ? "    " : "│   "));
            }

            return sb.ToString();
        }

        private static string GetNormalizedNodeId(IProgressNode node) =>
            node.Id.Replace("_", " ").Replace("-", " ").Trim();

        #endregion
    }
}