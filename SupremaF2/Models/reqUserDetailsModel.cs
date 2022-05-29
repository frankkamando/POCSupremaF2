using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Models
{
    public class reqUserDetailsModel
    {
        public List<UserDetails> UsersRegDetails { get; set; }
    }

    public class UserDetails
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
    }

    public class reqUserDetailsIPModel
    {
        public string DeviceIP { get; set; }
        public string DevicePort { get; set; }
        public List<UserDetails> UsersRegDetails { get; set; }
    }

}
