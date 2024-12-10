using System;
using System.Collections.Generic;

namespace Sharphound
{
    public class FetchQueryResult
    {
        public string CollectedComputerMachineID { get; set; }
        public DateTime CollectionDatetime { get; set; }
        public Dictionary<string, string> CollectionData { get; set; }
        public string CollectedComputerSID { get; set; }
        public string CollectedComputerNetbiosName { get; set; }
        public string CollectedComputerFullDomainName { get; set; }
    }
}
