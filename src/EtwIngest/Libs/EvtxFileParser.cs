// -----------------------------------------------------------------------
// <copyright file="EvtxFileParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#pragma warning disable CA1416
namespace EtwIngest.Libs
{
    using System.Diagnostics.Eventing.Reader;
    using evtx;

    public class EvtxFileParser
    {
        private readonly string evtxFile;
        private readonly long fileSize;

        public EvtxFileParser(string evtxFile)
        {
            this.evtxFile = evtxFile;
            this.fileSize = new FileInfo(evtxFile).Length;
        }

        public List<EvtxRecord> Parse()
        {
            var records = new List<EvtxRecord>();
            using var fs = new FileStream(this.evtxFile, FileMode.Open, FileAccess.Read);
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

            return records;
        }
    }
}
