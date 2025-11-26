//-----------------------------------------------------------------------
// <copyright file="ProgressTreeManager.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Threading.Tasks;
    using Spectre.Console;

    /// <summary>
    /// Manages a hierarchical progress tree using Spectre.Console.
    /// </summary>
    public class ProgressTreeMonitor : IProgressTreeMonitor
    {
        public async Task StartAsync(string name, Action<IProgressNode> buildAction)
        {
            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .AutoClear(true)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn()
                    {
                        Alignment = Justify.Left
                    },
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var rootNode = new ProgressNode(ctx, "__root__", name);
                    buildAction(rootNode);
                    await rootNode.ExecuteAsync(CancellationToken.None);

                    ProgressNodeRenderer.RenderTree(rootNode);
                });
        }

    }
}
