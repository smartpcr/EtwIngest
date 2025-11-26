//-----------------------------------------------------------------------
// <copyright file="ProgressNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using Spectre.Console;

    /// <summary>
    /// Concrete implementation of IProgressNode that wraps Spectre.Console's ProgressTask.
    /// </summary>
    public class ProgressNode : IProgressNode
    {
        private const double Tolerance = 0.0001;
        private readonly ProgressContext ctx;
        private readonly bool runChildrenInParallel;
        private readonly Func<IProgressNode, CancellationToken, Task>? workerFunc;

        #region props
        public ProgressTask ProgressTask { get; }
        public string Id { get; }
        public string Name { get; set; }
        public double ProgressPercentage { get; set; }
        public IProgressNode? Parent { get; }
        public List<IProgressNode> Children { get; }

        public int Depth { get; }
        public DateTime? CreationTime { get; }
        public DateTime? StartTime { get; set; }
        public DateTime? StopTime { get; set; }
        public double Duration
        {
            get
            {
                if (this.StartTime.HasValue && this.StopTime.HasValue)
                {
                    return (this.StopTime.Value - this.StartTime.Value).TotalMilliseconds;
                }

                if (this.StartTime.HasValue)
                {
                    return (DateTime.UtcNow - this.StartTime.Value).TotalMilliseconds;
                }

                return 0;
            }
        }
        public ProgressStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public string ErrorMessage { get; set; }
        public bool RunChildrenInParallel => this.runChildrenInParallel;
        public DateTime? EffectiveStartTime => this.Children.Count > 0
            ? this.Children.Where(c => c.EffectiveStartTime.HasValue)
                .Select(c => c.EffectiveStartTime)
                .DefaultIfEmpty(this.StartTime)
                .Min() ?? this.StartTime
            : this.StartTime;
        public DateTime? EffectiveStopTime => this.Children.Count > 0
            ? this.Children.Where(c => c.EffectiveStopTime.HasValue)
                .Select(c => c.EffectiveStopTime)
                .DefaultIfEmpty(this.StopTime)
                .Max() ?? this.StopTime
            : this.StopTime;

        public double EffectiveDurationMs => this.Children.Count > 0
            ? (this.EffectiveStopTime.HasValue && this.EffectiveStartTime.HasValue)
                ? (this.EffectiveStopTime.Value - this.EffectiveStartTime.Value).TotalMilliseconds
                : 0
            : this.Duration;

        #endregion

        #region events
        public event ProgressNodeStartedEventHandler? OnStart;
        public event ProgressNodeProgressEventHandler? OnProgress;
        public event ProgressNodeFinishedEventHandler? OnComplete;
        public event ProgressNodeFailedEventHandler? OnFail;
        public event ProgressNodeCanceledEventHandler? OnCancel;
        public event ProgressNodeCreatedEventHandler? OnChildCreated;

        #endregion

        public ProgressNode(
            ProgressContext ctx,
            string id,
            string name,
            ProgressNode? parent = null,
            bool runChildrenInParallel = false,
            Func<IProgressNode, CancellationToken, Task>? workerFunc = null)
        {
            this.Id = id;
            this.Name = name;
            this.ctx = ctx;
            this.runChildrenInParallel = runChildrenInParallel;
            this.workerFunc = workerFunc;
            this.ProgressTask = this.ctx.AddTask(id, maxValue: 100);
            this.Parent = parent;
            this.Children = new List<IProgressNode>();
            this.Depth = this.Parent == null ? 0 : this.Parent.Depth + 1;
            this.CreationTime = DateTime.UtcNow;
            this.Status = ProgressStatus.NotStarted;
            this.StatusMessage = "Not Started";
            this.ErrorMessage = string.Empty;

            this.OnStart += this.OnNodeStart;
            this.OnProgress += this.OnNodeProgress;
            this.OnComplete += this.OnNodeComplete;
            this.OnFail += this.OnNodeFail;
            this.OnCancel += this.OnNodeCancel;

            ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
        }

        public async Task ExecuteAsync(CancellationToken cancel)
        {
            this.Start();

            if (this.workerFunc != null)
            {
                await this.workerFunc(this, cancel);
            }

            if (this.Children.Any())
            {
                if (this.runChildrenInParallel)
                {
                    var tasks = this.Children.Select(c => c.ExecuteAsync(cancel));
                    await Task.WhenAll(tasks);
                }
                else
                {
                    foreach (var child in this.Children)
                    {
                        await  child.ExecuteAsync(cancel);
                    }
                }
            }
            else
            {
                for (var i = 0; i < 100; i++)
                {
                    await Task.Delay(10, cancel);
                    this.UpdateProgress($"Progress {i + 1}%", i + 1);
                }
            }

            this.Complete();
        }

        public IProgressNode AddChild(string id, string name, bool runInParallel = false, Func<IProgressNode, CancellationToken, Task>? childWorkerFunc = null)
        {
            var child = new ProgressNode(this.ctx, id, name, this, runInParallel, childWorkerFunc);
            this.Children.Add(child);
            child.OnStart += this.HandleChildStarted;
            child.OnProgress += this.HandleChildUpdateProgress;
            child.OnComplete += this.HandleChildCompleted;
            child.OnFail += this.HandleChildFailed;
            child.OnCancel += this.HandleChildCancelled;
            return child;
        }

        public NodeProgress GetProgress()
        {
            return new NodeProgress(this.Status, this.Duration, this.ProgressPercentage, this.StartTime, this.StopTime, this.StatusMessage, this.ErrorMessage);
        }

        public void Start()
        {
            if (this.StartTime == null || this.Status == ProgressStatus.NotStarted)
            {
                this.OnStart?.Invoke(this, this.ProgressTask);
            }
        }

        public void Complete()
        {
            if (this.StopTime == null || this.Status != ProgressStatus.Completed)
            {
                this.OnComplete?.Invoke(this, this.ProgressTask);
            }
        }

        public void UpdateProgress(string statusMessage, double value)
        {
            if (this.Status != ProgressStatus.InProgress || Math.Abs(value - this.ProgressPercentage) > Tolerance)
            {
                this.OnProgress?.Invoke(this, this.ProgressTask, statusMessage, value);
            }
        }

        public void Fail(Exception error)
        {
            if (this.Status != ProgressStatus.Failed)
            {
                this.OnFail?.Invoke(this, this.ProgressTask, error);
            }
        }

        public void Cancel()
        {
            if (this.Status != ProgressStatus.Cancelled)
            {
                this.OnCancel?.Invoke(this, this.ProgressTask);
            }
        }

        #region handlers

        private void OnNodeStart(IProgressNode progressNode, ProgressTask task)
        {
            if (this.StartTime == null || this.Status == ProgressStatus.NotStarted)
            {
                this.StartTime = DateTime.UtcNow;
                this.Status = ProgressStatus.InProgress;
                this.ProgressPercentage = 0;
                this.StatusMessage = "Starting...";

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void OnNodeProgress(IProgressNode progressNode, ProgressTask task, string statusMessage, double value)
        {
            if (this.Status != ProgressStatus.InProgress || Math.Abs(value - this.ProgressPercentage) > Tolerance)
            {
                this.Status = ProgressStatus.InProgress;
                this.StatusMessage = statusMessage;
                this.ProgressPercentage = value;

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void OnNodeComplete(IProgressNode progressNode, ProgressTask task)
        {
            if (this.StopTime == null || this.Status != ProgressStatus.Completed)
            {
                this.StopTime = DateTime.UtcNow;
                this.Status = ProgressStatus.Completed;
                this.ProgressPercentage = 100;
                this.StatusMessage = "Completed";

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void OnNodeFail(IProgressNode progressNode, ProgressTask task, Exception error)
        {
            if (this.Status != ProgressStatus.Failed)
            {
                this.Status = ProgressStatus.Failed;
                this.ErrorMessage = error.Message;
                this.StopTime = DateTime.UtcNow;

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void OnNodeCancel(IProgressNode progressNode, ProgressTask task)
        {
            if (this.Status != ProgressStatus.Cancelled)
            {
                this.Status = ProgressStatus.Cancelled;
                this.StopTime = DateTime.UtcNow;
                this.StatusMessage = "Cancelled";

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        #endregion

        private void HandleChildStarted(IProgressNode progressNode, ProgressTask task)
        {
            var statusChanged = false;
            var childNode = this.Children.FirstOrDefault(x => x.Id == progressNode.Id);
            if (childNode != null)
            {
                if (this.Status == ProgressStatus.NotStarted)
                {
                    this.Status = ProgressStatus.InProgress;
                    statusChanged = true;
                }

                if (this.StartTime == null)
                {
                    this.StartTime = DateTime.UtcNow;
                    statusChanged = true;
                }

                if (childNode.Status == ProgressStatus.NotStarted)
                {
                    childNode.Status = ProgressStatus.InProgress;
                    statusChanged = true;
                }

                if (childNode.StartTime == null)
                {
                    childNode.StartTime = DateTime.UtcNow;
                    statusChanged = true;
                }
            }

            if (statusChanged)
            {
                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void HandleChildUpdateProgress(IProgressNode progressNode, ProgressTask task, string statusMessage, double value)
        {
            var childNode = this.Children.FirstOrDefault(x => x.Id == progressNode.Id);
            if (childNode != null && Math.Abs(childNode.ProgressPercentage - value) > Tolerance)
            {
                childNode.StatusMessage = statusMessage;
                childNode.ProgressPercentage = value;
                this.ProgressPercentage = this.Children.Sum(x => x.ProgressPercentage) / this.Children.Count;

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void HandleChildCompleted(IProgressNode progressNode, ProgressTask task)
        {
            var childNode = this.Children.FirstOrDefault(x => x.Id == progressNode.Id);
            if (childNode != null)
            {
                childNode.Status = ProgressStatus.Completed;
                childNode.StopTime = DateTime.UtcNow;
                childNode.StatusMessage = "Completed";
                childNode.ProgressPercentage = 100;
                this.ProgressPercentage = this.Children.Sum(x => x.ProgressPercentage) / this.Children.Count;

                if (this.Children.All(x => x.Status == ProgressStatus.Completed || x.Status == ProgressStatus.Failed || x.Status == ProgressStatus.Cancelled))
                {
                    this.Complete();
                }

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void HandleChildFailed(IProgressNode progressNode, ProgressTask task, Exception error)
        {
            var childNode = this.Children.FirstOrDefault(x => x.Id == progressNode.Id);
            if (childNode != null)
            {
                childNode.Status = ProgressStatus.Failed;
                childNode.StopTime = DateTime.UtcNow;
                childNode.ErrorMessage = error.Message;
                this.ProgressPercentage = this.Children.Sum(x => x.ProgressPercentage) / this.Children.Count;
                if (this.Status != ProgressStatus.Failed)
                {
                    this.Status = ProgressStatus.Failed;
                }

                if (this.Children.All(x => x.Status == ProgressStatus.Completed || x.Status == ProgressStatus.Failed || x.Status == ProgressStatus.Cancelled))
                {
                    this.Fail(error);
                }

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }

        private void HandleChildCancelled(IProgressNode progressNode, ProgressTask task)
        {
            var childNode = this.Children.FirstOrDefault(x => x.Id == progressNode.Id);
            if (childNode != null)
            {
                childNode.Status = ProgressStatus.Failed;
                childNode.StopTime = DateTime.UtcNow;
                childNode.StatusMessage = "Cancelled";
                this.ProgressPercentage = this.Children.Sum(x => x.ProgressPercentage) / this.Children.Count;
                if (this.Status != ProgressStatus.Cancelled)
                {
                    this.Status = ProgressStatus.Cancelled;
                }

                if (this.Children.All(x => x.Status == ProgressStatus.Completed || x.Status == ProgressStatus.Failed || x.Status == ProgressStatus.Cancelled))
                {
                    this.Cancel();
                }

                ProgressNodeRenderer.RefreshTaskStatus(this, this.ProgressTask);
            }
        }
    }
}
