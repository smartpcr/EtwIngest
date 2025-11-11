//-----------------------------------------------------------------------
// <copyright file="ETLFileReaderIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReaderIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ASEventReader.Models;
    using ASEventReader.Tools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for reading actual ETL and ZIP files.
    /// </summary>
    [TestClass]
    public class ETLFileReaderIntegrationTests
    {
        /// <summary>
        /// The directory containing ETL/ZIP files to test.
        /// </summary>
        private const string TestDataDirectory = "/mnt/X/icm/IL17";

        /// <summary>
        /// Output directory for scalable processing results.
        /// </summary>
        private const string OutputDirectory = "/mnt/X/icm/IL17/output";

        /// <summary>
        /// Tests reading all ETL and ZIP files from the test directory.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var etlFiles = Directory.GetFiles(TestDataDirectory, "*.etl", SearchOption.AllDirectories);
            var zipFiles = Directory.GetFiles(TestDataDirectory, "*.zip", SearchOption.AllDirectories);
            var allFiles = etlFiles.Concat(zipFiles).ToArray();

            if (allFiles.Length == 0)
            {
                Assert.Inconclusive($"No ETL or ZIP files found in: {TestDataDirectory}");
                return;
            }

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"ETL/ZIP File Analysis from: {TestDataDirectory}");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Total files found: {allFiles.Length} (ETL: {etlFiles.Length}, ZIP: {zipFiles.Length})");
            Console.WriteLine();

            var processor = new EventProcessor();
            var allStatistics = new List<FileStatistics>();

            // Act
            foreach (var file in allFiles)
            {
                try
                {
                    Console.WriteLine($"Processing: {Path.GetFileName(file)}");
                    Console.WriteLine($"  Path: {file}");
                    Console.WriteLine($"  Size: {FormatFileSize(new FileInfo(file).Length)}");

                    var events = processor.GetEvents(new[] { file });

                    if (events.Count == 0)
                    {
                        Console.WriteLine("  Status: No events found");
                        Console.WriteLine();
                        continue;
                    }

                    var fileStats = AnalyzeEvents(events, file);
                    allStatistics.Add(fileStats);

                    PrintFileStatistics(fileStats);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: {ex.Message}");
                    Console.WriteLine();
                }
            }

            // Print overall summary
            PrintOverallSummary(allStatistics);

            // Assert
            Assert.IsTrue(allStatistics.Count > 0, "No files were successfully processed");
        }

        /// <summary>
        /// Tests reading ETL files with specific provider filter.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ReadETLFiles_WithProviderFilter_ReturnsFilteredEvents()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var etlFiles = Directory.GetFiles(TestDataDirectory, "*.etl", SearchOption.TopDirectoryOnly).Take(1).ToArray();

            if (etlFiles.Length == 0)
            {
                Assert.Inconclusive($"No ETL files found in: {TestDataDirectory}");
                return;
            }

            var processor = new EventProcessor();
            var allEvents = processor.GetEvents(etlFiles);

            if (allEvents.Count == 0)
            {
                Assert.Inconclusive("No events found in the ETL file");
                return;
            }

            // Get the first provider name from events
            var firstProvider = allEvents.First().Properties.ContainsKey(PropertyNames.ProviderName)
                ? allEvents.First().Properties[PropertyNames.ProviderName]?.ToString()
                : null;

            if (string.IsNullOrEmpty(firstProvider))
            {
                Assert.Inconclusive("Could not determine provider name from events");
                return;
            }

            Console.WriteLine($"Testing with provider filter: {firstProvider}");

            // Act
            var filteredEvents = processor.GetEvents(etlFiles, providerName: firstProvider);

            // Assert
            Assert.IsTrue(filteredEvents.Count > 0, "Filtered events should contain at least one event");
            Console.WriteLine($"Total events: {allEvents.Count}");
            Console.WriteLine($"Filtered events: {filteredEvents.Count}");
            Console.WriteLine($"Filtered to: {(filteredEvents.Count * 100.0 / allEvents.Count):F2}% of total events");
        }

        /// <summary>
        /// Tests scalable processing of large volumes of ETL/ZIP files.
        /// Uses parallel processing, CSV output, and progress tracking.
        /// This test is designed to handle 40GB+ data and 2000+ files.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("LargeScale")]
        public void ProcessLargeScale_WithParallelProcessing_WritesToCSVAndAggregates()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var etlFiles = Directory.GetFiles(TestDataDirectory, "*.etl", SearchOption.AllDirectories);
            var zipFiles = Directory.GetFiles(TestDataDirectory, "*.zip", SearchOption.AllDirectories);
            var allFiles = etlFiles.Concat(zipFiles).ToArray();

            if (allFiles.Length == 0)
            {
                Assert.Inconclusive($"No ETL or ZIP files found in: {TestDataDirectory}");
                return;
            }

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Large-Scale ETL/ZIP File Processing");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Test Data Directory: {TestDataDirectory}");
            Console.WriteLine($"Output Directory: {OutputDirectory}");
            Console.WriteLine($"Total files found: {allFiles.Length:N0} (ETL: {etlFiles.Length}, ZIP: {zipFiles.Length})");
            Console.WriteLine($"Available CPU Cores: {Environment.ProcessorCount}");
            Console.WriteLine();

            // Create output directory
            if (Directory.Exists(OutputDirectory))
            {
                Console.WriteLine($"Cleaning existing output directory...");
                try
                {
                    Directory.Delete(OutputDirectory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not clean output directory: {ex.Message}");
                }
            }
            Directory.CreateDirectory(OutputDirectory);

            // Act
            var processor = new ScalableEventProcessor(OutputDirectory);
            var summary = processor.ProcessFiles(allFiles);

            // Print summary
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Processing Summary");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine($"Start Time: {summary.StartTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"End Time: {summary.EndTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Duration: {summary.Duration.TotalMinutes:F2} minutes");
            Console.WriteLine();
            Console.WriteLine($"Files:");
            Console.WriteLine($"  Total: {summary.TotalFiles:N0}");
            Console.WriteLine($"  Processed: {summary.ProcessedFiles:N0}");
            Console.WriteLine($"  Errors: {summary.Errors.Count}");
            Console.WriteLine();
            Console.WriteLine($"Events:");
            Console.WriteLine($"  Total Events: {summary.TotalEvents:N0}");
            Console.WriteLine($"  Unique Providers: {summary.TotalProviders}");
            Console.WriteLine($"  Events/Second: {(summary.Duration.TotalSeconds > 0 ? summary.TotalEvents / summary.Duration.TotalSeconds : 0):F2}");
            Console.WriteLine();
            Console.WriteLine($"Output:");
            Console.WriteLine($"  Directory: {summary.OutputDirectory}");
            Console.WriteLine($"  CSV Files: {Directory.GetFiles(summary.OutputDirectory, "events_batch_*.csv").Length}");
            Console.WriteLine($"  Batch Aggregates: {Directory.GetFiles(summary.OutputDirectory, "batch_aggregate_*.txt").Length}");
            Console.WriteLine($"  Final Aggregate: {Path.Combine(summary.OutputDirectory, "final_aggregate.txt")}");
            Console.WriteLine($"  Provider Summary: {Path.Combine(summary.OutputDirectory, "provider_summary.csv")}");
            Console.WriteLine($"  Event Type Summary: {Path.Combine(summary.OutputDirectory, "event_type_summary.csv")}");
            Console.WriteLine();

            if (summary.Errors.Count > 0)
            {
                Console.WriteLine($"Errors:");
                foreach (var error in summary.Errors.Take(10))
                {
                    Console.WriteLine($"  - {error}");
                }
                if (summary.Errors.Count > 10)
                {
                    Console.WriteLine($"  ... and {summary.Errors.Count - 10} more errors");
                }
                Console.WriteLine();
            }

            Console.WriteLine("=".PadRight(80, '='));

            // Print sample from final aggregate
            PrintFinalAggregate(summary.OutputDirectory);

            // Assert
            Assert.IsTrue(summary.ProcessedFiles > 0, "At least one file should be processed successfully");
            Assert.IsTrue(summary.TotalEvents > 0, "At least one event should be found");
            Assert.IsTrue(File.Exists(Path.Combine(summary.OutputDirectory, "final_aggregate.txt")), "Final aggregate file should exist");
            Assert.IsTrue(File.Exists(Path.Combine(summary.OutputDirectory, "provider_summary.csv")), "Provider summary CSV should exist");
            Assert.IsTrue(File.Exists(Path.Combine(summary.OutputDirectory, "event_type_summary.csv")), "Event type summary CSV should exist");
        }

        /// <summary>
        /// Prints a sample from the final aggregate file.
        /// </summary>
        /// <param name="outputDirectory">Output directory.</param>
        private void PrintFinalAggregate(string outputDirectory)
        {
            var finalAggregatePath = Path.Combine(outputDirectory, "final_aggregate.txt");
            if (!File.Exists(finalAggregatePath))
            {
                return;
            }

            Console.WriteLine("Final Aggregate Sample:");
            Console.WriteLine("-".PadRight(80, '-'));

            var lines = File.ReadAllLines(finalAggregatePath);
            foreach (var line in lines.Take(50))
            {
                Console.WriteLine(line);
            }

            if (lines.Length > 50)
            {
                Console.WriteLine($"... (truncated, {lines.Length - 50} more lines)");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Analyzes events from a file and returns statistics.
        /// </summary>
        /// <param name="events">List of events.</param>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>File statistics.</returns>
        private FileStatistics AnalyzeEvents(List<ASEventObject> events, string filePath)
        {
            var stats = new FileStatistics
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                TotalEvents = events.Count
            };

            // Group by provider name
            var providerGroups = events
                .Where(e => e.Properties.ContainsKey(PropertyNames.ProviderName))
                .GroupBy(e => e.Properties[PropertyNames.ProviderName]?.ToString() ?? "Unknown")
                .OrderByDescending(g => g.Count());

            foreach (var group in providerGroups)
            {
                var providerStat = new ProviderStatistics
                {
                    ProviderName = group.Key,
                    EventCount = group.Count()
                };

                // Group by event type within this provider
                var eventTypeGroups = group
                    .GroupBy(e => e.EventType ?? "Unknown")
                    .OrderByDescending(g => g.Count());

                foreach (var eventGroup in eventTypeGroups)
                {
                    providerStat.EventTypes.Add(new EventTypeStatistics
                    {
                        EventType = eventGroup.Key,
                        Count = eventGroup.Count()
                    });
                }

                stats.Providers.Add(providerStat);
            }

            return stats;
        }

        /// <summary>
        /// Prints statistics for a single file.
        /// </summary>
        /// <param name="stats">File statistics to print.</param>
        private void PrintFileStatistics(FileStatistics stats)
        {
            Console.WriteLine($"  Total Events: {stats.TotalEvents:N0}");
            Console.WriteLine($"  Unique Providers: {stats.Providers.Count}");
            Console.WriteLine();

            Console.WriteLine("  Provider Statistics:");
            Console.WriteLine("  " + "-".PadRight(76, '-'));

            foreach (var provider in stats.Providers.Take(10))
            {
                var percentage = (provider.EventCount * 100.0) / stats.TotalEvents;
                Console.WriteLine($"  • {provider.ProviderName}");
                Console.WriteLine($"    Events: {provider.EventCount:N0} ({percentage:F2}%)");
                Console.WriteLine($"    Event Types: {provider.EventTypes.Count}");

                // Show top 5 event types for this provider
                var topEventTypes = provider.EventTypes.Take(5);
                foreach (var eventType in topEventTypes)
                {
                    var eventPercentage = (eventType.Count * 100.0) / provider.EventCount;
                    Console.WriteLine($"      - {eventType.EventType}: {eventType.Count:N0} ({eventPercentage:F2}%)");
                }

                if (provider.EventTypes.Count > 5)
                {
                    Console.WriteLine($"      ... and {provider.EventTypes.Count - 5} more event types");
                }

                Console.WriteLine();
            }

            if (stats.Providers.Count > 10)
            {
                Console.WriteLine($"  ... and {stats.Providers.Count - 10} more providers");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Prints overall summary across all files.
        /// </summary>
        /// <param name="allStatistics">All file statistics.</param>
        private void PrintOverallSummary(List<FileStatistics> allStatistics)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("OVERALL SUMMARY");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var totalEvents = allStatistics.Sum(s => s.TotalEvents);
            var totalFiles = allStatistics.Count;

            Console.WriteLine($"Files Processed: {totalFiles}");
            Console.WriteLine($"Total Events: {totalEvents:N0}");
            Console.WriteLine($"Average Events per File: {(totalFiles > 0 ? totalEvents / totalFiles : 0):N0}");
            Console.WriteLine();

            // Aggregate providers across all files
            var allProviders = allStatistics
                .SelectMany(s => s.Providers)
                .GroupBy(p => p.ProviderName)
                .Select(g => new
                {
                    ProviderName = g.Key,
                    TotalEvents = g.Sum(p => p.EventCount),
                    FileCount = g.Count(),
                    UniqueEventTypes = g.SelectMany(p => p.EventTypes.Select(et => et.EventType)).Distinct().Count()
                })
                .OrderByDescending(p => p.TotalEvents)
                .ToList();

            Console.WriteLine($"Unique Providers Across All Files: {allProviders.Count}");
            Console.WriteLine();

            Console.WriteLine("Top 10 Providers by Event Count:");
            Console.WriteLine("-".PadRight(80, '-'));

            foreach (var provider in allProviders.Take(10))
            {
                var percentage = (provider.TotalEvents * 100.0) / totalEvents;
                Console.WriteLine($"• {provider.ProviderName}");
                Console.WriteLine($"  Total Events: {provider.TotalEvents:N0} ({percentage:F2}%)");
                Console.WriteLine($"  Files: {provider.FileCount}");
                Console.WriteLine($"  Unique Event Types: {provider.UniqueEventTypes}");
                Console.WriteLine();
            }

            Console.WriteLine("=".PadRight(80, '='));
        }

        /// <summary>
        /// Formats file size in human-readable format.
        /// </summary>
        /// <param name="bytes">Size in bytes.</param>
        /// <returns>Formatted string.</returns>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Statistics for a single file.
        /// </summary>
        private class FileStatistics
        {
            public string FilePath { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public int TotalEvents { get; set; }
            public List<ProviderStatistics> Providers { get; set; } = new List<ProviderStatistics>();
        }

        /// <summary>
        /// Statistics for a provider.
        /// </summary>
        private class ProviderStatistics
        {
            public string ProviderName { get; set; } = string.Empty;
            public int EventCount { get; set; }
            public List<EventTypeStatistics> EventTypes { get; set; } = new List<EventTypeStatistics>();
        }

        /// <summary>
        /// Statistics for an event type.
        /// </summary>
        private class EventTypeStatistics
        {
            public string EventType { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
