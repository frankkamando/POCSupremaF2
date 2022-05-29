using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class respLogs
    {
        public Codes code { get; set; }
        public string message { get; set; }
        public List<respLogModel> Results { get; set; }
    }
    public class respLogModel
    {
        public string DeviceID { get; set; }
        public string Time { get; set; }
        public string EventID { get; set; }
        public string EventCode { get; set; }
        public string Action { get; set; }
        public string UserID { get; set; }
    }
}
