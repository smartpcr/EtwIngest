// -----------------------------------------------------------------------
// <copyright file="NodeProgress.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ProgressTree
{
    public struct NodeProgress
    {
        public ProgressStatus Status;
        public double DurationMs;
        public double ProgressPercent;
        public DateTime? StartTime;
        public DateTime? FinishTime;
        public string StatusMessage;
        public string ErrorMessage;

        public NodeProgress(
            ProgressStatus status,
            double durationMs,
            double progressPercent,
            DateTime? startTime,
            DateTime? finishTime,
            string statusMessage,
            string errorMessage)
        {
            this.Status = status;
            this.DurationMs = durationMs;
            this.ProgressPercent = progressPercent;
            this.StartTime = startTime;
            this.FinishTime = finishTime;
            this.StatusMessage = statusMessage;
            this.ErrorMessage = errorMessage;
        }
    }
}