//-------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtlIterator
{
    using System.Collections.Concurrent;
    using EtwIngest.Steps;
    using Kusto.Data;
    using Kusto.Data.Common;
    using Kusto.Data.Net.Client;

    public class Program
    {
        public static void Main(string[] args)
        {
            var etlFolder = @"C:\etls\output";
            var stagingFolder = @"c:\kustodata\staging";
            // MoveEtlFiles(etlFolder);

            var etlFiles = Directory.GetFiles(etlFolder, "*.etl", SearchOption.TopDirectoryOnly);
            etlFiles = etlFiles.Where(f => new FileInfo(f).Length > 0).ToArray();
            Console.WriteLine($"total of {etlFiles.Length} etl files found");

            var allEtwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
            var goodEtlFiles = new ConcurrentBag<string>();
            var failedEtlFiles = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount // or any specific number you want
            };
            Parallel.ForEach(etlFiles, parallelOptions, etlFile =>
            {
                var etl = new EtlFile(etlFile);
                bool failedToParse = false;
                var etwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
                etl.Parse(etwEvents, ref failedToParse);
                if (failedToParse)
                {
                    Interlocked.Increment(ref failedEtlFiles);
                    Console.WriteLine($"Failed to parse ETL files");
                }
                else
                {
                    goodEtlFiles.Add(etlFile);

                    foreach (var etwEvent in etwEvents)
                    {
                        allEtwEvents.TryAdd(etwEvent.Key, etwEvent.Value);
                    }
                }

                if (goodEtlFiles.Count % 10 == 0)
                {
                    Console.WriteLine($"Processed {goodEtlFiles.Count} of {etlFiles.Length} etl files, found {allEtwEvents.Count} distinct events");
                }
            });

            Console.WriteLine($"total of {allEtwEvents.Count} etw events found, there are {failedEtlFiles} corrupted etl files");

            var allKustoTableNames = EnsureKustoTables(allEtwEvents);

            Console.WriteLine("Done!");
        }

        private static (ICslAdminProvider adminClient, ICslQueryProvider queryClient) GetKustoClients()
        {
            var kustoClusterUri = "http://172.24.102.61:8080";
            var dbName = "Dell";
            var connectionStringBuilder = new KustoConnectionStringBuilder($"{kustoClusterUri}")
            {
                InitialCatalog = dbName
            };
            ICslAdminProvider adminClient = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            ICslQueryProvider queryClient = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);

            return (adminClient, queryClient);
        }

        private static HashSet<string> EnsureKustoTables(ConcurrentDictionary<(string providerName, string eventName), EtwEvent> allEtwEvents)
        {
            var (adminClient, _) = GetKustoClients();
            var allKustoTableNames = new HashSet<string>();
            var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
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
                    Console.WriteLine($"failed to create kusto table {kustoTableName}, error: {ex.Message}");
                }
            });

            return allKustoTableNames;
        }

        private static (int successfulIngests, int failedIngests) ExtractCsvFiles(List<string> etlFiles, HashSet<string> allKustoTableNames, string csvOutputFolder)
        {
            var processed = 0;
            var successfulIngests = 0;
            var failedIngests = 0;

            foreach (var etlFile in etlFiles)
            {
                var etl = new EtlFile(etlFile);
                var failedToParse = false;
                var etwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
                etl.Parse(etwEvents, ref failedToParse);
                if (failedToParse)
                {
                    Console.WriteLine($"Failed to parse ETL files");
                }

                foreach (var (providerName, eventName) in etwEvents.Keys)
                {
                    var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                    if (!allKustoTableNames.Contains(kustoTableName))
                    {
                        Console.WriteLine($"skip ingestion because kusto table {kustoTableName} doesn't exist");
                        continue;
                    }

                    var csvFileName = Path.Combine(csvOutputFolder, $"{kustoTableName}.csv");
                    if (!File.Exists(csvFileName))
                    {
                        var fieldNames = etwEvents[(providerName, eventName)].PayloadSchema.Select(f => f.fieldName)
                            .ToList();
                        var columnHeader = string.Join(',', fieldNames) + Environment.NewLine;
                        File.WriteAllText(csvFileName, columnHeader);
                    }
                }

                var writers = new Dictionary<(string providerName, string eventName), StreamWriter>();
                try
                {
                    foreach (var (providerName, eventName) in etwEvents.Keys)
                    {
                        var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                        if (!allKustoTableNames.Contains(kustoTableName))
                        {
                            Console.WriteLine($"skip ingestion because kusto table {kustoTableName} doesn't exist");
                            continue;
                        }

                        var csvFileName = Path.Combine(csvOutputFolder, $"{kustoTableName}.csv");
                        var writer = new StreamWriter(csvFileName, true);
                        writers.Add((providerName, eventName), writer);
                    }

                    etl.Process(etwEvents.ToDictionary(p => p.Key, p => p.Value), writers);
                    successfulIngests++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    failedIngests++;
                }
                finally
                {
                    foreach (var writer in writers.Values)
                    {
                        writer.Close();
                    }

                    processed++;
                    Console.WriteLine($"Finished processing {etlFile}, processed {processed} of {etlFiles.Count}");
                }
            }

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
            HashSet<string> uniqueFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = 0;

            foreach (var filePath in allEtlFiles)
            {
                string fileName = Path.GetFileName(filePath); // Get only the file name (without path)

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
    }
}
