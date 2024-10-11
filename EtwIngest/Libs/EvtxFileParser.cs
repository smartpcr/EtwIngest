// -----------------------------------------------------------------------
// <copyright file="EvtxFileParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#pragma warning disable CA1416
namespace EtwIngest.Libs
{
    using System.Diagnostics.Eventing.Reader;

    public class EvtxFileParser
    {
        private readonly string evtxFile;
        private readonly long fileSize;

        public EvtxFileParser(string evtxFile)
        {
            this.evtxFile = evtxFile;
            this.fileSize = new FileInfo(evtxFile).Length;
        }

        public void Parse()
        {
            var logReader = new EventLogReader(evtxFile, PathType.FilePath);
            while (logReader.ReadEvent() is { } record)
            {
                // record.ProviderName + record.LogName + record.MachineName + record.Id
            }
        }
    }
}
