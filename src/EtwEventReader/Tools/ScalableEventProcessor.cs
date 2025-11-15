//-----------------------------------------------------------------------
// <copyright file="ScalableEventProcessor.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EtwEventReader.Models;

    /// <summary>
    /// Scalable event processor that handles large volumes of ETL/ZIP files
    /// by writing to CSV files and processing in parallel.
    /// </summary>
    public class ScalableEventProcessor
    {
        private readonly string outputDirectory;
        private readonly int maxDegreeOfParallelism;
        private readonly object progressLock = new object();
        private int totalFilesProcessed = 0;
        private int totalFiles = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScalableEventProcessor"/> class.
        /// </summary>
        /// <param name="outputDirectory">Directory where CSV files will be written.</param>
        /// <param name="maxDegreeOfParallelism">Maximum number of parallel tasks (default: CPU core count).</param>
        public ScalableEventProcessor(string outputDirectory, int maxDegreeOfParallelism = -1)
        {
            this.outputDirectory = outputDirectory;
            this.maxDegreeOfParallelism = maxDegreeOfParallelism > 0
                ? maxDegreeOfParallelism
                : Environment.ProcessorCount;

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        /// <summary>
        /// Processes multiple ETL/ZIP files in parallel and writes results to CSV.
        /// </summary>
        /// <param name="filePaths">Array of file paths to process.</param>
        /// <param name="activityId">Optional activity ID filter.</param>
        /// <param name="providerName">Optional provider name filter.</param>
        /// <param name="eventName">Optional event name filter.</param>
        /// <returns>Processing summary.</returns>
        public ProcessingSummary ProcessFiles(
            string[] filePaths,
            Guid activityId = default,
            string? providerName = null,
            string? eventName = null)
        {
            var summary = new ProcessingSummary
            {
                StartTime = DateTime.Now,
                TotalFiles = filePaths.Length,
                OutputDirectory = this.outputDirectory
            };

            this.totalFiles = filePaths.Length;
            this.totalFilesProcessed = 0;

            Console.WriteLine($"Scalable Event Processor");
            Console.WriteLine($"========================");
            Console.WriteLine($"Total Files: {filePaths.Length:N0}");
            Console.WriteLine($"Max Parallel Tasks: {this.maxDegreeOfParallelism}");
            Console.WriteLine($"Output Directory: {this.outputDirectory}");
            Console.WriteLine();

            // Create batch aggregator
            var batchAggregator = new BatchAggregator(this.outputDirectory);

            // Process files in parallel
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = this.maxDegreeOfParallelism
            };

            var batchResults = new ConcurrentBag<BatchResult>();

            try
            {
                Parallel.ForEach(filePaths, options, (filePath, state, index) =>
                {
                    try
                    {
                        var result = this.ProcessFile(
                            filePath,
                            (int)index,
                            activityId,
                            providerName,
                            eventName);

                        batchResults.Add(result);

                        // Write batch aggregate
                        batchAggregator.WriteBatchAggregate(result);

                        // Update progress
                        Interlocked.Increment(ref this.totalFilesProcessed);
                        this.UpdateProgress();
                    }
                    catch (Exception ex)
                    {
                        summary.Errors.Add($"{filePath}: {ex.Message}");
                        Interlocked.Increment(ref this.totalFilesProcessed);
                        this.UpdateProgress();
                    }
                });

                Console.WriteLine();
                Console.WriteLine("Processing complete. Generating final aggregates...");

                // Generate final aggregates
                batchAggregator.GenerateFinalAggregates(batchResults.ToList());

                summary.EndTime = DateTime.Now;
                summary.ProcessedFiles = batchResults.Count;
                summary.TotalEvents = batchResults.Sum(r => r.EventCount);
                summary.TotalProviders = batchResults.SelectMany(r => r.Providers.Keys).Distinct().Count();

                return summary;
            }
            catch (Exception ex)
            {
                summary.Errors.Add($"Fatal error: {ex.Message}");
                summary.EndTime = DateTime.Now;
                return summary;
            }
        }

        /// <summary>
        /// Processes a single file and writes events to CSV.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="fileIndex">Index of the file being processed.</param>
        /// <param name="activityId">Optional activity ID filter.</param>
        /// <param name="providerName">Optional provider name filter.</param>
        /// <param name="eventName">Optional event name filter.</param>
        /// <returns>Batch result for this file.</returns>
        private BatchResult ProcessFile(
            string filePath,
            int fileIndex,
            Guid activityId,
            string? providerName,
            string? eventName)
        {
            var result = new BatchResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                BatchIndex = fileIndex,
                StartTime = DateTime.Now
            };

            // Create CSV file for this batch
            var csvFileName = $"events_batch_{fileIndex:D6}_{Path.GetFileNameWithoutExtension(filePath)}.csv";
            var csvFilePath = Path.Combine(this.outputDirectory, csvFileName);
            result.CsvFilePath = csvFilePath;

            using (var writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                // Write CSV header
                writer.WriteLine("Timestamp,ProviderName,EventType,ActivityId,ProcessID,ThreadID,FormattedMessage,Success,DurationMs");

                var processor = new EventProcessor();
                var events = processor.GetEvents(
                    new[] { filePath },
                    activityId,
                    providerName,
                    eventName);

                result.EventCount = events.Count;

                // Write events to CSV and collect statistics
                foreach (var evt in events)
                {
                    // Write to CSV
                    WriteCsvLine(writer, evt);

                    // Collect statistics
                    var provider = evt.Properties.ContainsKey(PropertyNames.ProviderName)
                        ? evt.Properties[PropertyNames.ProviderName]?.ToString() ?? "Unknown"
                        : "Unknown";

                    var eventType = evt.EventType ?? "Unknown";

                    if (!result.Providers.ContainsKey(provider))
                    {
                        result.Providers[provider] = new Dictionary<string, int>();
                    }

                    if (!result.Providers[provider].ContainsKey(eventType))
                    {
                        result.Providers[provider][eventType] = 0;
                    }

                    result.Providers[provider][eventType]++;
                }
            }

            result.EndTime = DateTime.Now;
            result.ProcessingTimeMs = (result.EndTime - result.StartTime).TotalMilliseconds;

            return result;
        }

        /// <summary>
        /// Writes a CSV line for an event.
        /// </summary>
        /// <param name="writer">CSV writer.</param>
        /// <param name="evt">Event object.</param>
        private static void WriteCsvLine(StreamWriter writer, EtwEventObject evt)
        {
            var timestamp = evt.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var providerName = evt.Properties.ContainsKey(PropertyNames.ProviderName)
                ? EscapeCsvField(evt.Properties[PropertyNames.ProviderName]?.ToString())
                : "";
            var eventType = EscapeCsvField(evt.EventType);
            var activityId = evt.Properties.ContainsKey(PropertyNames.ActivityId)
                ? evt.Properties[PropertyNames.ActivityId]?.ToString()
                : "";
            var processId = evt.Properties.ContainsKey(PropertyNames.ProcessID)
                ? evt.Properties[PropertyNames.ProcessID]?.ToString()
                : "";
            var threadId = evt.Properties.ContainsKey(PropertyNames.ThreadID)
                ? evt.Properties[PropertyNames.ThreadID]?.ToString()
                : "";
            var message = evt.Properties.ContainsKey(PropertyNames.FormattedMessage)
                ? EscapeCsvField(evt.Properties[PropertyNames.FormattedMessage]?.ToString())
                : "";
            var success = evt.Success;
            var durationMs = evt.DurationMs?.ToString() ?? "";

            writer.WriteLine($"{timestamp},{providerName},{eventType},{activityId},{processId},{threadId},{message},{success},{durationMs}");
        }

        /// <summary>
        /// Escapes a CSV field.
        /// </summary>
        /// <param name="field">Field to escape.</param>
        /// <returns>Escaped field.</returns>
        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        /// <summary>
        /// Updates the progress bar.
        /// </summary>
        private void UpdateProgress()
        {
            lock (this.progressLock)
            {
                var processed = this.totalFilesProcessed;
                var total = this.totalFiles;
                var percentage = (double)processed / total * 100;

                // Draw progress bar
                var barWidth = 50;
                var filledWidth = (int)(barWidth * percentage / 100);
                var emptyWidth = barWidth - filledWidth;

                var bar = new string('█', filledWidth) + new string('░', emptyWidth);

                Console.Write($"\r[{bar}] {percentage:F1}% ({processed:N0}/{total:N0} files)");
            }
        }
    }

    /// <summary>
    /// Batch result for a single file.
    /// </summary>
    public class BatchResult
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int BatchIndex { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double ProcessingTimeMs { get; set; }
        public int EventCount { get; set; }
        public string CsvFilePath { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, int>> Providers { get; set; } = new Dictionary<string, Dictionary<string, int>>();
    }

    /// <summary>
    /// Processing summary for all files.
    /// </summary>
    public class ProcessingSummary
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public long TotalEvents { get; set; }
        public int TotalProviders { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();

        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Aggregates batch results and writes summary files.
    /// </summary>
    public class BatchAggregator
    {
        private readonly string outputDirectory;
        private readonly object writeLock = new object();

        public BatchAggregator(string outputDirectory)
        {
            this.outputDirectory = outputDirectory;
        }

        /// <summary>
        /// Writes a batch aggregate file.
        /// </summary>
        /// <param name="result">Batch result.</param>
        public void WriteBatchAggregate(BatchResult result)
        {
            var aggregateFileName = $"batch_aggregate_{result.BatchIndex:D6}.txt";
            var aggregateFilePath = Path.Combine(this.outputDirectory, aggregateFileName);

            lock (this.writeLock)
            {
                using (var writer = new StreamWriter(aggregateFilePath, false, Encoding.UTF8))
                {
                    writer.WriteLine($"Batch Aggregate Report");
                    writer.WriteLine($"======================");
                    writer.WriteLine($"File: {result.FileName}");
                    writer.WriteLine($"Path: {result.FilePath}");
                    writer.WriteLine($"Batch Index: {result.BatchIndex}");
                    writer.WriteLine($"Processing Time: {result.ProcessingTimeMs:F2} ms");
                    writer.WriteLine($"Total Events: {result.EventCount:N0}");
                    writer.WriteLine($"CSV Output: {Path.GetFileName(result.CsvFilePath)}");
                    writer.WriteLine();

                    writer.WriteLine($"Provider Statistics:");
                    writer.WriteLine($"-------------------");

                    foreach (var provider in result.Providers.OrderByDescending(p => p.Value.Values.Sum()))
                    {
                        var totalEvents = provider.Value.Values.Sum();
                        writer.WriteLine($"\n{provider.Key}:");
                        writer.WriteLine($"  Total Events: {totalEvents:N0}");
                        writer.WriteLine($"  Event Types:");

                        foreach (var eventType in provider.Value.OrderByDescending(e => e.Value).Take(10))
                        {
                            var percentage = (double)eventType.Value / totalEvents * 100;
                            writer.WriteLine($"    - {eventType.Key}: {eventType.Value:N0} ({percentage:F2}%)");
                        }

                        if (provider.Value.Count > 10)
                        {
                            writer.WriteLine($"    ... and {provider.Value.Count - 10} more event types");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates final aggregate across all batches.
        /// </summary>
        /// <param name="results">All batch results.</param>
        public void GenerateFinalAggregates(List<BatchResult> results)
        {
            var finalAggregatePath = Path.Combine(this.outputDirectory, "final_aggregate.txt");
            var providerSummaryPath = Path.Combine(this.outputDirectory, "provider_summary.csv");
            var eventTypeSummaryPath = Path.Combine(this.outputDirectory, "event_type_summary.csv");

            // Generate final aggregate report
            using (var writer = new StreamWriter(finalAggregatePath, false, Encoding.UTF8))
            {
                writer.WriteLine($"Final Aggregate Report");
                writer.WriteLine($"=====================");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                writer.WriteLine($"Overall Statistics:");
                writer.WriteLine($"------------------");
                writer.WriteLine($"Total Files Processed: {results.Count:N0}");
                writer.WriteLine($"Total Events: {results.Sum(r => r.EventCount):N0}");
                writer.WriteLine($"Average Events per File: {(results.Count > 0 ? results.Average(r => r.EventCount) : 0):N0}");
                writer.WriteLine($"Total Processing Time: {results.Sum(r => r.ProcessingTimeMs):F2} ms");
                writer.WriteLine($"Average Processing Time per File: {(results.Count > 0 ? results.Average(r => r.ProcessingTimeMs) : 0):F2} ms");
                writer.WriteLine();

                // Aggregate providers across all files
                var allProviders = new Dictionary<string, Dictionary<string, long>>();

                foreach (var result in results)
                {
                    foreach (var provider in result.Providers)
                    {
                        if (!allProviders.ContainsKey(provider.Key))
                        {
                            allProviders[provider.Key] = new Dictionary<string, long>();
                        }

                        foreach (var eventType in provider.Value)
                        {
                            if (!allProviders[provider.Key].ContainsKey(eventType.Key))
                            {
                                allProviders[provider.Key][eventType.Key] = 0;
                            }

                            allProviders[provider.Key][eventType.Key] += eventType.Value;
                        }
                    }
                }

                writer.WriteLine($"Provider Summary:");
                writer.WriteLine($"----------------");
                writer.WriteLine($"Unique Providers: {allProviders.Count}");
                writer.WriteLine();

                var totalEvents = results.Sum(r => r.EventCount);

                foreach (var provider in allProviders.OrderByDescending(p => p.Value.Values.Sum()).Take(20))
                {
                    var providerEvents = provider.Value.Values.Sum();
                    var percentage = (double)providerEvents / totalEvents * 100;

                    writer.WriteLine($"{provider.Key}:");
                    writer.WriteLine($"  Events: {providerEvents:N0} ({percentage:F2}%)");
                    writer.WriteLine($"  Unique Event Types: {provider.Value.Count}");
                    writer.WriteLine($"  Files: {results.Count(r => r.Providers.ContainsKey(provider.Key))}");
                    writer.WriteLine();
                }

                if (allProviders.Count > 20)
                {
                    writer.WriteLine($"... and {allProviders.Count - 20} more providers");
                }
            }

            // Generate provider summary CSV
            using (var writer = new StreamWriter(providerSummaryPath, false, Encoding.UTF8))
            {
                writer.WriteLine("ProviderName,TotalEvents,UniqueEventTypes,FileCount,Percentage");

                var totalEvents = results.Sum(r => r.EventCount);
                var allProviders = new Dictionary<string, Dictionary<string, long>>();

                foreach (var result in results)
                {
                    foreach (var provider in result.Providers)
                    {
                        if (!allProviders.ContainsKey(provider.Key))
                        {
                            allProviders[provider.Key] = new Dictionary<string, long>();
                        }

                        foreach (var eventType in provider.Value)
                        {
                            if (!allProviders[provider.Key].ContainsKey(eventType.Key))
                            {
                                allProviders[provider.Key][eventType.Key] = 0;
                            }

                            allProviders[provider.Key][eventType.Key] += eventType.Value;
                        }
                    }
                }

                foreach (var provider in allProviders.OrderByDescending(p => p.Value.Values.Sum()))
                {
                    var providerEvents = provider.Value.Values.Sum();
                    var percentage = (double)providerEvents / totalEvents * 100;
                    var fileCount = results.Count(r => r.Providers.ContainsKey(provider.Key));

                    writer.WriteLine($"\"{provider.Key}\",{providerEvents},{provider.Value.Count},{fileCount},{percentage:F2}");
                }
            }

            // Generate event type summary CSV
            using (var writer = new StreamWriter(eventTypeSummaryPath, false, Encoding.UTF8))
            {
                writer.WriteLine("ProviderName,EventType,Count,Percentage");

                var totalEvents = results.Sum(r => r.EventCount);
                var allProviders = new Dictionary<string, Dictionary<string, long>>();

                foreach (var result in results)
                {
                    foreach (var provider in result.Providers)
                    {
                        if (!allProviders.ContainsKey(provider.Key))
                        {
                            allProviders[provider.Key] = new Dictionary<string, long>();
                        }

                        foreach (var eventType in provider.Value)
                        {
                            if (!allProviders[provider.Key].ContainsKey(eventType.Key))
                            {
                                allProviders[provider.Key][eventType.Key] = 0;
                            }

                            allProviders[provider.Key][eventType.Key] += eventType.Value;
                        }
                    }
                }

                foreach (var provider in allProviders.OrderByDescending(p => p.Value.Values.Sum()))
                {
                    foreach (var eventType in provider.Value.OrderByDescending(e => e.Value))
                    {
                        var percentage = (double)eventType.Value / totalEvents * 100;
                        writer.WriteLine($"\"{provider.Key}\",\"{eventType.Key}\",{eventType.Value},{percentage:F2}");
                    }
                }
            }
        }
    }
}
