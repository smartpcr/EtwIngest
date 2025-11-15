//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EtwEventReader.Models;
    using EtwEventReader.Tools;

    /// <summary>
    /// Main program entry point.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Main entry point for the ASEventReader console application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        static void Main(string[] args)
        {
            Console.WriteLine("ASEventReader - ETL Event Log Reader");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            try
            {
                var options = ParseArguments(args);

                if (options.ShowHelp)
                {
                    PrintUsage();
                    return;
                }

                if (options.Paths.Count == 0)
                {
                    Console.WriteLine("Error: No input paths specified.");
                    PrintUsage();
                    return;
                }

                var processor = new EventProcessor();
                var events = processor.GetEvents(
                    options.Paths.ToArray(),
                    options.ActivityId,
                    options.ProviderName,
                    options.EventName);

                Console.WriteLine($"\nFound {events.Count} events.");
                Console.WriteLine();

                // Display events
                if (options.OutputFormat == OutputFormat.Detailed)
                {
                    foreach (var evt in events)
                    {
                        Console.WriteLine(evt.ToString());
                    }
                }
                else if (options.OutputFormat == OutputFormat.Summary)
                {
                    Console.WriteLine("Summary of Events:");
                    Console.WriteLine("------------------");
                    foreach (var evt in events)
                    {
                        var success = evt.Success ? "SUCCESS" : "FAIL";
                        var duration = evt.DurationMs.HasValue ? $" ({evt.DurationMs}ms)" : "";
                        Console.WriteLine($"[{evt.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] {success} - {evt.EventType}{duration}");
                    }
                }
                else if (options.OutputFormat == OutputFormat.Tree)
                {
                    Console.WriteLine("Event Tree:");
                    Console.WriteLine("-----------");
                    // Build hierarchy
                    var rootEvents = BuildEventHierarchy(events);
                    foreach (var evt in rootEvents)
                    {
                        PrintEventTree(evt);
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"Total events processed: {events.Count}");

                // Display error summary if any events failed
                var failedEvents = events.Where(e => !e.Success).ToList();
                if (failedEvents.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Failed events: {failedEvents.Count}");
                    if (options.ShowErrors)
                    {
                        Console.WriteLine("\nError Details:");
                        Console.WriteLine("--------------");
                        foreach (var evt in failedEvents)
                        {
                            Console.WriteLine($"[{evt.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] {evt.EventType}");
                            if (!string.IsNullOrEmpty(evt.ErrorMessage))
                            {
                                Console.WriteLine($"  Error: {evt.ErrorMessage}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Prints the command line usage information.
        /// </summary>
        static void PrintUsage()
        {
            Console.WriteLine("Usage: ASEventReader [options] <path1> [path2] ...");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help              Show this help message");
            Console.WriteLine("  -a, --activity <guid>   Filter by Activity ID");
            Console.WriteLine("  -p, --provider <name>   Filter by Provider Name");
            Console.WriteLine("  -e, --event <name>      Filter by Event Name");
            Console.WriteLine("  -f, --format <format>   Output format: detailed, summary, tree (default: summary)");
            Console.WriteLine("  --errors                Show detailed error information");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <path>                  Path to ETL file(s) or directory containing ETL files");
            Console.WriteLine("                          Supports wildcards (e.g., *.etl)");
            Console.WriteLine("                          Supports zip files containing ETL files");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ASEventReader trace.etl");
            Console.WriteLine("  ASEventReader -p \"Microsoft-Windows-Kernel-Process\" trace.etl");
            Console.WriteLine("  ASEventReader -f detailed *.etl");
            Console.WriteLine("  ASEventReader --errors -f tree C:\\Logs\\");
        }

        /// <summary>
        /// Parses command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Parsed options.</returns>
        static Options ParseArguments(string[] args)
        {
            var options = new Options();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;

                    case "-a":
                    case "--activity":
                        if (i + 1 < args.Length)
                        {
                            if (Guid.TryParse(args[++i], out var activityId))
                            {
                                options.ActivityId = activityId;
                            }
                            else
                            {
                                Console.WriteLine($"Warning: Invalid activity ID format: {args[i]}");
                            }
                        }
                        break;

                    case "-p":
                    case "--provider":
                        if (i + 1 < args.Length)
                        {
                            options.ProviderName = args[++i];
                        }
                        break;

                    case "-e":
                    case "--event":
                        if (i + 1 < args.Length)
                        {
                            options.EventName = args[++i];
                        }
                        break;

                    case "-f":
                    case "--format":
                        if (i + 1 < args.Length)
                        {
                            var format = args[++i].ToLower();
                            options.OutputFormat = format switch
                            {
                                "detailed" => OutputFormat.Detailed,
                                "summary" => OutputFormat.Summary,
                                "tree" => OutputFormat.Tree,
                                _ => OutputFormat.Summary
                            };
                        }
                        break;

                    case "--errors":
                        options.ShowErrors = true;
                        break;

                    default:
                        if (!args[i].StartsWith("-"))
                        {
                            options.Paths.Add(args[i]);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Unknown option: {args[i]}");
                        }
                        break;
                }
            }

            return options;
        }

        /// <summary>
        /// Builds event hierarchy by organizing events by their parent-child relationships.
        /// </summary>
        /// <param name="events">List of events.</param>
        /// <returns>List of root events.</returns>
        static List<EtwEventObject> BuildEventHierarchy(List<EtwEventObject> events)
        {
            // For simplicity, we'll just return events at hierarchy level 0
            // A more sophisticated implementation would build the full tree
            return events.Where(e => e.HierarchyLevel == 0).ToList();
        }

        /// <summary>
        /// Prints an event tree recursively.
        /// </summary>
        /// <param name="evt">Event to print.</param>
        static void PrintEventTree(EtwEventObject evt)
        {
            var indent = new string(' ', evt.HierarchyLevel * 2);
            var success = evt.Success ? "✓" : "✗";
            var duration = evt.DurationMs.HasValue ? $" ({evt.DurationMs}ms)" : "";
            Console.WriteLine($"{indent}{success} {evt.EventType}{duration}");
        }

        /// <summary>
        /// Options for the command line application.
        /// </summary>
        class Options
        {
            public bool ShowHelp { get; set; }
            public Guid ActivityId { get; set; }
            public string? ProviderName { get; set; }
            public string? EventName { get; set; }
            public OutputFormat OutputFormat { get; set; } = OutputFormat.Summary;
            public bool ShowErrors { get; set; }
            public List<string> Paths { get; set; } = new List<string>();
        }

        /// <summary>
        /// Output format options.
        /// </summary>
        enum OutputFormat
        {
            Detailed,
            Summary,
            Tree
        }
    }
}
