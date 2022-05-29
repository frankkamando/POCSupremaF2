using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class resLevelModel
    {
        public Codes code { get; set; }
        public string message { get; set; }
        public List<LevelModel> Results { get; set; }
    }
    public class LevelModel
    {
        public string Level { get; set; }
        public string UserID { get; set; }
    }
}
