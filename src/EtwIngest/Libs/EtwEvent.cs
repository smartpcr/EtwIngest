//-------------------------------------------------------------------------------
// <copyright file="EtwEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Libs
{
    public class EtwEvent
    {
        public string ProviderName { get; set; }
        public string EventName { get; set; }
        public List<(string fieldName, Type fieldType)> PayloadSchema { get; set; }
        public Dictionary<string, object> Payload { get; set; }
    }
}
