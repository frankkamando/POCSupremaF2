using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class reqDeviceConnection
    {
        public string DeviceIP { get; set; }
        public string DevicePort { get; set; }
    }

    public class reqDevicenIDConnection
    {
        public string DeviceIP { get; set; }
        public string DevicePort { get; set; }
        public string UserID { get; set; }
    }
    public class reqDevicenIDlvConnection
    {
        public string DeviceIP { get; set; }
        public string DevicePort { get; set; }
        public string UserID { get; set; }
        public int Level { get; set; }
    }
    
}
