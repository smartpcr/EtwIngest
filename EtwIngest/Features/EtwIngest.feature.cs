﻿// ------------------------------------------------------------------------------
//  <auto-generated>
//      This code was generated by Reqnroll (https://www.reqnroll.net/).
//      Reqnroll Version:1.0.0.0
//      Reqnroll Generator Version:1.0.0.0
// 
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// ------------------------------------------------------------------------------
#region Designer generated code
#pragma warning disable
namespace EtwIngest.Features
{
    using Reqnroll;
    using System;
    using System.Linq;
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Reqnroll", "1.0.0.0")]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute()]
    public partial class EtwIngestFeature
    {
        
        private static Reqnroll.ITestRunner testRunner;
        
        private Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _testContext;
        
        private static string[] featureTags = ((string[])(null));
        
#line 1 "EtwIngest.feature"
#line hidden
        
        public virtual Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext
        {
            get
            {
                return this._testContext;
            }
            set
            {
                this._testContext = value;
            }
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.ClassInitializeAttribute()]
        public static async System.Threading.Tasks.Task FeatureSetupAsync(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext testContext)
        {
            testRunner = Reqnroll.TestRunnerManager.GetTestRunnerForAssembly(null, System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
            Reqnroll.FeatureInfo featureInfo = new Reqnroll.FeatureInfo(new System.Globalization.CultureInfo("en-US"), "Features", "EtwIngest", "    As a user,\r\n    I want to be able to extract ETW events from a file,\r\n    and" +
                    " infer its kusto table schema based on provider and event,\r\n    and ingest the e" +
                    "vents into the kusto table.", ProgrammingLanguage.CSharp, featureTags);
            await testRunner.OnFeatureStartAsync(featureInfo);
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.ClassCleanupAttribute()]
        public static async System.Threading.Tasks.Task FeatureTearDownAsync()
        {
            await testRunner.OnFeatureEndAsync();
            testRunner = null;
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute()]
        public async System.Threading.Tasks.Task TestInitializeAsync()
        {
            if (((testRunner.FeatureContext != null) 
                        && (testRunner.FeatureContext.FeatureInfo.Title != "EtwIngest")))
            {
                await global::EtwIngest.Features.EtwIngestFeature.FeatureSetupAsync(null);
            }
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute()]
        public async System.Threading.Tasks.Task TestTearDownAsync()
        {
            await testRunner.OnScenarioEndAsync();
        }
        
        public void ScenarioInitialize(Reqnroll.ScenarioInfo scenarioInfo)
        {
            testRunner.OnScenarioInitialize(scenarioInfo);
            testRunner.ScenarioContext.ScenarioContainer.RegisterInstanceAs<Microsoft.VisualStudio.TestTools.UnitTesting.TestContext>(_testContext);
        }
        
        public async System.Threading.Tasks.Task ScenarioStartAsync()
        {
            await testRunner.OnScenarioStartAsync();
        }
        
        public async System.Threading.Tasks.Task ScenarioCleanupAsync()
        {
            await testRunner.CollectScenarioErrorsAsync();
        }
        
        public virtual async System.Threading.Tasks.Task FeatureBackgroundAsync()
        {
#line 7
 #line hidden
#line 8
  await testRunner.GivenAsync("kusto cluster uri \"http://172.24.102.61:8080\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 9
  await testRunner.AndAsync("kusto database name \"Dell\"", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
#line 10
  await testRunner.AndAsync("kustainer volume mount from \"c:\\\\kustodata\" to \"/kustodata\"", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("extract etl file")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "EtwIngest")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute("parser")]
        public async System.Threading.Tasks.Task ExtractEtlFile()
        {
            string[] tagsOfScenario = new string[] {
                    "parser"};
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("extract etl file", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 13
    this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 7
 await this.FeatureBackgroundAsync();
#line hidden
#line 14
        await testRunner.GivenAsync("etl file \"C:\\\\Users\\\\xiaodoli\\\\Downloads\\\\SAC14-S1-N01_AzureStack.Compute.HostPlu" +
                        "ginWatchDog.2024-09-23.1.etl\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 15
        await testRunner.WhenAsync("I parse etl file", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table1 = new Reqnroll.Table(new string[] {
                            "ProviderName",
                            "EventName"});
                table1.AddRow(new string[] {
                            "MSNT_SystemTrace",
                            "EventTrace/PartitionInfoExtensionV2"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "ManifestData"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "StartWatchDog/Start"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "ConfigFilesFound"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "StartWatchDog/Stop"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "ReadConfigFromStore"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "EnsureProcessStarted/Start"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "ProcessStarted"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "EnsureProcessStarted/Stop"});
                table1.AddRow(new string[] {
                            "Microsoft-AzureStack-Compute-HostPluginWatchDog",
                            "FoundProcessAlreadyRunning"});
#line 16
        await testRunner.ThenAsync("the result have the following events", ((string)(null)), table1, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("infer kusto schema")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "EtwIngest")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute("schema")]
        public async System.Threading.Tasks.Task InferKustoSchema()
        {
            string[] tagsOfScenario = new string[] {
                    "schema"};
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("infer kusto schema", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 30
    this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 7
 await this.FeatureBackgroundAsync();
#line hidden
#line 31
  await testRunner.GivenAsync("etl file \"C:\\\\Users\\\\xiaodoli\\\\Downloads\\\\SAC14-S1-N01_AzureStack.Compute.HostPlu" +
                        "ginWatchDog.2024-09-23.1.etl\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 32
  await testRunner.WhenAsync("I infer schema for provider \"Microsoft-AzureStack-Compute-HostPluginWatchDog\" and" +
                        " event \"EnsureProcessStarted/Stop\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table2 = new Reqnroll.Table(new string[] {
                            "ColumnName",
                            "DataType",
                            "Nullable"});
                table2.AddRow(new string[] {
                            "TimeStamp",
                            "datetime",
                            "false"});
                table2.AddRow(new string[] {
                            "ProcessID",
                            "int",
                            "false"});
                table2.AddRow(new string[] {
                            "ProcessName",
                            "string",
                            "false"});
                table2.AddRow(new string[] {
                            "Level",
                            "int",
                            "false"});
                table2.AddRow(new string[] {
                            "Opcode",
                            "int",
                            "false"});
                table2.AddRow(new string[] {
                            "OpcodeName",
                            "string",
                            "false"});
                table2.AddRow(new string[] {
                            "correlationVector",
                            "string",
                            "true"});
                table2.AddRow(new string[] {
                            "name",
                            "string",
                            "true"});
                table2.AddRow(new string[] {
                            "executablePath",
                            "string",
                            "true"});
                table2.AddRow(new string[] {
                            "arguments",
                            "string",
                            "true"});
                table2.AddRow(new string[] {
                            "workingDirectory",
                            "string",
                            "true"});
                table2.AddRow(new string[] {
                            "duration",
                            "long",
                            "true"});
                table2.AddRow(new string[] {
                            "exception",
                            "string",
                            "true"});
#line 33
  await testRunner.ThenAsync("the result have the following schema", ((string)(null)), table2, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("ensure kusto table")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "EtwIngest")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute("schema")]
        public async System.Threading.Tasks.Task EnsureKustoTable()
        {
            string[] tagsOfScenario = new string[] {
                    "schema"};
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("ensure kusto table", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 50
 this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 7
 await this.FeatureBackgroundAsync();
#line hidden
#line 51
  await testRunner.GivenAsync("etl file \"C:\\\\Users\\\\xiaodoli\\\\Downloads\\\\SAC14-S1-N01_AzureStack.Compute.HostPlu" +
                        "ginWatchDog.2024-09-23.1.etl\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 52
  await testRunner.WhenAsync("I infer schema for provider \"Microsoft-AzureStack-Compute-HostPluginWatchDog\" and" +
                        " event \"EnsureProcessStarted/Stop\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 53
  await testRunner.ThenAsync("Kusto table name should be \"ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.E" +
                        "nsureProcessStartedStop\"", ((string)(null)), ((Reqnroll.Table)(null)), "Then ");
#line hidden
#line 54
  await testRunner.WhenAsync("I ensure kusto table", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table3 = new Reqnroll.Table(new string[] {
                            "ColumnName",
                            "DataType",
                            "Nullable"});
                table3.AddRow(new string[] {
                            "TimeStamp",
                            "datetime",
                            "false"});
                table3.AddRow(new string[] {
                            "ProcessID",
                            "int",
                            "false"});
                table3.AddRow(new string[] {
                            "ProcessName",
                            "string",
                            "false"});
                table3.AddRow(new string[] {
                            "Level",
                            "int",
                            "false"});
                table3.AddRow(new string[] {
                            "Opcode",
                            "int",
                            "false"});
                table3.AddRow(new string[] {
                            "OpcodeName",
                            "string",
                            "false"});
                table3.AddRow(new string[] {
                            "correlationVector",
                            "string",
                            "true"});
                table3.AddRow(new string[] {
                            "name",
                            "string",
                            "true"});
                table3.AddRow(new string[] {
                            "executablePath",
                            "string",
                            "true"});
                table3.AddRow(new string[] {
                            "arguments",
                            "string",
                            "true"});
                table3.AddRow(new string[] {
                            "workingDirectory",
                            "string",
                            "true"});
                table3.AddRow(new string[] {
                            "duration",
                            "long",
                            "true"});
                table3.AddRow(new string[] {
                            "exception",
                            "string",
                            "true"});
#line 55
  await testRunner.ThenAsync("the table should be created with the following schema", ((string)(null)), table3, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("extract etl file to csv file")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "EtwIngest")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute("extract")]
        public async System.Threading.Tasks.Task ExtractEtlFileToCsvFile()
        {
            string[] tagsOfScenario = new string[] {
                    "extract"};
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("extract etl file to csv file", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 72
 this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 7
 await this.FeatureBackgroundAsync();
#line hidden
#line 73
  await testRunner.GivenAsync("etl file \"C:\\\\Users\\\\xiaodoli\\\\Downloads\\\\SAC14-S1-N01_AzureStack.Compute.HostPlu" +
                        "ginWatchDog.2024-09-23.1.etl\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 74
  await testRunner.WhenAsync("I parse etl file", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 75
  await testRunner.AndAsync("I extract etl file to target folder \"c:\\\\kustodata\\\\staging\"", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
                Reqnroll.Table table4 = new Reqnroll.Table(new string[] {
                            "FileName"});
                table4.AddRow(new string[] {
                            "ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart.csv" +
                                ""});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop.csv"});
                table4.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning.cs" +
                                "v"});
#line 76
  await testRunner.ThenAsync("I should generate the following csv files", ((string)(null)), table4, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("ingest etl files into kusto")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "EtwIngest")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute("ingest")]
        public async System.Threading.Tasks.Task IngestEtlFilesIntoKusto()
        {
            string[] tagsOfScenario = new string[] {
                    "ingest"};
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("ingest etl files into kusto", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 90
 this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 7
 await this.FeatureBackgroundAsync();
#line hidden
#line 91
  await testRunner.GivenAsync("etl file \"C:\\\\Users\\\\xiaodoli\\\\Downloads\\\\SAC14-S1-N01_AzureStack.Compute.HostPlu" +
                        "ginWatchDog.2024-09-23.1.etl\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 92
  await testRunner.WhenAsync("I parse etl file", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 93
  await testRunner.AndAsync("ensure kusto tables for all events are created", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
                Reqnroll.Table table5 = new Reqnroll.Table(new string[] {
                            "TableName"});
                table5.AddRow(new string[] {
                            "ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop"});
                table5.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning"});
#line 94
  await testRunner.ThenAsync("the following kusto tables should be created", ((string)(null)), table5, "Then ");
#line hidden
#line 106
  await testRunner.WhenAsync("I extract etl file to target folder \"c:\\\\kustodata\\\\staging\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table6 = new Reqnroll.Table(new string[] {
                            "FileName"});
                table6.AddRow(new string[] {
                            "ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart.csv" +
                                ""});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop.csv"});
                table6.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning.cs" +
                                "v"});
#line 107
  await testRunner.ThenAsync("I should generate the following csv files", ((string)(null)), table6, "Then ");
#line hidden
#line 119
  await testRunner.WhenAsync("I ingest etl files into kusto", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table7 = new Reqnroll.Table(new string[] {
                            "TableName",
                            "RecordCount"});
                table7.AddRow(new string[] {
                            "ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2",
                            "1"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ManifestData",
                            "1"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStart",
                            "1"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ConfigFilesFound",
                            "584"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.StartWatchDogStop",
                            "1"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ReadConfigFromStore",
                            "1138"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStart",
                            "1138"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.ProcessStarted",
                            "2"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop",
                            "1138"});
                table7.AddRow(new string[] {
                            "ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.FoundProcessAlreadyRunning",
                            "1136"});
#line 120
  await testRunner.ThenAsync("the following kusto tables should have the following records", ((string)(null)), table7, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
    }
}
#pragma warning restore
#endregion
