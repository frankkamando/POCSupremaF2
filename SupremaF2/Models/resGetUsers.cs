using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class respGetUserModel
    {
        public Codes code { get; set; }
        public string message { get; set; }
        public List<resGetUsers> Results { get; set; }
    }
    public class resGetUsers
    {
        public string UserName { get; set; }
        public string UserID { get; set; }
        public string NumFace { get; set; }
        public string NumCard { get; set; }
        public string NumFinger { get; set; }
        public string SecurityLevel { get; set; }
        public int SecurityLevelID { get; set; }
        public string Pin { get; set; }
        public string Photo { get; set; }
        public string DeviceFinger { get; set; }        
        public List<Fingers> Finger { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class Fingers
    {
        //public string WSQFinger { get; set; }
        public string DeviceFinger { get; set; }
    }

    public enum BS2UserSecurityLevelEnum
    {
        DEFAULT = 0,
        LOWER = 1,
        LOW = 2,
        NORMAL = 3,
        HIGH = 4,
        HIGHER = 5,
    }
}
