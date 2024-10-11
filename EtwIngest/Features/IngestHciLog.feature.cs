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
    public partial class IngestHCIEtwAndEvtxEventsFeature
    {
        
        private static Reqnroll.ITestRunner testRunner;
        
        private Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _testContext;
        
        private static string[] featureTags = ((string[])(null));
        
#line 1 "IngestHciLog.feature"
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
            Reqnroll.FeatureInfo featureInfo = new Reqnroll.FeatureInfo(new System.Globalization.CultureInfo("en-US"), "Features", "ingest HCI etw and evtx events", null, ProgrammingLanguage.CSharp, featureTags);
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
                        && (testRunner.FeatureContext.FeatureInfo.Title != "ingest HCI etw and evtx events")))
            {
                await global::EtwIngest.Features.IngestHCIEtwAndEvtxEventsFeature.FeatureSetupAsync(null);
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
#line 2
  #line hidden
#line 3
    await testRunner.GivenAsync("kusto cluster uri \"http://172.20.102.248:8080\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 4
    await testRunner.AndAsync("kusto database name \"hci\"", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
#line 5
    await testRunner.AndAsync("kustainer volume mount from \"E:\\\\kustodata\" to \"/kustodata\"", ((string)(null)), ((Reqnroll.Table)(null)), "And ");
#line hidden
        }
        
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute()]
        [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("end to end ingestion of HCI logs")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute("FeatureTitle", "ingest HCI etw and evtx events")]
        public async System.Threading.Tasks.Task EndToEndIngestionOfHCILogs()
        {
            string[] tagsOfScenario = ((string[])(null));
            System.Collections.Specialized.OrderedDictionary argumentsOfScenario = new System.Collections.Specialized.OrderedDictionary();
            Reqnroll.ScenarioInfo scenarioInfo = new Reqnroll.ScenarioInfo("end to end ingestion of HCI logs", null, tagsOfScenario, argumentsOfScenario, featureTags);
#line 7
  this.ScenarioInitialize(scenarioInfo);
#line hidden
            if ((TagHelper.ContainsIgnoreTag(tagsOfScenario) || TagHelper.ContainsIgnoreTag(featureTags)))
            {
                testRunner.SkipScenario();
            }
            else
            {
                await this.ScenarioStartAsync();
#line 2
  await this.FeatureBackgroundAsync();
#line hidden
#line 8
    await testRunner.GivenAsync("A zip file at \"%HOME%\\\\Downloads\\\\hci.zip\"", ((string)(null)), ((Reqnroll.Table)(null)), "Given ");
#line hidden
#line 9
    await testRunner.WhenAsync("I extract \"etl\" files from zip file to folder \"%HOME%\\\\Downloads\\\\hci\\\\etw\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table11 = new Reqnroll.Table(new string[] {
                            "FileName"});
                table11.AddRow(new string[] {
                            "V-HOST1_AzureStack.Update.Admin.2024-10-09.1.etl"});
#line 10
    await testRunner.ThenAsync("I should see the following \"etl\" files in folder \"%HOME%\\\\Downloads\\\\hci\\\\etw\"", ((string)(null)), table11, "Then ");
#line hidden
#line 13
    await testRunner.WhenAsync("I extract \"evtx\" files from zip file to folder \"%HOME%\\\\Downloads\\\\hci\\\\evtx\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table12 = new Reqnroll.Table(new string[] {
                            "FileName"});
                table12.AddRow(new string[] {
                            "Event_Microsoft.AzureStack.LCMController.EventSource-Admin.evtx"});
#line 14
    await testRunner.ThenAsync("I should see the following \"evtx\" files in folder \"%HOME%\\\\Downloads\\\\hci\\\\evtx\"", ((string)(null)), table12, "Then ");
#line hidden
#line 17
    await testRunner.WhenAsync("I parse etl files in folder \"%HOME%\\\\Downloads\\\\hci\\\\etw\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 18
    await testRunner.ThenAsync("I should find 121 distinct events in etl files", ((string)(null)), ((Reqnroll.Table)(null)), "Then ");
#line hidden
#line 19
    await testRunner.WhenAsync("I create tables based on etl event schemas", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table13 = new Reqnroll.Table(new string[] {
                            "TableName"});
                table13.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.HealthCheckResultDirectoryIsEmptyOrDoesNotExis" +
                                "t"});
                table13.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.ResolverGetAll"});
                table13.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.StopService"});
#line 20
    await testRunner.ThenAsync("I should see following etl kusto tables", ((string)(null)), table13, "Then ");
#line hidden
#line 25
    await testRunner.WhenAsync("I extract etl files in folder \"%HOME%\\\\Downloads\\\\hci\\\\etw\" to csv files in folde" +
                        "r \"%HOME%\\\\Downloads\\\\hci\\\\csv\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table14 = new Reqnroll.Table(new string[] {
                            "FileName"});
                table14.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.ResolverGetAll.csv"});
                table14.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.StartService.csv"});
                table14.AddRow(new string[] {
                            "ETL-Microsoft-URP-InfraEventSource.StopService.csv"});
#line 26
    await testRunner.ThenAsync("I should see following csv files in folder \"%HOME%\\\\Downloads\\\\hci\\\\csv\"", ((string)(null)), table14, "Then ");
#line hidden
#line 31
    await testRunner.WhenAsync("I parse evtx files in folder \"%HOME%\\\\Downloads\\\\hci\\\\evtx\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 32
    await testRunner.ThenAsync("I should find 4475 distinct records in evtx files", ((string)(null)), ((Reqnroll.Table)(null)), "Then ");
#line hidden
#line 33
    await testRunner.WhenAsync("I create table based on evtx record schema", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table15 = new Reqnroll.Table(new string[] {
                            "TableName"});
                table15.AddRow(new string[] {
                            "WindowsEvents"});
#line 34
    await testRunner.ThenAsync("I should see following evtx kusto table", ((string)(null)), table15, "Then ");
#line hidden
#line 37
    await testRunner.WhenAsync("I extract evtx records to csv files in folder \"%HOME%\\\\Downloads\\\\hci\\\\csv\"", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
#line 38
    await testRunner.ThenAsync("I should see following csv file \"WindowsEvents.csv\" in folder \"%HOME%\\\\Downloads\\" +
                        "\\hci\\\\csv\"", ((string)(null)), ((Reqnroll.Table)(null)), "Then ");
#line hidden
#line 39
    await testRunner.WhenAsync("I ingest csv files in folder \"%HOME%\\\\Downloads\\\\hci\\\\csv\" to kusto", ((string)(null)), ((Reqnroll.Table)(null)), "When ");
#line hidden
                Reqnroll.Table table16 = new Reqnroll.Table(new string[] {
                            "TableName",
                            "RecordCount"});
#line 40
    await testRunner.ThenAsync("the following kusto tables should have added records with expected counts", ((string)(null)), table16, "Then ");
#line hidden
            }
            await this.ScenarioCleanupAsync();
        }
    }
}
#pragma warning restore
#endregion
