//-------------------------------------------------------------------------------
// <copyright file="EtwIngestSteps.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Steps
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Text;
    using System.Xml.Linq;
    using EtwIngest.Libs;
    using FluentAssertions;
    using Kusto.Data;
    using Kusto.Data.Common;
    using Kusto.Data.Net.Client;

    using Reqnroll;

    [Binding]
    public class EtwIngestSteps
    {
        private readonly ScenarioContext context;
        private readonly IReqnrollOutputHelper outputWriter;

        public EtwIngestSteps(ScenarioContext context, IReqnrollOutputHelper outputWriter)
        {
            this.context = context;
            this.outputWriter = outputWriter;
        }

        [Given("kusto cluster uri \"([^\"]+)\"")]
        public async Task GivenKustoClusterUri(string kustoClusterUri)
        {
            var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(1)
            };
            HttpResponseMessage response = await httpClient.GetAsync(kustoClusterUri);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            this.context.Set(kustoClusterUri, "kustoClusterUri");
            var connectionStringBuilder = new KustoConnectionStringBuilder($"{kustoClusterUri}")
            {
                InitialCatalog = "NetDefaultDB"
            };
            ICslAdminProvider adminClient = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            this.context.Set(adminClient, "adminClient");
            ICslQueryProvider queryClient = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
            this.context.Set(queryClient, "queryClient");
        }

        [Given("kusto database name \"([^\"]+)\"")]
        public void GivenKustoDatabaseName(string dbName)
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var showDatabasesCommand = ".show databases";
            using var result = adminClient.ExecuteControlCommand(showDatabasesCommand);
            var dbExists = false;
            while (result.Read())
            {
                if (result.GetString(0) == dbName)
                {
                    dbExists = true;
                    break;
                }
            }

            if (!dbExists)
            {
                var createDatabaseCommand = @$".create database {dbName} persist (
      @""/kustodata/dbs/{dbName}/md"",
      @""/kustodata/dbs/{dbName}/data""
    )";
                adminClient.ExecuteControlCommand(createDatabaseCommand);
                this.outputWriter.WriteLine($"Database {dbName} created");
            }

            var kustoClusterUri = this.context.Get<string>("kustoClusterUri");
            var connectionStringBuilder = new KustoConnectionStringBuilder($"{kustoClusterUri}")
            {
                InitialCatalog = dbName
            };
            adminClient = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            this.context.Set(adminClient, "adminClient");
            ICslQueryProvider queryClient = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
            this.context.Set(queryClient, "queryClient");

            this.context.Set(dbName, "dbName");
        }

        [Given("kustainer volume mount from \"([^\"]+)\" to \"([^\"]+)\"")]
        public void GivenKustainerVolumeMountFromTo(string hostPath, string containerPath)
        {
            this.context.Set(hostPath, "hostPath");
            this.context.Set(containerPath, "containerPath");
        }

        [Given("etl file \"([^\"]+)\"")]
        public void GivenEtlFile(string etlFile)
        {
            File.Exists(etlFile).Should().BeTrue();
            this.outputWriter.WriteLine($"etl file: {etlFile}");
            this.context.Set(etlFile, "etlFile");
        }

        [When("I parse etl file")]
        public void WhenIParseEtlFile()
        {
            var etlFile = this.context.Get<string>("etlFile");
            var etl = new EtlFile(etlFile);
            bool failedToParse = false;
            var etwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
            etl.Parse(etwEvents, ref failedToParse);
            if (failedToParse)
            {
                Assert.Fail($"Failed to parse ETL files");
            }

            this.context.Set(etwEvents, "etwEvents");
        }

        [Then("the result have the following events")]
        public void ThenTheResultHaveTheFollowingEvents(Table table)
        {
            var etwEvents = this.context.Get<ConcurrentDictionary<(string providerName, string eventName), EtwEvent>>("etwEvents");
            foreach (var row in table.Rows)
            {
                var providerName = row["ProviderName"];
                var eventName = row["EventName"];
                etwEvents.ContainsKey((providerName, eventName)).Should().BeTrue();
            }
        }

        [When("I infer schema for provider \"([^\"]+)\" and event \"([^\"]+)\"")]
        public void WhenIInferSchemaForProviderAndEvent(string providerName, string eventName)
        {
            var etlFile = this.context.Get<string>("etlFile");
            var etl = new EtlFile(etlFile);
            bool failedToParse = false;
            var etwEvents = new ConcurrentDictionary<(string providerName, string eventName), EtwEvent>();
            etl.Parse(etwEvents, ref failedToParse);
            if (failedToParse)
            {
                Assert.Fail($"Failed to parse ETL files");
            }

            etwEvents.ContainsKey((providerName, eventName)).Should().BeTrue();
            var eventFields = etwEvents[(providerName, eventName)].PayloadSchema;
            this.context.Set(providerName, "providerName");
            this.context.Set(eventName, "eventName");
            var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
            this.context.Set(kustoTableName, "kustoTableName");
            this.context.Set(eventFields, "eventFields");
        }

        [Then("the result have the following schema")]
        public void ThenTheResultHaveTheFollowingSchema(Table table)
        {
            var eventFields = this.context.Get<List<(string fieldName, Type fieldType)>>("eventFields");
            foreach (var row in table.Rows)
            {
                var expectedFieldName = row["ColumnName"];
                var expectedFieldType = row["DataType"];
                eventFields.Any(x => x.fieldName == expectedFieldName).Should().BeTrue();
                var clrType = eventFields.First(x => x.fieldName == expectedFieldName).fieldType;
                var cslType = clrType.ToKustoColumnType();
                cslType.Should().Be(expectedFieldType);
            }
        }

        [Then("Kusto table name should be \"([^\"]+)\"")]
        public void ThenKustoTableNameShouldBe(string expectedTableName)
        {
            var kustoTableName = this.context.Get<string>("kustoTableName");
            kustoTableName.Should().Be(expectedTableName);
        }

        [When("I ensure kusto table")]
        public void WhenIEnsureKustoTable()
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var kustoTableName = this.context.Get<string>("kustoTableName");
            if (adminClient.IsTableExist(kustoTableName)) return;

            var eventFields = this.context.Get<List<(string fieldName, Type fieldType)>>("eventFields");

            // create table
            var createTableCmd = KustoExtension.GenerateCreateTableCommand(kustoTableName, eventFields);
            adminClient.ExecuteControlCommand(createTableCmd);
            this.outputWriter.WriteLine($"Table {kustoTableName} created");

            // create ingestion mapping
            var csvMappingCmd = KustoExtension.GenerateCsvIngestionMapping(kustoTableName, "CsvMapping", eventFields);
            adminClient.ExecuteControlCommand(csvMappingCmd);
            this.outputWriter.WriteLine($"Ingestion mapping for {kustoTableName} created");
        }

        [Then("the table should be created with the following schema")]
        public void ThenTheTableShouldBeCreatedWithTheFollowingSchema(Table table)
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var kustoTableName = this.context.Get<string>("kustoTableName");
            var showTableSchemaCmd = $".show table ['{kustoTableName}'] cslschema";
            using var result = adminClient.ExecuteControlCommand(showTableSchemaCmd);
            var tableColumns = new Dictionary<string, string>();
            if (result.Read())
            {
                var schemaResult = result.GetString(1);
                var fields = schemaResult.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var field in fields)
                {
                    var fieldParts = field.Split(':');
                    tableColumns.Add(fieldParts[0].Trim(), fieldParts[1].Trim());
                }
            }
            result.Close();

            tableColumns.Should().HaveCount(table.RowCount);
            foreach (var row in table.Rows)
            {
                var expectedFieldName = row["ColumnName"];
                var expectedFieldType = row["DataType"];
                tableColumns.ContainsKey(expectedFieldName).Should().BeTrue();
                tableColumns[expectedFieldName].Should().Be(expectedFieldType);
            }
        }

        [When("I extract etl file to target folder \"([^\"]+)\"")]
        public void WhenIExtractEtlFileToTargetFolder(string ingestFolder)
        {
            if (Directory.Exists(ingestFolder))
            {
                Directory.Delete(ingestFolder, true);
            }
            Directory.CreateDirectory(ingestFolder);

            this.context.Set(ingestFolder, "ingestFolder");
            var etwEvents = this.context.Get<ConcurrentDictionary<(string providerName, string eventName), EtwEvent>>("etwEvents");

            foreach (var (providerName, eventName) in etwEvents.Keys)
            {
                var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                var csvFileName = Path.Combine(ingestFolder, $"{kustoTableName}.csv");
                if (!File.Exists(csvFileName))
                {
                    var fieldNames = etwEvents[(providerName, eventName)].PayloadSchema.Select(f => f.fieldName)
                        .ToList();
                    var columnHeader = string.Join(',', fieldNames) + Environment.NewLine;
                    File.WriteAllText(csvFileName, columnHeader);
                }
            }

            var writers = new ConcurrentDictionary<(string providerName, string eventName), StreamWriter>();

            try
            {
                foreach (var (providerName, eventName) in etwEvents.Keys)
                {
                    var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                    var csvFileName = Path.Combine(ingestFolder, $"{kustoTableName}.csv");
                    var writer = new StreamWriter(csvFileName, true);
                    writers.TryAdd((providerName, eventName), writer);
                }

                var etlFile = new EtlFile(this.context.Get<string>("etlFile"));
                var fileLines = etlFile.Process(etwEvents.ToDictionary(p => p.Key, p => p.Value));
                foreach (var kvp in fileLines)
                {
                    var writer = writers[(kvp.Key.providerName, kvp.Key.eventName)];
                    foreach (var line in kvp.Value)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                this.outputWriter.WriteLine(ex.Message);
                Assert.Fail(ex.Message);
            }
            finally
            {
                foreach (var writer in writers.Values)
                {
                    writer.Close();
                }
            }
        }

        [Then("I should generate the following csv files")]
        public void ThenIShouldGenerateTheFollowingCsvFiles(Table table)
        {
            var ingestFolder = this.context.Get<string>("ingestFolder");
            var csvFiles = Directory.GetFiles(ingestFolder, "*.csv");
            csvFiles.Should().HaveCount(table.RowCount);

            foreach (var row in table.Rows)
            {
                var expectedFileName = row["FileName"];
                csvFiles.Any(x => Path.GetFileName(x) == expectedFileName).Should().BeTrue();
            }
        }

        [When("ensure kusto tables for all events are created")]
        public async Task WhenEnsureKustoTablesForAllEventsAreCreated()
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var queryClient = this.context.Get<ICslQueryProvider>("queryClient");
            var etwEvents = this.context.Get<ConcurrentDictionary<(string providerName, string eventName), EtwEvent>>("etwEvents");
            var dbName = this.context.Get<string>("dbName");
            var tableRecordCount = new Dictionary<string, long>();

            foreach (var (providerName, eventName) in etwEvents.Keys)
            {
                var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                if (!adminClient.IsTableExist(kustoTableName))
                {
                    var eventFields = etwEvents[(providerName, eventName)].PayloadSchema;
                    // create table
                    var createTableCmd = KustoExtension.GenerateCreateTableCommand(kustoTableName, eventFields);
                    adminClient.ExecuteControlCommand(createTableCmd);
                    this.outputWriter.WriteLine($"Table {kustoTableName} created");

                    // create ingestion mapping
                    var csvMappingCmd = KustoExtension.GenerateCsvIngestionMapping(kustoTableName, "CsvMapping", eventFields);
                    adminClient.ExecuteControlCommand(csvMappingCmd);
                    this.outputWriter.WriteLine($"Ingestion mapping for {kustoTableName} created");
                }

                var recordCountCmd = $"['{kustoTableName}'] | count";
                using var reader = await queryClient.ExecuteQueryAsync(dbName, recordCountCmd, new ClientRequestProperties());
                if (reader.Read())
                {
                    var recordCount = reader.GetInt64(0);
                    tableRecordCount.Add(kustoTableName, recordCount);
                }
            }

            this.context.Set(tableRecordCount, "tableRecordCount");
        }

        [Then("the following kusto tables should be created")]
        public void ThenTheFollowingKustoTablesShouldBeCreated(Table table)
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var tableNames = new List<string>();
            var showDatabasesCommand = ".show tables";
            using var result = adminClient.ExecuteControlCommand(showDatabasesCommand);
            while (result.Read())
            {
                tableNames.Add(result.GetString(0));
            }

            foreach (var row in table.Rows)
            {
                var tableName = row["TableName"];
                tableNames.Should().Contain(tableName);
            }
        }

        [When("I ingest etl files into kusto")]
        public void WhenIIngestEtlFilesIntoKusto()
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var etwEvents = this.context.Get<ConcurrentDictionary<(string providerName, string eventName), EtwEvent>>("etwEvents");
            var ingestFolder = this.context.Get<string>("ingestFolder");
            var volumeBindingHostPath = this.context.Get<string>("hostPath");
            var volumeBindingContainerPath = this.context.Get<string>("containerPath");

            foreach (var (providerName, eventName) in etwEvents.Keys)
            {
                var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                var csvFileName = Path.Combine(ingestFolder, $"{kustoTableName}.csv");
                File.Exists(csvFileName).Should().BeTrue();
                var csvFileContainerPath = csvFileName.Replace(volumeBindingHostPath, volumeBindingContainerPath);
                csvFileContainerPath = csvFileContainerPath.Replace(@"\\", "/");
                csvFileContainerPath = csvFileContainerPath.Replace(@"\", "/");

                var ingestCommand = $".ingest into table ['{kustoTableName}'] (\"{csvFileContainerPath}\") with (format='csv', ingestionMappingReference='CsvMapping', ignoreFirstRecord=true)";
                adminClient.ExecuteControlCommand(ingestCommand);
            }
        }

        [Then("the following kusto tables should have the following records")]
        public async Task ThenTheFollowingKustoTablesShouldHaveTheFollowingRecords(Table table)
        {
            var etwEvents = this.context.Get<ConcurrentDictionary<(string providerName, string eventName), EtwEvent>>("etwEvents");
            var queryClient = this.context.Get<ICslQueryProvider>("queryClient");
            var tableRecordCount = this.context.Get<Dictionary<string, long>>("tableRecordCount");
            var dbName = this.context.Get<string>("dbName");

            foreach (var (providerName, eventName) in etwEvents.Keys)
            {
                var kustoTableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
                var recordCountCmd = $"['{kustoTableName}'] | count";
                using var reader = await queryClient.ExecuteQueryAsync(dbName, recordCountCmd, this.GetClientRequestProps());
                if (reader.Read())
                {
                    var recordCount = reader.GetInt64(0);
                    if (!tableRecordCount.TryAdd(kustoTableName, recordCount))
                    {
                        tableRecordCount[kustoTableName] = recordCount - tableRecordCount[kustoTableName];
                    }
                }
            }

            foreach (var row in table.Rows)
            {
                var tableName = row["TableName"];
                tableRecordCount.Should().ContainKey(tableName);
                var expectedRecordCount = int.Parse(row["RecordCount"]);
                tableRecordCount[tableName].Should().Be(expectedRecordCount);
            }
        }

        [Then("total of {int} kusto tables should be created, including")]
        public void ThenTotalOfKustoTablesShouldBeCreatedIncluding(int tableCount, Table table)
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var tableNames = new List<string>();
            var showDatabasesCommand = ".show tables";
            using var result = adminClient.ExecuteControlCommand(showDatabasesCommand);
            while (result.Read())
            {
                tableNames.Add(result.GetString(0));
            }
            tableNames.Count.Should().Be(tableCount);

            foreach (var row in table.Rows)
            {
                var tableName = row["TableName"];
                tableNames.Should().Contain(tableName);
            }
        }

        [Then("I should generate the {int} csv files that include the following")]
        public void ThenIShouldGenerateTheCsvFilesThatIncludeTheFollowing(int csvFileCount, Table table)
        {
            var ingestFolder = this.context.Get<string>("ingestFolder");
            var csvFiles = Directory.GetFiles(ingestFolder, "*.csv");
            csvFiles.Should().HaveCount(csvFileCount);

            foreach (var row in table.Rows)
            {
                var expectedFileName = row["FileName"];
                csvFiles.Any(x => Path.GetFileName(x) == expectedFileName).Should().BeTrue();
            }
        }

        private ClientRequestProperties GetClientRequestProps(TimeSpan timeout = default)
        {
            var requestProps = new ClientRequestProperties { ClientRequestId = Guid.NewGuid().ToString() };
            if (timeout != default)
            {
                requestProps.SetOption(ClientRequestProperties.OptionServerTimeout, timeout);
            }

            return requestProps;
        }
    }
}
