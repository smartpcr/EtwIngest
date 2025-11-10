// -----------------------------------------------------------------------
// <copyright file="EvtxParserSteps.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#pragma warning disable CA1416
namespace EtwIngest.Steps
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using evtx;
    using FluentAssertions;
    using Kusto.Data.Common;
    using Libs;
    using Reqnroll;


    [Binding]
    public class EvtxParserSteps
    {
        private readonly ScenarioContext context;
        private readonly IReqnrollOutputHelper outputWriter;

        public EvtxParserSteps(ScenarioContext context, IReqnrollOutputHelper outputWriter)
        {
            this.context = context;
            this.outputWriter = outputWriter;
        }

        [Given("a evtx file at \"([^\"]+)\"")]
        public void GivenAEvtxFileAt(string evtxFile)
        {
            if (evtxFile.Contains("%HOME%", StringComparison.OrdinalIgnoreCase))
            {
                evtxFile = evtxFile.Replace("%HOME%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);
            }
            File.Exists(evtxFile).Should().BeTrue();
            this.context.Set(evtxFile, "evtxFile");
        }

        [When("I parse evtx file")]
        public void WhenIParseEvtxFile()
        {
            var evtxFile = this.context.Get<string>("evtxFile");
            var records = new List<EvtxRecord>();
            var total = 0;
            using (var fs = new FileStream(evtxFile, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var record in es.GetEventRecords())
                {
                    records.Add(new EvtxRecord()
                    {
                        TimeStamp = record.TimeCreated,
                        ProviderName = record.Provider,
                        LogName = record.Channel,
                        MachineName = record.Computer,
                        EventId = record.EventId,
                        Level = record.Level,
                        Keywords = record.Keywords,
                        ProcessId = record.ProcessId,
                        Description = record.MapDescription,
                    });
                }
            }

            this.context.Set(records, "evtxRecords");
        }

        [Then(@"I should get (\d+) evtx records")]
        public void ThenIShouldGetTheFollowingEvtxRecords(int expectedCount, Table table)
        {
            var evtxRecords = this.context.Get<List<EvtxRecord>>("evtxRecords");
            evtxRecords.Count.Should().Be(expectedCount);

            foreach (var row in table.Rows)
            {
                var eventId = int.Parse(row["EventId"]);
                var found = evtxRecords.Any(r => r.EventId == eventId);
                found.Should().BeTrue();
            }
        }

        [Then(@"kusto table ""([^""]+)"" should have the following columns")]
        public void ThenKustoTableWindowsEventsShouldHaveTheFollowingColumns(string kustoTableName, Table table)
        {
            var adminClient = this.context.Get<ICslAdminProvider>("adminClient");
            var showTableSchemaCmd = $".show table ['{kustoTableName}'] cslschema";
            using var reader = adminClient.ExecuteControlCommand(showTableSchemaCmd);
            var tableColumns = new Dictionary<string, string>();
            if (reader.Read())
            {
                var schemaResult = reader.GetString(1);
                var fields = schemaResult.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var field in fields)
                {
                    var fieldParts = field.Split(':');
                    tableColumns.Add(fieldParts[0].Trim(), fieldParts[1].Trim());
                }
            }
            reader.Close();

            tableColumns.Should().NotBeEmpty();
            foreach (var row in table.Rows)
            {
                var columnName = row["ColumnName"];
                var columnType = row["DataType"];
                tableColumns.ContainsKey(columnName).Should().BeTrue();
                tableColumns[columnName].Should().Be(columnType);
            }
        }
    }
}
