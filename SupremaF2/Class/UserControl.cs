using Suprema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Class
{
    public class UserControl
    {
        private const int USER_PAGE_SIZE = 1024;

        private API.OnReadyToScan cbCardOnReadyToScan = null;
        private API.OnReadyToScan cbFingerOnReadyToScan = null;
        private API.OnReadyToScan cbFaceOnReadyToScan = null;
        private API.OnUserPhrase cbOnUserPhrase = null;

        private IntPtr sdkContext;


    }
}
