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
    using System.Diagnostics.Eventing.Reader;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.XPath;
    using FluentAssertions;
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
            File.Exists(evtxFile).Should().BeTrue();
            this.context.Set(evtxFile, "evtxFile");
        }

        [When("I parse evtx file")]
        public void WhenIParseEvtxFile()
        {
            var evtxFile = this.context.Get<string>("evtxFile");
            var logReader = new EventLogReader(evtxFile, PathType.FilePath);
            while (logReader.ReadEvent() is { } record)
            {
                // record.ProviderName + record.LogName + record.MachineName + record.Id
            }
        }


    }
}
