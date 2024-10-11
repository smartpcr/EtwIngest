// -----------------------------------------------------------------------
// <copyright file="EvtxRecord.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace EtwIngest.Libs
{
    public class EvtxRecord
    {
        public DateTime TimeStamp { get; set; }
        public string ProviderName { get; set; }
        public string LogName { get; set; }
        public string MachineName { get; set; }
        public int EventId { get; set; }
        public string Description { get; set; }
    }
}