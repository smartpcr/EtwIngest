//-------------------------------------------------------------------------------
// <copyright file="ParseResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Models
{
    /// <summary>
    /// Result of a parse operation
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the parse operation succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets error message if parse failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception if one occurred
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets the number of events discovered/processed
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the elapsed parsing time
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Creates a successful parse result
        /// </summary>
        public static ParseResult Successful(int eventCount, long fileSizeBytes, TimeSpan elapsed) => new()
        {
            Success = true,
            EventCount = eventCount,
            FileSizeBytes = fileSizeBytes,
            ElapsedTime = elapsed
        };

        /// <summary>
        /// Creates a failed parse result
        /// </summary>
        public static ParseResult Failed(string errorMessage, Exception? exception = null) => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}
