//-------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtlIterator
{
    using System.Collections.Concurrent;
    using EtwIngest.Libs;
    using Kusto.Data;
    using Kusto.Data.Common;
    using Kusto.Data.Net.Client;

    public class Program
    {
        static readonly string etlFolder = @"C:\etls\output";
        static readonly string csvOutputFolder = @"c:\csvs";
        static readonly string kustoClusterUri = "http://172.24.102.61:8080";
        static readonly string dbName = "Dell";

        public static void Main(string[] args)
        {

            // MoveEtlFiles(etlFolder);

            var etlFiles = Directory.GetFiles(etlFolder, "*.etl", SearchOption.TopDirectoryOnly);
            etlFiles = etlFiles.Where(f => new FileInfo(f).Length > 0).ToArray();
            Console.WriteLine($"total of {etlFiles.Length} etl files found");

            var batchSize = 1;
            var totalBatches = (int)Math.Ceiling((double)etlFiles.Length / batchSize);
            var startIndex = 0;

            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batchFiles = etlFiles.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                ProcessBatch(batchFiles, startIndex);
                startIndex += batchFiles.Length;
            }

            Console.WriteLine("Done!");
        }

        private static void ProcessBatch(string[] etlFiles, int startIndex)
        {
            var allEtwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
            var goodEtlFiles = new ConcurrentBag<string>();
            var failedEtlFiles = 0;
            var etlFilesprocessed = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount // or any specific number you want
            };

            Parallel.ForEach(etlFiles, parallelOptions, etlFile =>
            {
                var etl = new EtlFile(etlFile);
                var failedToParse = false;
                var localEtwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
                etl.Parse(localEtwEvents, ref failedToParse);
                if (failedToParse)
                {
                    Interlocked.Increment(ref failedEtlFiles);
                    Console.WriteLine($"Failed to parse ETL files");
                }
                else
                {
                    goodEtlFiles.Add(etlFile);
                    foreach (var key in localEtwEvents.Keys)
                    {
                        allEtwEvents.TryAdd(key, localEtwEvents[key]);
                    }
                }

                var currentProcessed = Interlocked.Increment(ref etlFilesprocessed);
                if (currentProcessed % 10 == 0)
                {
                    Console.WriteLine($"Processed {currentProcessed + startIndex} of {etlFiles.Length} etl files, found {allEtwEvents.Count} distinct events");
                }
            });

            // create kusto tables
            Console.WriteLine($"total of {allEtwEvents.Count + startIndex} etw events found, there are {failedEtlFiles} corrupted etl files");

            var allKustoTableNames = EnsureKustoTables(allEtwEvents, startIndex);

            // Process the ETL files to extract CSV files

            if (!Directory.Exists(csvOutputFolder))
            {
                Directory.CreateDirectory(csvOutputFolder);
            }
            var (successfulIngests, failedIngests) = ExtractCsvFiles(
                goodEtlFiles.ToList(),
                allEtwEvents,
                allKustoTableNames,
                csvOutputFolder,
                startIndex);

            Console.WriteLine($"Batch processing complete: {successfulIngests} successful ingests, {failedIngests} failed ingests");

            // Clear memory
            allEtwEvents.Clear();
            allEtwEvents = null;
            goodEtlFiles = null;
            GC.Collect();
        }

        private static (ICslAdminProvider adminClient, ICslQueryProvider queryClient) GetKustoClients()
        {
            var connectionStringBuilder = new KustoConnectionStringBuilder($"{kustoClusterUri}")
            {
                InitialCatalog = dbName
            };
            var adminClient = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            var queryClient = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);

            return (adminClient, queryClient);
        }

        private static HashSet<string> EnsureKustoTables(
            ConcurrentDictionary<(string providerName, string eventName), EtwEvent> allEtwEvents,
            int startIndex)
        {
            var (adminClient, _) = GetKustoClients();
            var allKustoTableNames = new HashSet<string>();
            var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            var processed = 0;

            Parallel.ForEach(allEtwEvents.Keys, parallelOpts, key =>
            {
                var (providerName, eventName) = key;
                var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                try
                {
                    if (!adminClient.IsTableExist(kustoTableName))
                    {
                        var eventFields = allEtwEvents[(providerName, eventName)].PayloadSchema;
                        // create table
                        var createTableCmd = KustoExtension.GenerateCreateTableCommand(kustoTableName, eventFields);
                        adminClient.ExecuteControlCommand(createTableCmd);
                        Console.WriteLine($"Table {kustoTableName} created");

                        // create ingestion mapping
                        var csvMappingCmd = KustoExtension.GenerateCsvIngestionMapping(kustoTableName, "CsvMapping", eventFields);
                        adminClient.ExecuteControlCommand(csvMappingCmd);
                        Console.WriteLine($"Ingestion mapping for {kustoTableName} created");
                    }

                    allKustoTableNames.Add(kustoTableName);
                }
                catch (Exception ex)
                {
                    WriteError($"Error: failed to create kusto table {kustoTableName}, error: {ex.Message}");
                }
                finally
                {
                    var currentProcessed = Interlocked.Increment(ref processed);
                    if (currentProcessed % 10 == 0)
                    {
                        Console.WriteLine($"Processed {currentProcessed + startIndex} of {allEtwEvents.Count} kusto tables");
                    }
                }
            });

            return allKustoTableNames;
        }

        private static (int successfulIngests, int failedIngests) ExtractCsvFiles(
            List<string> etlFiles,
            ConcurrentDictionary<(string providerName, string eventName), EtwEvent> allEtwEvents,
            HashSet<string> allKustoTableNames,
            string csvOutputFolder,
            int startIndex)
        {
            // generate csv files if not exist
            var totalCsvFileGenerated = 0;
            var totalCsvFileScanned = 0;
            foreach (var kvp in allEtwEvents)
            {
                totalCsvFileScanned++;
                var kustoTableName = $"ETL-{kvp.Key.providerName}.{kvp.Key.eventName.Replace("/", "")}";
                if (!allKustoTableNames.Contains(kustoTableName))
                {
                    continue;
                }
                var csvFileName = Path.Combine(csvOutputFolder, $"{kustoTableName}.csv");
                if (!File.Exists(csvFileName))
                {
                    var fieldNames = kvp.Value.PayloadSchema.Select(f => f.fieldName).ToList();
                    var columnHeader = string.Join(',', fieldNames) + Environment.NewLine;
                    File.WriteAllText(csvFileName, columnHeader);
                    totalCsvFileGenerated++;
                }

                if (totalCsvFileScanned % 10 == 0)
                {
                    Console.WriteLine($"scanned {totalCsvFileScanned} events, generated {totalCsvFileGenerated} csv files");
                }
            }

            var processed = 0;
            var successfulIngests = 0;
            var failedIngests = 0;

            var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            var allFileContents = new ConcurrentDictionary<(string providerName, string eventName), List<string>>();

            Parallel.ForEach(etlFiles, parallelOpts, etlFile =>
            {
                var etl = new EtlFile(etlFile);

                try
                {
                    var fileContents = etl.Process(allEtwEvents.ToDictionary(p => p.Key, p => p.Value));
                    foreach (var kvp in fileContents)
                    {
                        allFileContents.AddOrUpdate(kvp.Key, kvp.Value, (key, existingValue) =>
                        {
                            existingValue.AddRange(kvp.Value);
                            return existingValue;
                        });
                    }

                    Interlocked.Increment(ref successfulIngests);
                }
                catch (Exception ex)
                {
                    WriteError("Error: " + ex.Message);
                    Interlocked.Increment(ref failedIngests);
                }
                finally
                {
                    var currentProcessed = Interlocked.Increment(ref processed);
                    if (currentProcessed % 10 == 0)
                    {
                        Console.WriteLine($"Generating csv files from etl files, processed {currentProcessed + startIndex} of {etlFiles.Count} etl files");
                    }
                }
            });

            Parallel.ForEach(allFileContents, kvp =>
            {
                var csvFileName = Path.Combine(csvOutputFolder, $"{kvp.Key.providerName}.{kvp.Key.eventName.Replace("/", "")}.csv");
                if (File.Exists(csvFileName))
                {
                    File.AppendAllLines(csvFileName, kvp.Value);
                }
            });

            allFileContents.Clear();

            return (successfulIngests, failedIngests);
        }

        private static void MoveEtlFiles(string etlOutputFolder)
        {
            var rootFolder = @"C:\etls";
            if (Directory.Exists(etlOutputFolder))
            {
                Directory.Delete(etlOutputFolder, true);
            }
            Directory.CreateDirectory(etlOutputFolder);

            // Find all distinct .etl files
            var distinctEtlFiles = FindDistinctEtlFiles(rootFolder);

            // Output the distinct .etl files
            foreach (var filePath in distinctEtlFiles)
            {
                File.Move(filePath, Path.Combine(etlOutputFolder, Path.GetFileName(filePath)), true);
            }
        }

        static IEnumerable<string> FindDistinctEtlFiles(string rootFolderPath)
        {
            // Search for all .etl files in the root folder and its subfolders
            var allEtlFiles = Directory.EnumerateFiles(rootFolderPath, "*.etl", SearchOption.AllDirectories);

            // Use a HashSet to store distinct file names
            var uniqueFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var total = 0;

            foreach (var filePath in allEtlFiles)
            {
                var fileName = Path.GetFileName(filePath); // Get only the file name (without path)

                // Add to the HashSet to ensure uniqueness
                if (uniqueFileNames.Add(fileName))
                {
                    total++;
                    if (total % 10 ==0)
                    {
                        Console.WriteLine($"Found {total} distinct ETL files so far...");
                    }

                    yield return filePath; // Yield only if it is a new file name
                }
            }
        }

        private static void WriteError(string errorMessage)
        {
            if (Console.ForegroundColor != ConsoleColor.Red)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            Console.WriteLine(errorMessage);
            Console.ResetColor();
        }
    }
}
