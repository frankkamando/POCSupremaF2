using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class respDeviceModel
    {
        public Codes code { get; set; }
        public string message { get; set; }
        public List<respDeviceInfo> Results { get; set; }
    }
    public class respDeviceInfo
    {
        public int ID { get; set; }
        public string DeviceID { get; set; }
        public string Type { get; set; }
        public string ConnMode { get; set; }
        public string DeviceIP { get; set; }
        public string DevicePort { get; set; }
    }
}
