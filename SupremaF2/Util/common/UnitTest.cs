//#define SDK_AUTO_CONNECTION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using SupremaF2.Models;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace Suprema
{
#if !SDK_AUTO_CONNECTION
    class ReconnectionTask : IDisposable
    {
        private IntPtr sdkContext;
        private bool running;
        private Thread thread;
        private readonly object locker = new object();
        private EventWaitHandle eventWaitHandle = new AutoResetEvent(false);
        private Queue<UInt32> deviceIDQueue = new Queue<UInt32>();

        public ReconnectionTask(IntPtr sdkContext)
        {
            this.sdkContext = sdkContext;
            thread = new Thread(run);
        }

        public void enqueue(UInt32 deviceID)
        {
            bool isAlreadyRequested = false;

            lock (locker)
            {
                foreach (UInt32 targetDeviceID in deviceIDQueue)
                {
                    if (targetDeviceID == deviceID)
                    {
                        isAlreadyRequested = true;
                        break;
                    }
                }

                if (!isAlreadyRequested)
                {
                    deviceIDQueue.Enqueue(deviceID);
                }
            }

            if (!isAlreadyRequested)
            {
                Console.WriteLine("enqueue Device[{0, 10}].", deviceID);
                eventWaitHandle.Set();
            }
        }

        public void Dispose()
        {
            stop();
        }

        public void start()
        {
            if (!running)
            {
                running = true;
                thread.Start();
            }
        }

        public void stop()
        {
            if (running)
            {
                running = false;
                lock (locker)
                {
                    deviceIDQueue.Clear();
                }
                eventWaitHandle.Set();
                thread.Join();
                eventWaitHandle.Close();
            }
        }

        public void run()
        {
            while (running)
            {
                UInt32 deviceID = 0;

                lock (locker)
                {
                    if (deviceIDQueue.Count > 0)
                    {
                        deviceID = deviceIDQueue.Dequeue();
                    }
                }

                if (deviceID != 0)
                {
                    Console.WriteLine("trying to reconnect Device[{0, 10}].", deviceID);
                   
                    /*
                    BS2ErrorCode result = new BS2ErrorCode();
                    while (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        //result = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
                        result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
                        if (result != BS2ErrorCode.BS_SDK_ERROR_CANNOT_CONNECT_SOCKET)
                        {
                            Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                        }
                        else
                        {
                            enqueue(deviceID);
                        }

                    }
                    */
                    
                    /*
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        if (result != BS2ErrorCode.BS_SDK_ERROR_CANNOT_CONNECT_SOCKET)
                        {
                            //Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                            return;
                        }
                        else
                        {
                            enqueue(deviceID);
                        }
                    }
                    */            
                    
                    //원본
                    BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        if (result != BS2ErrorCode.BS_SDK_ERROR_CANNOT_CONNECT_SOCKET)
                        {
                            Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                            return;
                        }
                        else
                        {
                            enqueue(deviceID);
                        }
                    }       
                    
                     
                }
                else
                {
                    eventWaitHandle.WaitOne();
                }
            }
        }
    }
#endif

    public abstract class UnitTest
    {
        private static string path = AppDomain.CurrentDomain.BaseDirectory + "resource\\server\\";
        private string title;
        private API.OnDeviceFound cbOnDeviceFound = null;
        private API.OnDeviceAccepted cbOnDeviceAccepted = null;
        private API.OnDeviceConnected cbOnDeviceConnected = null;
        private API.OnDeviceDisconnected cbOnDeviceDisconnected = null;
        protected IntPtr sdkContext = IntPtr.Zero;
#if !SDK_AUTO_CONNECTION
        private ReconnectionTask reconnectionTask = null;
#endif
        private UInt32 deviceIDForServerMode = 0;
        private EventWaitHandle eventWaitHandle = new AutoResetEvent(false);

        private API.PreferMethod cbPreferMethod = null;
        private API.GetRootCaFilePath cbGetRootCaFilePath = null;
        private API.GetServerCaFilePath cbGetServerCaFilePath = null;
        private API.GetServerPrivateKeyFilePath cbGetServerPrivateKeyFilePath = null;
        private API.GetPassword cbGetPassword = null;
        private API.OnErrorOccured cbOnErrorOccured = null;
        private API.OnLogReceived cbOnLogReceived = null;

        private string ssl_server_root_crt = path + "ssl_server_root.crt";
        private string ssl_server_crt = path + "ssl_server.crt";
        private string ssl_server_pem = path + "ssl_server.pem";
        private string ssl_server_passwd = "supremaserver";
        //private API.OnSendRootCA cbOnSendRootCA = null;
        private API.CBDebugExPrint cbDebugExPrint = null;

        private IntPtr ptr_server_root_crt = IntPtr.Zero;
        private IntPtr ptr_server_crt = IntPtr.Zero;
        private IntPtr ptr_server_pem = IntPtr.Zero;
        private IntPtr ptr_server_passwd = IntPtr.Zero;

        protected abstract void runImpl(UInt32 deviceID);

        protected string Title {
            get
            {
                return title;
            }
            set
            {
                title = value;
                Console.Title = value;
            }
        }

        public UnitTest()
        {

            AppDomain.CurrentDomain.ProcessExit += (object s, EventArgs args) =>
            {
                if (sdkContext != IntPtr.Zero)
                {
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                }
            };
        }

        ~UnitTest()
        {
            if (ptr_server_root_crt != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr_server_root_crt);
            if (ptr_server_crt != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr_server_crt);
            if (ptr_server_pem != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr_server_pem);
            if (ptr_server_passwd != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr_server_passwd);
        }

        void printStructureSize<T>()
        {
            Console.WriteLine("{0} size : {1}", typeof(T), Marshal.SizeOf(typeof(T)));
        }

        public void run()
        {
            UInt32 deviceID = 0;
            IntPtr versionPtr = API.BS2_Version();
            Console.WriteLine("SDK version : " + Marshal.PtrToStringAnsi(versionPtr));
            
            sdkContext = API.BS2_AllocateContext();
            if (sdkContext == IntPtr.Zero)
            {
                Console.WriteLine("Can't allocate sdk context.");
                return;
            }
            cbPreferMethod = new API.PreferMethod(PreferMethodHandle);
            cbGetRootCaFilePath = new API.GetRootCaFilePath(GetRootCaFilePathHandle);
            cbGetServerCaFilePath = new API.GetServerCaFilePath(GetServerCaFilePathHandle);
            cbGetServerPrivateKeyFilePath = new API.GetServerPrivateKeyFilePath(GetServerPrivateKeyFilePathHandle);
            cbGetPassword = new API.GetPassword(GetPasswordHandle);
            cbOnErrorOccured = new API.OnErrorOccured(OnErrorOccuredHandle);
            //ServicePointManager.SecurityProtocol = (SecurityProtocolType)SecurityProtocolType.Ssl3;

            BS2ErrorCode sdkResult = (BS2ErrorCode)API.BS2_SetSSLHandler(sdkContext, cbPreferMethod, cbGetRootCaFilePath, cbGetServerCaFilePath, cbGetServerPrivateKeyFilePath, cbGetPassword, null);
            if (sdkResult != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("BS2_SetSSLHandler failed with : {0}", sdkResult);
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                ClearSDK();
                return;
            }
            else
            {
                //bSsl = true;
            }

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SDK initialization failed with : {0}", result);
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                ClearSDK();
                return;
            }

            cbOnDeviceFound = new API.OnDeviceFound(DeviceFound);
            cbOnDeviceAccepted = new API.OnDeviceAccepted(DeviceAccepted);
            cbOnDeviceConnected = new API.OnDeviceConnected(DeviceConnected);
            cbOnDeviceDisconnected = new API.OnDeviceDisconnected(DeviceDisconnected);

            result = (BS2ErrorCode)API.BS2_SetDeviceEventListener(sdkContext,
                                                                cbOnDeviceFound,
                                                                cbOnDeviceAccepted,
                                                                cbOnDeviceConnected,
                                                                cbOnDeviceDisconnected);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Can't register a callback function/method to a sdk.({0})", result);
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                ClearSDK();
                return;
            }

#if SDK_AUTO_CONNECTION
            result = (BS2ErrorCode)API.BS2_SetAutoConnection(sdkContext, 1);
#endif

           // eventWaitHandle.Close();
           //// API.BS2_ReleaseContext(sdkContext);
           // //sdkContext = IntPtr.Zero;

           // cbOnDeviceFound = null;
           // cbOnDeviceAccepted = null;
           // cbOnDeviceConnected = null;
           // cbOnDeviceDisconnected = null;
            //cbOnSendRootCA = null;
        }
        private const int USER_PAGE_SIZE = 1024;

        UInt32 RemotedeviceID = 0;
        IntPtr RemotesdkContext;
        public string ConnectToDeviceUnit(ref UInt32 deviceID, reqDeviceConnection reqDevice)
        {
            run();
            string deviceIpAddress = reqDevice.DeviceIP;
            IPAddress ipAddress;

            if (!IPAddress.TryParse(deviceIpAddress, out ipAddress))
            {
                Console.WriteLine("Invalid ip : " + deviceIpAddress);
                ClearSDK();
                return "Invalid ip : " + deviceIpAddress;
            }
           // BS2ErrorCode rmresult = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
            UInt16 port = Convert.ToUInt16(reqDevice.DevicePort == null ? (UInt16)BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT : reqDevice.DevicePort);
            Console.WriteLine("Trying to connect to device [ip :{0}, port : {1}]", deviceIpAddress, port);
            IntPtr ptrIPAddr = Marshal.StringToHGlobalAnsi(deviceIpAddress);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ptrIPAddr, port, out deviceID);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                ClearSDK();
                return "Can't connect to device(errorCode : " + result;
            }
            Marshal.FreeHGlobal(ptrIPAddr);
            //if (deviceID > 0)
            //{
            //    Console.Title = String.Format("{0} connected deviceID[{1}]", title, deviceID);

            //   #if !SDK_AUTO_CONNECTION
            //    reconnectionTask = new ReconnectionTask(sdkContext);
            //    reconnectionTask.start();
            //   #endif
            //    ////runImpl(deviceID);
            //   #if !SDK_AUTO_CONNECTION
            //    reconnectionTask.stop();
            //    reconnectionTask = null;
            //   #endif

            //    Console.WriteLine("Trying to discconect device[{0}].", deviceID);
                
            //}
            RemotesdkContext = sdkContext;
            RemotedeviceID = deviceID;
            //ClearSDK();
            return "Successfully connected to the device " + deviceID;
        }

        public string DisConnectDeviceUnitwithIp(reqDeviceConnection reqDevice)
        {
            UInt32 deviceID = 0;
            ConnectToDeviceUnit(ref deviceID, reqDevice);
            if (reconnectionTask != null)
            {
                Console.WriteLine("enqueue");
                reconnectionTask.enqueue(deviceID);

            }
            var result = (BS2ErrorCode)API.BS2_DisconnectDevice(RemotesdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                return "Got error " + result;
            }
            ClearSDK();
            cbOnDeviceDisconnected = new API.OnDeviceDisconnected(DeviceDisconnected);
            return "Device has been disconnected " + deviceID;
        }

        public string DisConnectDeviceUnitWithDeviceID(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            if (reconnectionTask != null)
            {
                Console.WriteLine("enqueue");
                reconnectionTask.enqueue(deviceID);

            }
            var result = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                return "Got error " + result;
            }
            ClearSDK();
            cbOnDeviceDisconnected = new API.OnDeviceDisconnected(DeviceDisconnected);
            return "Device has been disconnected " + deviceID;
        }

        public string trimString(string myString, string founder)
        {
            string newString = myString.Replace(founder, string.Empty);
            return newString;
        }        

        public respGetUserModel listUserFromDeviceWithIPUnit(reqDeviceConnection reqDevice)
        {
            UInt32 deviceID = 0;
            ConnectToDeviceUnit(ref deviceID,reqDevice);
            IntPtr outUidObjs = IntPtr.Zero;
            UInt32 numUserIds = 0;
            API.IsAcceptableUserID cbIsAcceptableUserID = null; // we don't need to user id filtering

            Console.WriteLine("Trying to get user list.");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserList(RemotesdkContext, deviceID, out outUidObjs, out numUserIds, cbIsAcceptableUserID);
            if (result == BS2ErrorCode.BS_SDK_SUCCESS)
            {
                List<resGetUsers> users = new List<resGetUsers>();
                if (numUserIds > 0)
                {
                    IntPtr curUidObjs = outUidObjs;
                    BS2UserBlob[] userBlobs = new BS2UserBlob[USER_PAGE_SIZE];

                    Console.WriteLine("Number of users : ({0}).", numUserIds);
                    for (UInt32 idx = 0; idx < numUserIds;)
                    {
                        UInt32 available = numUserIds - idx;
                        if (available > USER_PAGE_SIZE)
                        {
                            available = USER_PAGE_SIZE;
                        }

                        result = (BS2ErrorCode)API.BS2_GetUserDatas(RemotesdkContext, deviceID, curUidObjs, available, userBlobs, (UInt32)BS2UserMaskEnum.ALL);
                        if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                        {

                            for (UInt32 loop = 0; loop < available; ++loop)
                            {
                                resGetUsers singleUser = new resGetUsers();
                                singleUser.UserName = trimString(Encoding.Default.GetString(userBlobs[loop].name), "\0");
                                singleUser.UserID = trimString(Encoding.Default.GetString(userBlobs[loop].user.userID), "\0");
                                singleUser.NumFinger = userBlobs[loop].user.numFingers.ToString();
                                singleUser.NumFace = userBlobs[loop].user.numFaces.ToString();
                                singleUser.NumCard = userBlobs[loop].user.numCards.ToString();
                                singleUser.Pin = BitConverter.ToString(userBlobs[loop].pin);
                                singleUser.Photo = Util.GetBase64frombyte(userBlobs[loop].photo.data);
                                singleUser.SecurityLevel = Enum.GetName(typeof(BS2UserSecurityLevelEnum), int.Parse(userBlobs[loop].setting.securityLevel.ToString()));
                                singleUser.SecurityLevelID = int.Parse(userBlobs[loop].setting.securityLevel.ToString());
                                singleUser.StartTime = Util.ConvertFromUnixTimestamp((double)userBlobs[loop].setting.startTime);
                                singleUser.EndTime = Util.ConvertFromUnixTimestamp((double)userBlobs[loop].setting.endTime);

                                if (userBlobs[loop].user.numFingers > 0)
                                {
                                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                                    Type type = typeof(BS2Fingerprint);
                                    IntPtr curObjs = userBlobs[loop].fingerObjs;

                                    for (byte idfg = 0; idfg < userBlobs[loop].user.numFingers; ++idfg)
                                    {
                                        if (idfg == 0)
                                        {
                                            BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                                            singleUser.DeviceFinger = Util.GetBase64frombyte(finger.data);
                                        }
                                    }
                                }
                                users.Add(singleUser);

                                if (userBlobs[loop].cardObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].cardObjs);
                                if (userBlobs[loop].fingerObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].fingerObjs);
                                if (userBlobs[loop].faceObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].faceObjs);
                            }

                            idx += available;
                            curUidObjs += (int)available * BS2Environment.BS2_USER_ID_SIZE;
                        }
                        else
                        {
                            respGetUserModel respUserr = new respGetUserModel();
                            respUserr.message = ((BS2ErrorCode)result).ToString();
                            respUserr.code = Codes.FAILED;
                            API.BS2_ReleaseContext(sdkContext);
                            sdkContext = IntPtr.Zero;
                            ClearSDK();
                            return respUserr;
                        }
                    }

                    API.BS2_ReleaseObject(outUidObjs);

                    respGetUserModel respUser = new respGetUserModel();
                    respUser.message = ((BS2ErrorCode)result).ToString();
                    respUser.code = Codes.SUCCESS;
                    respUser.Results = users;
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                    ClearSDK();
                    return respUser;
                }
                else
                {
                    respGetUserModel respUser = new respGetUserModel();
                    respUser.message = ((BS2ErrorCode)result).ToString();
                    respUser.code = Codes.FAILED;
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                    ClearSDK();
                    return respUser;
                }
                
            }
            else
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "No user available";
                respUser.code = Codes.INFORMATION;
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                ClearSDK();
                return respUser;
            }
        }

        public respGetUserModel listUserFromDeviceWithDeviceIDUnit(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            //reqDeviceConnection reqDevice = new reqDeviceConnection
            //{
            //    DeviceIP="192.168.15.166",
            //    DevicePort="51213"
            //};
            //ConnectToDeviceUnit(ref deviceID, reqDevice);
            IntPtr outUidObjs = IntPtr.Zero;
            UInt32 numUserIds = 0;
            API.IsAcceptableUserID cbIsAcceptableUserID = null; // we don't need to user id filtering

            Console.WriteLine("Trying to get user list.");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserList(sdkContext, deviceID, out outUidObjs, out numUserIds, cbIsAcceptableUserID);
            if (result == BS2ErrorCode.BS_SDK_SUCCESS)
            {
                List<resGetUsers> users = new List<resGetUsers>();
                if (numUserIds > 0)
                {
                    IntPtr curUidObjs = outUidObjs;
                    BS2UserBlob[] userBlobs = new BS2UserBlob[USER_PAGE_SIZE];

                    Console.WriteLine("Number of users : ({0}).", numUserIds);
                    for (UInt32 idx = 0; idx < numUserIds;)
                    {
                        UInt32 available = numUserIds - idx;
                        if (available > USER_PAGE_SIZE)
                        {
                            available = USER_PAGE_SIZE;
                        }

                        result = (BS2ErrorCode)API.BS2_GetUserDatas(RemotesdkContext, deviceID, curUidObjs, available, userBlobs, (UInt32)BS2UserMaskEnum.ALL);
                        if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                        {

                            for (UInt32 loop = 0; loop < available; ++loop)
                            {
                                resGetUsers singleUser = new resGetUsers();
                                singleUser.UserName = trimString(Encoding.Default.GetString(userBlobs[loop].name), "\0");
                                singleUser.UserID = trimString(Encoding.Default.GetString(userBlobs[loop].user.userID), "\0");
                                singleUser.NumFinger = userBlobs[loop].user.numFingers.ToString();
                                singleUser.NumFace = userBlobs[loop].user.numFingers.ToString();
                                singleUser.NumCard = userBlobs[loop].user.numCards.ToString();
                                singleUser.Pin = BitConverter.ToString(userBlobs[loop].pin);
                                singleUser.Photo = Util.GetBase64frombyte(userBlobs[loop].photo.data);
                                singleUser.SecurityLevel = Enum.GetName(typeof(BS2UserSecurityLevelEnum), int.Parse(userBlobs[loop].setting.securityLevel.ToString()));
                                singleUser.SecurityLevelID = int.Parse(userBlobs[loop].setting.securityLevel.ToString());
                                singleUser.StartTime = Util.ConvertFromUnixTimestamp((double)userBlobs[loop].setting.startTime);
                                singleUser.EndTime = Util.ConvertFromUnixTimestamp((double)userBlobs[loop].setting.endTime);

                                if (userBlobs[loop].user.numFingers > 0)
                                {
                                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                                    Type type = typeof(BS2Fingerprint);
                                    IntPtr curObjs = userBlobs[loop].fingerObjs;

                                    for (byte idfg = 0; idfg < userBlobs[loop].user.numFingers; ++idfg)
                                    {
                                        if (idfg == 0)
                                        {
                                            BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                                            singleUser.DeviceFinger = Util.GetBase64frombyte(finger.data);
                                        }
                                    }
                                }
                                users.Add(singleUser);

                                if (userBlobs[loop].cardObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].cardObjs);
                                if (userBlobs[loop].fingerObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].fingerObjs);
                                if (userBlobs[loop].faceObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].faceObjs);
                            }

                            idx += available;
                            curUidObjs += (int)available * BS2Environment.BS2_USER_ID_SIZE;
                        }
                        else
                        {
                            respGetUserModel respUserr = new respGetUserModel();
                            respUserr.message = ((BS2ErrorCode)result).ToString();
                            respUserr.code = Codes.FAILED;
                            API.BS2_ReleaseContext(sdkContext);
                            sdkContext = IntPtr.Zero;
                            return respUserr;
                        }
                    }

                    API.BS2_ReleaseObject(outUidObjs);

                    respGetUserModel respUser = new respGetUserModel();
                    respUser.message = ((BS2ErrorCode)result).ToString();
                    respUser.code = Codes.SUCCESS;
                    respUser.Results = users;
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                    ClearSDK();
                    return respUser;
                }
                else
                {
                    respGetUserModel respUser = new respGetUserModel();
                    respUser.message = "No user available";
                    respUser.code = Codes.INFORMATION;
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                    ClearSDK();
                    return respUser;
                }
            }
            else
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.FAILED;
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                ClearSDK();
                return respUser;
            }
        }

        public respDeviceModel ConnectToDeviceWithDeviceIDUnit(ref UInt32 deviceID)
        {
            //sdkContext = API.BS2_AllocateContext();
            run();
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevices(sdkContext);
            result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            IntPtr deviceListObj = IntPtr.Zero;
            UInt32 numDevice = 0;

            const UInt32 LONG_TIME_STANDBY_7S = 7;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_7S);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            result = (BS2ErrorCode)API.BS2_GetDevices(sdkContext, out deviceListObj, out numDevice);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            if (numDevice > 0)
            {
                List<respDeviceInfo> respDeviceInfo = new List<respDeviceInfo>();
                BS2SimpleDeviceInfo deviceInfo;
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    respDeviceInfo DeviceInfo = new respDeviceInfo();
                    respDeviceModel respDeviceInfom = new respDeviceModel();
                    deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));
                    result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {

                        respDeviceInfom.message = ((BS2ErrorCode)result).ToString();
                        respDeviceInfom.code = Codes.FAILED;
                        return respDeviceInfom;
                    }

                    Console.WriteLine("[{0, 3:##0}] ==> ID[{1, 10}] Type[{2, 20}] Connection mode[{3}] Ip[{4, 16}] port[{5, 5}] Master/Slave[{6}]",
                            idx,
                            deviceID,
                            API.productNameDictionary.ContainsKey((BS2DeviceTypeEnum)deviceInfo.type) ? API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type] : (API.productNameDictionary[BS2DeviceTypeEnum.UNKNOWN] + "(" + deviceInfo.type + ")"),
                            (BS2ConnectionModeEnum)deviceInfo.connectionMode,
                            new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString(),
                            deviceInfo.port, deviceInfo.rs485Mode);
                    DeviceInfo.ID = (int)idx;
                    DeviceInfo.DeviceID = ((int)deviceID).ToString();
                    DeviceInfo.DeviceIP = new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString();
                    DeviceInfo.DevicePort = deviceInfo.port.ToString();
                    DeviceInfo.ConnMode = ((BS2ConnectionModeEnum)deviceInfo.connectionMode).ToString();
                    respDeviceInfo.Add(DeviceInfo);
                }
                respDeviceModel respDeviceInfomd = new respDeviceModel();
                respDeviceInfomd.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfomd.code = Codes.SUCCESS;
                respDeviceInfomd.Results = respDeviceInfo;
                return respDeviceInfomd;
            }
            else
            {
                respDeviceModel respDeviceInfomodel = new respDeviceModel();
                respDeviceInfomodel.message = "No user available";
                respDeviceInfomodel.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfomodel;
            }

        }

        public respDeviceModel SearchDevices(ref UInt32 deviceID)
        {
            //sdkContext = API.BS2_AllocateContext();
            run();
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevices(sdkContext);
            result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            IntPtr deviceListObj = IntPtr.Zero;
            UInt32 numDevice = 0;

            const UInt32 LONG_TIME_STANDBY_7S = 7;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_7S);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            result = (BS2ErrorCode)API.BS2_GetDevices(sdkContext, out deviceListObj, out numDevice);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respDeviceModel respDeviceInfo = new respDeviceModel();
                respDeviceInfo.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfo.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfo;
            }

            if (numDevice > 0)
            {
                List<respDeviceInfo> respDeviceInfo = new List<respDeviceInfo>();
                BS2SimpleDeviceInfo deviceInfo;
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    respDeviceInfo DeviceInfo = new respDeviceInfo();
                    respDeviceModel respDeviceInfom = new respDeviceModel();
                    deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));
                    result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {

                        respDeviceInfom.message = ((BS2ErrorCode)result).ToString();
                        respDeviceInfom.code = Codes.FAILED;
                        return respDeviceInfom;
                    }

                    Console.WriteLine("[{0, 3:##0}] ==> ID[{1, 10}] Type[{2, 20}] Connection mode[{3}] Ip[{4, 16}] port[{5, 5}] Master/Slave[{6}]",
                        idx,
                        deviceID,
                        API.productNameDictionary.ContainsKey((BS2DeviceTypeEnum)deviceInfo.type) ? API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type] : (API.productNameDictionary[BS2DeviceTypeEnum.UNKNOWN] + "(" + deviceInfo.type + ")"),
                        (BS2ConnectionModeEnum)deviceInfo.connectionMode,
                        new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString(),
                        deviceInfo.port, deviceInfo.rs485Mode);
                    DeviceInfo.ID = (int)idx;
                    DeviceInfo.DeviceID = ((int)deviceID).ToString();
                    DeviceInfo.DeviceIP = new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString();
                    DeviceInfo.DevicePort = deviceInfo.port.ToString();
                    DeviceInfo.ConnMode = ((BS2ConnectionModeEnum)deviceInfo.connectionMode).ToString();
                    respDeviceInfo.Add(DeviceInfo);
                }
                respDeviceModel respDeviceInfomd = new respDeviceModel();
                respDeviceInfomd.message = ((BS2ErrorCode)result).ToString();
                respDeviceInfomd.code = Codes.SUCCESS;
                respDeviceInfomd.Results = respDeviceInfo;
                return respDeviceInfomd;
            }
            else
            {
                respDeviceModel respDeviceInfomodel = new respDeviceModel();
                respDeviceInfomodel.message = "No user available";
                respDeviceInfomodel.code = Codes.FAILED;
                ClearSDK();
                return respDeviceInfomodel;
            }

        }

        void ClearSDK()
        {
            if (sdkContext != IntPtr.Zero)
            {
                API.BS2_ReleaseContext(sdkContext);
            }
            sdkContext = IntPtr.Zero;

            eventWaitHandle.Close();
            cbOnDeviceFound = null;
            cbOnDeviceAccepted = null;
            cbOnDeviceConnected = null;
            cbOnDeviceDisconnected = null;
            Thread.Sleep(1);
        }

        public void closeconn(UInt32 deviceID, BS2ErrorCode result)
        {
            if (deviceID > 0)
            {
                Console.Title = String.Format("{0} connected deviceID[{1}]", title, deviceID);

#if !SDK_AUTO_CONNECTION
                reconnectionTask = new ReconnectionTask(sdkContext);
                reconnectionTask.start();
#endif
                runImpl(deviceID);
#if !SDK_AUTO_CONNECTION
                reconnectionTask.stop();
                reconnectionTask = null;
#endif

                Console.WriteLine("Trying to discconect device[{0}].", deviceID);
                result = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                }
            }


            eventWaitHandle.Close();
            API.BS2_ReleaseContext(sdkContext);
            sdkContext = IntPtr.Zero;

            cbOnDeviceFound = null;
            cbOnDeviceAccepted = null;
            cbOnDeviceConnected = null;
            cbOnDeviceDisconnected = null;
        }

        public respLogs getLogWithIP(reqDeviceConnection reqDevice)
        {
            UInt32 deviceID = 0;
            ConnectToDeviceUnit(ref deviceID, reqDevice);
            const UInt32 defaultLogPageSize = 1024;
            Type structureType = typeof(BS2Event);
            int structSize = Marshal.SizeOf(structureType);
            bool getAllLog = false;
            UInt32 lastEventId = 0;
            UInt32 amount;
            IntPtr outEventLogObjs = IntPtr.Zero;
            UInt32 outNumEventLogs = 0;
            cbOnLogReceived = new API.OnLogReceived(NormalLogReceived);

            lastEventId = (UInt32)0;
            amount = (UInt32)0;

            getAllLog = true;
            amount = defaultLogPageSize;

            outEventLogObjs = IntPtr.Zero;
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetLog(sdkContext, deviceID, lastEventId, amount, out outEventLogObjs, out outNumEventLogs);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respLogs logsdata = new respLogs();
                logsdata.code = Codes.FAILED;
                logsdata.message = ((BS2ErrorCode)result).ToString();
                Console.WriteLine("Got error({0}).", result);
            ClearSDK();
            return logsdata;
            }
            List<respLogModel> logs = new List<respLogModel>();
            if (outNumEventLogs > 0)
            {
                IntPtr curEventLogObjs = outEventLogObjs;
                    
                for (UInt32 idx = 0; idx < outNumEventLogs; idx++)
                {
                    respLogModel log = new respLogModel();
                    BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, structureType);

                    if(
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_SUCCESS_FACE ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_SUCCESS_FINGER ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_FAIL_FINGER ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_FAIL_FACE ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_FAIL_CARD ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_FAIL_PIN ||
                        (BS2EventCodeEnum)eventLog.code== BS2EventCodeEnum.IDENTIFY_FAIL_ID
                        )
                    {
                        DateTime eventTime = Util.ConvertFromUnixTimestamp(eventLog.dateTime);
                        string userID = System.Text.Encoding.ASCII.GetString(eventLog.userID).TrimEnd('\0');

                        if (userID.Length == 0)
                        {
                            userID = "unknown";
                        }
                        string action = "Not specified";
                        BS2TNAKeyEnum tnaKeyEnum = (BS2TNAKeyEnum)eventLog.param;
                        if (tnaKeyEnum != BS2TNAKeyEnum.UNSPECIFIED)
                        {
                            if (tnaKeyEnum.ToString() == "KEY1")
                            {
                                action = "IN";
                            }
                            else if (tnaKeyEnum.ToString() == "KEY2")
                            {
                                action = "OUT";
                            }
                            else if (tnaKeyEnum.ToString() == "KEY3")
                            {
                                action = "IN DUTY";
                            }
                            else
                            {
                                action = "OUT DUTY";
                            }
                        }
                        log.DeviceID = eventLog.deviceID.ToString();
                        log.Time = eventTime.ToString("yyyy-MM-dd HH:mm:ss");
                        log.EventID = eventLog.id.ToString();
                        log.EventCode = ((BS2EventCodeEnum)eventLog.code).ToString();
                        log.UserID = userID;
                        log.Action = action;
                        logs.Add(log);

                    }
                    Console.WriteLine(Util.GetLogMsg(eventLog));
                    curEventLogObjs += structSize;
                    lastEventId = eventLog.id;
                }

                API.BS2_ReleaseObject(outEventLogObjs);

                respLogs logsdata = new respLogs();
                logsdata.code = Codes.SUCCESS;
                logsdata.message = ((BS2ErrorCode)result).ToString();
                logsdata.Results = logs;
            ClearSDK();
            return logsdata;
            }
            else
            {
                respLogs logsdata = new respLogs();
                logsdata.code = Codes.FAILED;
                logsdata.message = "No Events Logs";
                logsdata.Results = logs;
                ClearSDK();
                return logsdata;
            }
        }

        public respLogs getLogWithDeviceID(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            const UInt32 defaultLogPageSize = 1024;
            Type structureType = typeof(BS2Event);
            int structSize = Marshal.SizeOf(structureType);
            bool getAllLog = false;
            UInt32 lastEventId = 0;
            UInt32 amount;
            IntPtr outEventLogObjs = IntPtr.Zero;
            UInt32 outNumEventLogs = 0;
            cbOnLogReceived = new API.OnLogReceived(NormalLogReceived);

            lastEventId = (UInt32)0;
            amount = (UInt32)0;

            getAllLog = true;
            amount = defaultLogPageSize;

            outEventLogObjs = IntPtr.Zero;
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetLog(sdkContext, deviceID, lastEventId, amount, out outEventLogObjs, out outNumEventLogs);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respLogs logsdata = new respLogs();
                logsdata.code = Codes.FAILED;
                logsdata.message = ((BS2ErrorCode)result).ToString();
                Console.WriteLine("Got error({0}).", result);
                ClearSDK();
                return logsdata;
            }
            List<respLogModel> logs = new List<respLogModel>();
            if (outNumEventLogs > 0)
            {
                IntPtr curEventLogObjs = outEventLogObjs;

                for (UInt32 idx = 0; idx < outNumEventLogs; idx++)
                {
                    respLogModel log = new respLogModel();
                    BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, structureType);

                    if (
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_SUCCESS_FACE ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_SUCCESS_FINGER ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_FAIL_FINGER ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_FAIL_FACE ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_FAIL_CARD ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_FAIL_PIN ||
                        (BS2EventCodeEnum)eventLog.code == BS2EventCodeEnum.IDENTIFY_FAIL_ID
                        )
                    {
                        DateTime eventTime = Util.ConvertFromUnixTimestamp(eventLog.dateTime);
                        string userID = System.Text.Encoding.ASCII.GetString(eventLog.userID).TrimEnd('\0');

                        if (userID.Length == 0)
                        {
                            userID = "unknown";
                        }
                        string action = "Not specified";
                        BS2TNAKeyEnum tnaKeyEnum = (BS2TNAKeyEnum)eventLog.param;
                        if (tnaKeyEnum != BS2TNAKeyEnum.UNSPECIFIED)
                        {
                            if (tnaKeyEnum.ToString() == "KEY1")
                            {
                                action = "IN";
                            }
                            else if (tnaKeyEnum.ToString() == "KEY2")
                            {
                                action = "OUT";
                            }
                            else if (tnaKeyEnum.ToString() == "KEY3")
                            {
                                action = "IN DUTY";
                            }
                            else
                            {
                                action = "OUT DUTY";
                            }
                        }
                        log.DeviceID = eventLog.deviceID.ToString();
                        log.Time = eventTime.ToString("yyyy-MM-dd HH:mm:ss");
                        log.EventID = eventLog.id.ToString();
                        log.EventCode = ((BS2EventCodeEnum)eventLog.code).ToString();
                        log.UserID = userID;
                        log.Action = action;
                        logs.Add(log);

                    }
                    Console.WriteLine(Util.GetLogMsg(eventLog));
                    curEventLogObjs += structSize;
                    lastEventId = eventLog.id;
                }

                API.BS2_ReleaseObject(outEventLogObjs);

                respLogs logsdata = new respLogs();
                logsdata.code = Codes.SUCCESS;
                logsdata.message = ((BS2ErrorCode)result).ToString();
                logsdata.Results = logs;
                ClearSDK();
                return logsdata;
            }
            else
            {
                respLogs logsdata = new respLogs();
                logsdata.code = Codes.FAILED;
                logsdata.message = "No Events Logs";
                logsdata.Results = logs;
                ClearSDK();
                return logsdata;
            }
        }

        private void NormalLogReceived(UInt32 deviceID, IntPtr log)
        {
            if (log != IntPtr.Zero)
            {
                BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(log, typeof(BS2Event));
                Console.WriteLine(Util.GetLogMsg(eventLog));
            }
        }

        public string deleteSingleUserFromDeviceWithIP(reqDevicenIDConnection reqDevice)
        {
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = reqDevice.DeviceIP,
                DevicePort = reqDevice.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            string userID = reqDevice.UserID;
            if (userID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than "+ BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                byte[] uidArray = new byte[BS2Environment.BS2_USER_ID_SIZE];
                byte[] rawUid = Encoding.UTF8.GetBytes(userID);
                IntPtr uids = Marshal.AllocHGlobal(BS2Environment.BS2_USER_ID_SIZE);

                Array.Clear(uidArray, 0, BS2Environment.BS2_USER_ID_SIZE);
                Array.Copy(rawUid, 0, uidArray, 0, rawUid.Length);
                Marshal.Copy(uidArray, 0, uids, BS2Environment.BS2_USER_ID_SIZE);

                result = (BS2ErrorCode)API.BS2_RemoveUser(sdkContext, deviceID, uids, 1);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    ClearSDK();
                    return "Can't connect to device(errorCode : " + result;
                }
                else
                {
                    Marshal.FreeHGlobal(uids);
                    ClearSDK();
                    return "Successful Deleted";
                }                
            }
        }

        public string deleteAllUserFromDeviceWithIP(reqDeviceConnection reqDevice)
        {
            UInt32 deviceID = 0;
            ConnectToDeviceUnit(ref deviceID, reqDevice);
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            result = (BS2ErrorCode)API.BS2_RemoveAllUser(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                ClearSDK();
                return "Can't connect to device(errorCode : " + result;
            }
            else
            {
                ClearSDK();
                return "Successful Deleted";
            }
        }

        public string deleteSingleUserFromDeviceWithDeviceID(ref UInt32 deviceID,string UserID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);

            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            string userID = UserID;
            if (userID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                byte[] uidArray = new byte[BS2Environment.BS2_USER_ID_SIZE];
                byte[] rawUid = Encoding.UTF8.GetBytes(userID);
                IntPtr uids = Marshal.AllocHGlobal(BS2Environment.BS2_USER_ID_SIZE);

                Array.Clear(uidArray, 0, BS2Environment.BS2_USER_ID_SIZE);
                Array.Copy(rawUid, 0, uidArray, 0, rawUid.Length);
                Marshal.Copy(uidArray, 0, uids, BS2Environment.BS2_USER_ID_SIZE);

                result = (BS2ErrorCode)API.BS2_RemoveUser(sdkContext, deviceID, uids, 1);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    ClearSDK();
                    return "Can't connect to device(errorCode : " + result;
                }
                else
                {
                    Marshal.FreeHGlobal(uids);
                    ClearSDK();
                    return "Successful Deleted";
                }
            }
        }

        public string deleteAllUserFromDeviceWithDeviceID(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            result = (BS2ErrorCode)API.BS2_RemoveAllUser(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                ClearSDK();
                return "Can't connect to device(errorCode : " + result;
            }
            else
            {
                ClearSDK();
                return "Successful Deleted";
            }
        }
        
        public respGetUserModel getsingleUserFromDeviceWithIP(reqDevicenIDConnection reqDevice)
        {
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = reqDevice.DeviceIP,
                DevicePort = reqDevice.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            IntPtr outUidObjs = IntPtr.Zero;
            byte[] tempUID = new byte[BS2Environment.BS2_USER_ID_SIZE];
            Array.Clear(tempUID, 0, BS2Environment.BS2_USER_ID_SIZE);
            IntPtr ptrUID = Marshal.AllocHGlobal(tempUID.Length);

            string userID = reqDevice.UserID;
            List<resGetUsers> users = new List<resGetUsers>();
            if (userID.Length == 0)
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id can not be empty.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                Console.WriteLine("The user id should less than {0} words.", BS2Environment.BS2_USER_ID_SIZE);
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id should less than "+ BS2Environment.BS2_USER_ID_SIZE + " words.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }

            //TODO Alphabet user id is not implemented yet.
            UInt32 uid;
            if (!UInt32.TryParse(userID, out uid))
            {
                Console.WriteLine("The user id should be a numeric.");
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id should be a numeric.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }

            byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
            Array.Copy(userIDArray, tempUID, userIDArray.Length);
            Marshal.Copy(tempUID, 0, ptrUID, tempUID.Length);

            BS2UserBlobEx[] userBlobs = new BS2UserBlobEx[1];
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserDatasEx(sdkContext, deviceID, ptrUID, 1, userBlobs, (UInt32)BS2UserMaskEnum.ALL);

            Marshal.FreeHGlobal(ptrUID);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else
            {
                resGetUsers singleUser = new resGetUsers();
                singleUser.UserName = trimString(Encoding.Default.GetString(userBlobs[0].name), "\0");
                singleUser.UserID = trimString(Encoding.Default.GetString(userBlobs[0].user.userID), "\0");
                singleUser.NumFinger = userBlobs[0].user.numFingers.ToString();
                singleUser.NumFace = userBlobs[0].user.numFaces.ToString();
                singleUser.NumCard = userBlobs[0].user.numCards.ToString();
                singleUser.Pin = BitConverter.ToString(userBlobs[0].pin);
                singleUser.Photo = Util.GetBase64frombyte(userBlobs[0].photo.data);
                singleUser.SecurityLevel = Enum.GetName(typeof(BS2UserSecurityLevelEnum), int.Parse(userBlobs[0].setting.securityLevel.ToString()));
                singleUser.SecurityLevelID = int.Parse(userBlobs[0].setting.securityLevel.ToString());
                singleUser.StartTime = Util.ConvertFromUnixTimestamp((double)userBlobs[0].setting.startTime);
                singleUser.EndTime = Util.ConvertFromUnixTimestamp((double)userBlobs[0].setting.endTime);
                if (userBlobs[0].user.numFingers > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                    Type type = typeof(BS2Fingerprint);
                    IntPtr curObjs = userBlobs[0].fingerObjs;

                    for (byte idfg = 0; idfg < userBlobs[0].user.numFingers; ++idfg)
                    {
                        if (idfg == 0)
                        {
                            BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                            singleUser.DeviceFinger = Util.GetBase64frombyte(finger.data);
                        }
                    }
                }
                users.Add(singleUser);

                respGetUserModel respUser = new respGetUserModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.SUCCESS;
                respUser.Results = users;
                ClearSDK();
                return respUser;

            }
            //Release
            if (userBlobs[0].cardObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].cardObjs);
            if (userBlobs[0].fingerObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].fingerObjs);
            if (userBlobs[0].faceObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].faceObjs);
        }

        public respGetUserModel getsingleUserFromDeviceWithDeviceID(ref UInt32 deviceID, string UserID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            IntPtr outUidObjs = IntPtr.Zero;
            byte[] tempUID = new byte[BS2Environment.BS2_USER_ID_SIZE];
            Array.Clear(tempUID, 0, BS2Environment.BS2_USER_ID_SIZE);
            IntPtr ptrUID = Marshal.AllocHGlobal(tempUID.Length);

            string userID = UserID;
            List<resGetUsers> users = new List<resGetUsers>();
            if (userID.Length == 0)
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id can not be empty.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                Console.WriteLine("The user id should less than {0} words.", BS2Environment.BS2_USER_ID_SIZE);
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }

            //TODO Alphabet user id is not implemented yet.
            UInt32 uid;
            if (!UInt32.TryParse(userID, out uid))
            {
                Console.WriteLine("The user id should be a numeric.");
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = "The user id should be a numeric.";
                respUser.code = Codes.FAILED;
                ClearSDK();
                return respUser;
            }

            byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
            Array.Copy(userIDArray, tempUID, userIDArray.Length);
            Marshal.Copy(tempUID, 0, ptrUID, tempUID.Length);

            BS2UserBlobEx[] userBlobs = new BS2UserBlobEx[1];
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserDatasEx(sdkContext, deviceID, ptrUID, 1, userBlobs, (UInt32)BS2UserMaskEnum.ALL);

            Marshal.FreeHGlobal(ptrUID);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                respGetUserModel respUser = new respGetUserModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else
            {
                resGetUsers singleUser = new resGetUsers();
                singleUser.UserName = trimString(Encoding.Default.GetString(userBlobs[0].name), "\0");
                singleUser.UserID = trimString(Encoding.Default.GetString(userBlobs[0].user.userID), "\0");
                singleUser.NumFinger = userBlobs[0].user.numFingers.ToString();
                singleUser.NumFace = userBlobs[0].user.numFaces.ToString();
                singleUser.NumCard = userBlobs[0].user.numCards.ToString();
                singleUser.Pin = BitConverter.ToString(userBlobs[0].pin);
                singleUser.Photo = Util.GetBase64frombyte(userBlobs[0].photo.data);
                singleUser.SecurityLevel = Enum.GetName(typeof(BS2UserSecurityLevelEnum), int.Parse(userBlobs[0].setting.securityLevel.ToString()));
                singleUser.SecurityLevelID = int.Parse(userBlobs[0].setting.securityLevel.ToString());
                singleUser.StartTime = Util.ConvertFromUnixTimestamp((double)userBlobs[0].setting.startTime);
                singleUser.EndTime = Util.ConvertFromUnixTimestamp((double)userBlobs[0].setting.endTime);
                if (userBlobs[0].user.numFingers > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                    Type type = typeof(BS2Fingerprint);
                    IntPtr curObjs = userBlobs[0].fingerObjs;

                    for (byte idfg = 0; idfg < userBlobs[0].user.numFingers; ++idfg)
                    {
                        if (idfg == 0)
                        {
                            BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                            singleUser.DeviceFinger = Util.GetBase64frombyte(finger.data);
                        }
                    }
                }
                users.Add(singleUser);

                respGetUserModel respUser = new respGetUserModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.SUCCESS;
                respUser.Results = users;
                ClearSDK();
                return respUser;

            }
            //Release
            if (userBlobs[0].cardObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].cardObjs);
            if (userBlobs[0].fingerObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].fingerObjs);
            if (userBlobs[0].faceObjs != IntPtr.Zero)
                API.BS2_ReleaseObject(userBlobs[0].faceObjs);
        }

        public string setAuthOperatorLevelExIP(reqDevicenIDlvConnection reqDevice)
        {
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = reqDevice.DeviceIP,
                DevicePort = reqDevice.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            if (reqDevice.UserID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (reqDevice.UserID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(reqDevice.UserID);

                BS2AuthOperatorLevel item = Util.AllocateStructure<BS2AuthOperatorLevel>();
                int structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));
                IntPtr operatorlevelObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curOperatorlevelObj = operatorlevelObj;
                foreach (string strUserID in userIDs)
                {
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Clear(item.userID, 0, BS2Environment.BS2_USER_ID_SIZE);
                    Array.Copy(userIDArray, item.userID, userIDArray.Length);
                    if (reqDevice.Level == 1)
                    {
                        item.level = (byte)BS2UserOperatorEnum.ADMIN;
                    }
                    else if (reqDevice.Level == 2)
                    {
                        item.level = (byte)BS2UserOperatorEnum.USER;
                    }

                    Marshal.StructureToPtr(item, curOperatorlevelObj, false);
                    curOperatorlevelObj = (IntPtr)((long)curOperatorlevelObj + structSize);
                }

                Console.WriteLine("Trying to set auth operator level ex to device.");
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_SetAuthOperatorLevelEx(sdkContext, deviceID, operatorlevelObj, (UInt32)userIDs.Count);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                    ClearSDK();
                    return ((BS2ErrorCode)result).ToString();
                }
                else
                {
                    ClearSDK();
                    return "Successfull Set";
                }

                Marshal.FreeHGlobal(operatorlevelObj);
            }
        }

        public string setAuthOperatorLevelExDeviceID(ref UInt32 deviceID,string UserID,int level=0)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            string userID = UserID;
            if (userID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(userID);

                BS2AuthOperatorLevel item = Util.AllocateStructure<BS2AuthOperatorLevel>();
                int structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));
                IntPtr operatorlevelObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curOperatorlevelObj = operatorlevelObj;
                foreach (string strUserID in userIDs)
                {
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Clear(item.userID, 0, BS2Environment.BS2_USER_ID_SIZE);
                    Array.Copy(userIDArray, item.userID, userIDArray.Length);
                    if (level==1)
                    {
                        item.level = (byte)BS2UserOperatorEnum.ADMIN;
                    }
                    else if(level == 2)
                    {
                        item.level = (byte)BS2UserOperatorEnum.USER;
                    }

                    Marshal.StructureToPtr(item, curOperatorlevelObj, false);
                    curOperatorlevelObj = (IntPtr)((long)curOperatorlevelObj + structSize);
                }

                Console.WriteLine("Trying to set auth operator level ex to device.");
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_SetAuthOperatorLevelEx(sdkContext, deviceID, operatorlevelObj, (UInt32)userIDs.Count);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                    ClearSDK();
                    return ((BS2ErrorCode)result).ToString();
                }
                else
                {
                    ClearSDK();
                    return "Successfull Set";
                }

                Marshal.FreeHGlobal(operatorlevelObj);
            }
        }

        public string delAllAuthOperatorLevelExDeviceID(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveAllAuthOperatorLevelEx(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                ClearSDK();
                return ((BS2ErrorCode)result).ToString();
            }
            else
            {
                ClearSDK();
                return "Successfull Deleted";
            }
        }

        public string delAllAuthOperatorLevelExIP(reqDeviceConnection reqd)
        {
            UInt32 deviceID = 0;
            //reqDeviceConnection reqd = new reqDeviceConnection
            //{
            //    DeviceIP = reqDevice.DeviceIP,
            //    DevicePort = reqDevice.DevicePort
            //};

            ConnectToDeviceUnit(ref deviceID, reqd);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveAllAuthOperatorLevelEx(sdkContext, deviceID);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                ClearSDK();
                return ((BS2ErrorCode)result).ToString();
            }
            else
            {
                ClearSDK();
                return "Successfull Deleted";
            }
        }

        public resLevelModel getAuthOperatorLevelExDeviceID(ref UInt32 deviceID,string UserID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            string userID = UserID;
            if (userID.Length == 0)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "The user id can not be empty.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "The user id should less than "+ BS2Environment.BS2_USER_ID_SIZE + " words.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(userID);

                int structSize = BS2Environment.BS2_USER_ID_SIZE;
                byte[] userIDBuf = new byte[structSize];
                IntPtr userIDObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curUserIDObj = userIDObj;
                foreach (string strUserID in userIDs)
                {
                    Array.Clear(userIDBuf, 0, userIDBuf.Length);
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Copy(userIDArray, userIDBuf, Math.Min(userIDArray.Length, userIDBuf.Length));
                    Marshal.Copy(userIDBuf, 0, curUserIDObj, userIDBuf.Length);
                    curUserIDObj = (IntPtr)((long)curUserIDObj + structSize);
                }

                IntPtr operatorlevelObj = IntPtr.Zero;
                UInt32 numOperatorlevel = 0;

                BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetAuthOperatorLevelEx(sdkContext, deviceID, userIDObj, (UInt32)userIDs.Count, out operatorlevelObj, out numOperatorlevel);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = ((BS2ErrorCode)result).ToString();
                    respUser.code = Codes.ERROR;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }
                else if (numOperatorlevel > 0)
                {
                    IntPtr curOperatorLevelObj = operatorlevelObj;
                    structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));

                    List<LevelModel> leve = new List<LevelModel>();
                    for (int idx = 0; idx < numOperatorlevel; ++idx)
                    {
                        LevelModel lv = new LevelModel();
                        BS2AuthOperatorLevel item = (BS2AuthOperatorLevel)Marshal.PtrToStructure(curOperatorLevelObj, typeof(BS2AuthOperatorLevel));
                        lv.UserID = Encoding.UTF8.GetString(item.userID).TrimEnd('\0');
                        lv.Level =((BS2UserOperatorEnum)item.level).ToString();
                        leve.Add(lv);
                        curOperatorLevelObj = (IntPtr)((long)curOperatorLevelObj + structSize);
                    }
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = "Successful";
                    respUser.code = Codes.SUCCESS;
                    respUser.Results =leve;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }
                else
                {
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = "There is no auth operator level in the device.";
                    respUser.code = Codes.ERROR;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }

                //if (operatorlevelObj != IntPtr.Zero)
                   
            }
        }

        public resLevelModel getAuthOperatorLevelExIP(reqDevicenIDConnection reqDevice)
        {
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = reqDevice.DeviceIP,
                DevicePort = reqDevice.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            string userID = reqDevice.UserID;
            if (userID.Length == 0)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "The user id can not be empty.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                return respUser;
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(userID);

                int structSize = BS2Environment.BS2_USER_ID_SIZE;
                byte[] userIDBuf = new byte[structSize];
                IntPtr userIDObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curUserIDObj = userIDObj;
                foreach (string strUserID in userIDs)
                {
                    Array.Clear(userIDBuf, 0, userIDBuf.Length);
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Copy(userIDArray, userIDBuf, Math.Min(userIDArray.Length, userIDBuf.Length));
                    Marshal.Copy(userIDBuf, 0, curUserIDObj, userIDBuf.Length);
                    curUserIDObj = (IntPtr)((long)curUserIDObj + structSize);
                }

                IntPtr operatorlevelObj = IntPtr.Zero;
                UInt32 numOperatorlevel = 0;

                BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetAuthOperatorLevelEx(sdkContext, deviceID, userIDObj, (UInt32)userIDs.Count, out operatorlevelObj, out numOperatorlevel);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = ((BS2ErrorCode)result).ToString();
                    respUser.code = Codes.ERROR;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }
                else if (numOperatorlevel > 0)
                {
                    IntPtr curOperatorLevelObj = operatorlevelObj;
                    structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));

                    List<LevelModel> leve = new List<LevelModel>();
                    for (int idx = 0; idx < numOperatorlevel; ++idx)
                    {
                        LevelModel lv = new LevelModel();
                        BS2AuthOperatorLevel item = (BS2AuthOperatorLevel)Marshal.PtrToStructure(curOperatorLevelObj, typeof(BS2AuthOperatorLevel));
                        lv.UserID = Encoding.UTF8.GetString(item.userID).TrimEnd('\0');
                        lv.Level = ((BS2UserOperatorEnum)item.level).ToString();
                        leve.Add(lv);
                        curOperatorLevelObj = (IntPtr)((long)curOperatorLevelObj + structSize);
                    }
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = "Successful";
                    respUser.code = Codes.SUCCESS;
                    respUser.Results = leve;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }
                else
                {
                    resLevelModel respUser = new resLevelModel();
                    respUser.message = "There is no auth operator level in the device.";
                    respUser.code = Codes.ERROR;
                    ClearSDK();
                    API.BS2_ReleaseObject(operatorlevelObj);
                    Marshal.FreeHGlobal(userIDObj);
                    return respUser;
                }

                //if (operatorlevelObj != IntPtr.Zero)

            }
        }

        public resLevelModel getAllAuthOperatorLevelEx(ref UInt32 deviceID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            IntPtr operatorlevelObj = IntPtr.Zero;
            UInt32 numOperatorlevel = 0;

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetAllAuthOperatorLevelEx(sdkContext, deviceID, out operatorlevelObj, out numOperatorlevel);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.ERROR;
                ClearSDK();
                API.BS2_ReleaseObject(operatorlevelObj);
                return respUser;
            }
            else if (numOperatorlevel > 0)
            {
                IntPtr curOperatorLevelObj = operatorlevelObj;
                int structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));
                List<LevelModel> leve = new List<LevelModel>();
                for (int idx = 0; idx < numOperatorlevel; ++idx)
                {
                    LevelModel lv = new LevelModel();
                    BS2AuthOperatorLevel item = (BS2AuthOperatorLevel)Marshal.PtrToStructure(curOperatorLevelObj, typeof(BS2AuthOperatorLevel));
                    lv.UserID = Encoding.UTF8.GetString(item.userID).TrimEnd('\0');
                    lv.Level = ((BS2UserOperatorEnum)item.level).ToString();
                    leve.Add(lv);
                    curOperatorLevelObj = (IntPtr)((long)curOperatorLevelObj + structSize);
                }
                resLevelModel respUser = new resLevelModel();
                respUser.message = "Successful";
                respUser.code = Codes.SUCCESS;
                respUser.Results = leve;
                ClearSDK();
                return respUser;
            }
            else
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "There is no auth operator level ex in the device.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                API.BS2_ReleaseObject(operatorlevelObj);
                return respUser;
            } 
        }

        public resLevelModel getAllAuthOperatorLevelExIP(reqDeviceConnection reqDevice)
        {
            UInt32 deviceID = 0;

            ConnectToDeviceUnit(ref deviceID, reqDevice);
            IntPtr operatorlevelObj = IntPtr.Zero;
            UInt32 numOperatorlevel = 0;

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetAllAuthOperatorLevelEx(sdkContext, deviceID, out operatorlevelObj, out numOperatorlevel);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = ((BS2ErrorCode)result).ToString();
                respUser.code = Codes.ERROR;
                ClearSDK();
                API.BS2_ReleaseObject(operatorlevelObj);
                return respUser;
            }
            else if (numOperatorlevel > 0)
            {
                IntPtr curOperatorLevelObj = operatorlevelObj;
                int structSize = Marshal.SizeOf(typeof(BS2AuthOperatorLevel));
                List<LevelModel> leve = new List<LevelModel>();
                for (int idx = 0; idx < numOperatorlevel; ++idx)
                {
                    LevelModel lv = new LevelModel();
                    BS2AuthOperatorLevel item = (BS2AuthOperatorLevel)Marshal.PtrToStructure(curOperatorLevelObj, typeof(BS2AuthOperatorLevel));
                    lv.UserID = Encoding.UTF8.GetString(item.userID).TrimEnd('\0');
                    lv.Level = ((BS2UserOperatorEnum)item.level).ToString();
                    leve.Add(lv);
                    curOperatorLevelObj = (IntPtr)((long)curOperatorLevelObj + structSize);
                }
                resLevelModel respUser = new resLevelModel();
                respUser.message = "Successful";
                respUser.code = Codes.SUCCESS;
                respUser.Results = leve;
                ClearSDK();
                return respUser;
            }
            else
            {
                resLevelModel respUser = new resLevelModel();
                respUser.message = "There is no auth operator level ex in the device.";
                respUser.code = Codes.ERROR;
                ClearSDK();
                API.BS2_ReleaseObject(operatorlevelObj);
                return respUser;
            }
        }

        public string delAuthOperatorLevelEx(ref UInt32 deviceID, string UserID)
        {
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            string userID = UserID;
            if (userID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than "+ BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(userID);

                int structSize = BS2Environment.BS2_USER_ID_SIZE;
                byte[] userIDBuf = new byte[structSize];
                IntPtr userIDObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curUserIDObj = userIDObj;
                foreach (string strUserID in userIDs)
                {
                    Array.Clear(userIDBuf, 0, userIDBuf.Length);
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Copy(userIDArray, userIDBuf, Math.Min(userIDArray.Length, userIDBuf.Length));
                    Marshal.Copy(userIDBuf, 0, curUserIDObj, userIDBuf.Length);
                    curUserIDObj = (IntPtr)((long)curUserIDObj + structSize);
                }

                BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveAuthOperatorLevelEx(sdkContext, deviceID, userIDObj, (UInt32)userIDs.Count);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    ClearSDK();
                    Marshal.FreeHGlobal(userIDObj);
                    return ((BS2ErrorCode)result).ToString();
                }
                else
                {
                    ClearSDK();
                    Marshal.FreeHGlobal(userIDObj);
                    return "Successful";
                }
                
            }
        }

        public string delAuthOperatorLevelExIP(reqDevicenIDConnection reqDevice)
        {
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = reqDevice.DeviceIP,
                DevicePort = reqDevice.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            string userID = reqDevice.UserID;
            if (userID.Length == 0)
            {
                ClearSDK();
                return "The user id can not be empty.";
            }
            else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
            {
                ClearSDK();
                return "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
            }
            else
            {
                List<string> userIDs = new List<string>();
                userIDs.Add(userID);

                int structSize = BS2Environment.BS2_USER_ID_SIZE;
                byte[] userIDBuf = new byte[structSize];
                IntPtr userIDObj = Marshal.AllocHGlobal(structSize * userIDs.Count);
                IntPtr curUserIDObj = userIDObj;
                foreach (string strUserID in userIDs)
                {
                    Array.Clear(userIDBuf, 0, userIDBuf.Length);
                    byte[] userIDArray = Encoding.UTF8.GetBytes(strUserID);
                    Array.Copy(userIDArray, userIDBuf, Math.Min(userIDArray.Length, userIDBuf.Length));
                    Marshal.Copy(userIDBuf, 0, curUserIDObj, userIDBuf.Length);
                    curUserIDObj = (IntPtr)((long)curUserIDObj + structSize);
                }

                BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveAuthOperatorLevelEx(sdkContext, deviceID, userIDObj, (UInt32)userIDs.Count);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    ClearSDK();
                    Marshal.FreeHGlobal(userIDObj);
                    return ((BS2ErrorCode)result).ToString();
                }
                else
                {
                    ClearSDK();
                    Marshal.FreeHGlobal(userIDObj);
                    return "Successful";
                }

            }
        }
       
        protected BS2SimpleDeviceInfo deviceInfo;

        public string InsertUserDetailsDeviceID(ref UInt32 deviceID, reqUserDetailsModel rqdetails)
        {
            ClearSDK();
            string results="";
            ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            //bool pinSupported = Convert.ToBoolean(deviceInfo.pinSupported);
            //bool nameSupported = Convert.ToBoolean(deviceInfo.userNameSupported);
              bool nameSupported = true ;
            //bool cardSupported = Convert.ToBoolean(deviceInfo.cardSupported);
            //bool fingerScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FINGER_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FINGER_SCAN;
            //bool faceScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_SCAN;
            //bool faceExScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_EX_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_EX_SCAN;

            BS2UserFaceExBlob[] userBlob = Util.AllocateStructureArray<BS2UserFaceExBlob>(1);
            userBlob[0].cardObjs = IntPtr.Zero;
            userBlob[0].fingerObjs = IntPtr.Zero;
            userBlob[0].faceObjs = IntPtr.Zero;
            userBlob[0].user_photo_obj = IntPtr.Zero;
            userBlob[0].faceExObjs = IntPtr.Zero;

            BS2ErrorCode sdkResult = BS2ErrorCode.BS_SDK_SUCCESS;
            for (int i = 0; i < rqdetails.UsersRegDetails.Count; i++) 
            {
                string userID = rqdetails.UsersRegDetails[i].UserID;
                if (userID.Length == 0)
                {
                    results= "The user id can not be empty.";
                }
                else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
                {
                    results = "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
                }
                else
                {
                    //TODO Alphabet user id is not implemented yet.
                    UInt32 uid;
                    if (!UInt32.TryParse(userID, out uid))
                    {
                        results = "The user id should be a numeric.";
                    }

                    byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
                    Array.Clear(userBlob[0].user.userID, 0, BS2Environment.BS2_USER_ID_SIZE);
                    Array.Copy(userIDArray, userBlob[0].user.userID, userIDArray.Length);
                }

                Array.Clear(userBlob[0].name, 0, BS2Environment.BS2_USER_NAME_LEN);

                if (nameSupported)
                {
                    string name = rqdetails.UsersRegDetails[i].UserName;
                    if (name.Length > BS2Environment.BS2_USER_NAME_LEN)
                    {
                        results = "The user name should less than " + BS2Environment.BS2_USER_NAME_LEN + " words.";
                    }
                    else
                    {
                        byte[] nameArray = Encoding.UTF8.GetBytes(name);
                        Array.Copy(nameArray, userBlob[0].name, nameArray.Length);

                        var myDate = DateTime.Now;
                        var newDate = myDate.AddYears(40);

                        userBlob[0].setting.startTime = Convert.ToUInt32(Util.ConvertToUnixTimestamp(DateTime.Now));
                        userBlob[0].setting.endTime = Convert.ToUInt32(Util.ConvertToUnixTimestamp(newDate));

                        Array.Clear(userBlob[0].pin, 0, BS2Environment.BS2_PIN_HASH_SIZE);
                        userBlob[0].setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;
                        userBlob[0].setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
                        userBlob[0].setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;

                        userBlob[0].settingEx.faceAuthMode = (byte)BS2ExtFaceAuthModeEnum.NONE;
                        userBlob[0].settingEx.fingerprintAuthMode = (byte)BS2ExtFingerprintAuthModeEnum.NONE;
                        userBlob[0].settingEx.cardAuthMode = (byte)BS2ExtCardAuthModeEnum.NONE;
                        userBlob[0].settingEx.idAuthMode = (byte)BS2ExtIDAuthModeEnum.NONE;

                        Array.Clear(userBlob[0].accessGroupId, 0, BS2Environment.BS2_MAX_ACCESS_GROUP_PER_USER);
                        userBlob[0].user.numCards = 0;
                        userBlob[0].user.numFingers = 0;
                        userBlob[0].user.numFaces = 0;

                        bool unwarpedMemory = false;

                        sdkResult = (BS2ErrorCode)API.BS2_EnrollUserFaceEx(sdkContext, deviceID, userBlob, 1, 1);
                        if (BS2ErrorCode.BS_SDK_SUCCESS != sdkResult)
                        {
                            results = ((BS2ErrorCode)sdkResult).ToString();
                        }
                        else
                        {
                            results = "Successful";
                        }
                    }
                }                
            }
            ClearSDK();
            return results;
        }

        public string InsertUserDetailsIP(reqUserDetailsIPModel rqdetails)
        {
            ClearSDK();
            string results = "";
            UInt32 deviceID = 0;
            reqDeviceConnection reqd = new reqDeviceConnection
            {
                DeviceIP = rqdetails.DeviceIP,
                DevicePort = rqdetails.DevicePort
            };

            ConnectToDeviceUnit(ref deviceID, reqd);
            //bool pinSupported = Convert.ToBoolean(deviceInfo.pinSupported);
            //bool nameSupported = Convert.ToBoolean(deviceInfo.userNameSupported);
            bool nameSupported = true;
            //bool cardSupported = Convert.ToBoolean(deviceInfo.cardSupported);
            //bool fingerScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FINGER_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FINGER_SCAN;
            //bool faceScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_SCAN;
            //bool faceExScanSupported = (deviceInfoEx.supported & (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_EX_SCAN) == (UInt32)BS2SupportedInfoMask.BS2_SUPPORT_FACE_EX_SCAN;

            BS2UserFaceExBlob[] userBlob = Util.AllocateStructureArray<BS2UserFaceExBlob>(1);
            userBlob[0].cardObjs = IntPtr.Zero;
            userBlob[0].fingerObjs = IntPtr.Zero;
            userBlob[0].faceObjs = IntPtr.Zero;
            userBlob[0].user_photo_obj = IntPtr.Zero;
            userBlob[0].faceExObjs = IntPtr.Zero;

            BS2ErrorCode sdkResult = BS2ErrorCode.BS_SDK_SUCCESS;
            for (int i = 0; i < rqdetails.UsersRegDetails.Count; i++)
            {
                string userID = rqdetails.UsersRegDetails[i].UserID;
                if (userID.Length == 0)
                {
                    results = "The user id can not be empty.";
                }
                else if (userID.Length > BS2Environment.BS2_USER_ID_SIZE)
                {
                    results = "The user id should less than " + BS2Environment.BS2_USER_ID_SIZE + " words.";
                }
                else
                {
                    //TODO Alphabet user id is not implemented yet.
                    UInt32 uid;
                    if (!UInt32.TryParse(userID, out uid))
                    {
                        results = "The user id should be a numeric.";
                    }

                    byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
                    Array.Clear(userBlob[0].user.userID, 0, BS2Environment.BS2_USER_ID_SIZE);
                    Array.Copy(userIDArray, userBlob[0].user.userID, userIDArray.Length);
                }

                Array.Clear(userBlob[0].name, 0, BS2Environment.BS2_USER_NAME_LEN);

                if (nameSupported)
                {
                    string name = rqdetails.UsersRegDetails[i].UserName;
                    if (name.Length > BS2Environment.BS2_USER_NAME_LEN)
                    {
                        results = "The user name should less than " + BS2Environment.BS2_USER_NAME_LEN + " words.";
                    }
                    else
                    {
                        byte[] nameArray = Encoding.UTF8.GetBytes(name);
                        Array.Copy(nameArray, userBlob[0].name, nameArray.Length);

                        //string EndTime = "2100-12-31 06:60:59";
                        //DateTime EndTimeDate = Convert.ToDateTime(EndTime);

                        var myDate = DateTime.Now;
                        var newDate = myDate.AddYears(40);

                        userBlob[0].setting.startTime = Convert.ToUInt32(Util.ConvertToUnixTimestamp(DateTime.Now));
                        userBlob[0].setting.endTime = Convert.ToUInt32(Util.ConvertToUnixTimestamp(newDate));

                        Array.Clear(userBlob[0].pin, 0, BS2Environment.BS2_PIN_HASH_SIZE);
                        userBlob[0].setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;
                        userBlob[0].setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
                        userBlob[0].setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;

                        userBlob[0].settingEx.faceAuthMode = (byte)BS2ExtFaceAuthModeEnum.NONE;
                        userBlob[0].settingEx.fingerprintAuthMode = (byte)BS2ExtFingerprintAuthModeEnum.NONE;
                        userBlob[0].settingEx.cardAuthMode = (byte)BS2ExtCardAuthModeEnum.NONE;
                        userBlob[0].settingEx.idAuthMode = (byte)BS2ExtIDAuthModeEnum.NONE;

                        Array.Clear(userBlob[0].accessGroupId, 0, BS2Environment.BS2_MAX_ACCESS_GROUP_PER_USER);
                        userBlob[0].user.numCards = 0;
                        userBlob[0].user.numFingers = 0;
                        userBlob[0].user.numFaces = 0;

                        bool unwarpedMemory = false;

                        sdkResult = (BS2ErrorCode)API.BS2_EnrollUserFaceEx(sdkContext, deviceID, userBlob, 1, 1);
                        if (BS2ErrorCode.BS_SDK_SUCCESS != sdkResult)
                        {
                            results = ((BS2ErrorCode)sdkResult).ToString();
                        }
                        else
                        {
                            results = "Successful";
                        }
                    }
                }
            }
            ClearSDK();
            return results;
        }


        public Bitmap getimage(byte[] arr, int width, int height, int imageFormat)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

            BitmapData bmpData = bitmap.LockBits(
                                 new Rectangle(0, 0, width, height),
                                 ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);

            IntPtr p = bmpData.Scan0;

            Marshal.Copy(arr, 0, bmpData.Scan0, arr.Length);
            bitmap.UnlockBits(bmpData);

            ColorPalette pal = bitmap.Palette;

            for (int i = 0; i < 256; i++)
            {
                pal.Entries[i] = Color.FromArgb(255, i, i, i);
            }

            bitmap.Palette = pal;

            var crop = new Rectangle(0, 0, width / 4, height / 4);
            Bitmap nb = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(nb);
            g.DrawImage(bitmap, -crop.X, -crop.Y);
            bitmap = nb;
            bitmap.SetResolution(500, 500);
            bitmap.Save("test.Jpeg", ImageFormat.Jpeg);

            return bitmap;
        }

        public void runWithoutConnection()
        {
            UInt32 deviceID = 0;
            IntPtr versionPtr = API.BS2_Version();
            //bool bSsl = false;

            if (title.Length > 0)
            {
                Console.Title = title;
            }

            Console.WriteLine("SDK version : " + Marshal.PtrToStringAnsi(versionPtr));

            sdkContext = API.BS2_AllocateContext();
            if (sdkContext == IntPtr.Zero)
            {
                Console.WriteLine("Can't allocate sdk context.");
                return;
            }

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SDK initialization failed with : {0}", result);
                API.BS2_ReleaseContext(sdkContext);
                sdkContext = IntPtr.Zero;
                return;
            }

            runImpl(deviceID);

            API.BS2_ReleaseContext(sdkContext);

          
            // sdkContext = IntPtr.Zero;

            cbOnDeviceFound = null;
            cbOnDeviceAccepted = null;
            cbOnDeviceConnected = null;
            cbOnDeviceDisconnected = null;
            //cbOnSendRootCA = null;
        }

        bool SearchAndConnectDevice(ref UInt32 deviceID)
        {
            Console.WriteLine("Trying to broadcast on the network");           

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevices(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error : {0}.", result);
                return false;
            }

            IntPtr deviceListObj = IntPtr.Zero;
            UInt32 numDevice = 0;

            const UInt32 LONG_TIME_STANDBY_7S = 7;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_7S);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                Console.WriteLine("Got error : {0}.", result);
                return false;
            }

            result = (BS2ErrorCode)API.BS2_GetDevices(sdkContext, out deviceListObj, out numDevice);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error : {0}.", result);
                return false;
            }

            if (numDevice > 0)
            {
                BS2SimpleDeviceInfo deviceInfo;

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));
                    result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Can't get device information(errorCode : {0}).", result);
                        return false;
                    }
                    Console.WriteLine("[{0, 3:##0}] ==> ID[{1, 10}] Type[{2, 20}] Connection mode[{3}] Ip[{4, 16}] port[{5, 5}] Master/Slave[{6}]",
                            idx,
                            deviceID,                            
                            API.productNameDictionary.ContainsKey((BS2DeviceTypeEnum)deviceInfo.type) ?  API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type] : (API.productNameDictionary[BS2DeviceTypeEnum.UNKNOWN] + "(" + deviceInfo.type + ")"),
                            (BS2ConnectionModeEnum)deviceInfo.connectionMode,
                            new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString(),
                            deviceInfo.port, deviceInfo.rs485Mode);
                }
                deviceID = 0;
                Int32 selection = Util.GetInput();
                if (selection >= 0)
                {
                    if (selection < numDevice)
                    {
                        deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)selection * sizeof(UInt32)));
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection[{0}]", selection);
                    }
                }

                API.BS2_ReleaseObject(deviceListObj);
                if (deviceID > 0)
                {
                    Console.WriteLine("Trying to connect to device[{0}]", deviceID);                    
                    result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);                    

                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                        return false;
                    }

                    Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("There is no device to launch.");
            }

            return false;
        }

        bool ConnectToDeviceSSL(string deviceIpAddress, ref UInt32 deviceID)
        {                
            UInt16 port = Util.GetInput((UInt16)BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT);

            int nCnt = 0;
            while (true)
            {
                IntPtr ptrIPAddr = Marshal.StringToHGlobalAnsi(deviceIpAddress);
                //BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, deviceIpAddress, port, out deviceID);
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ptrIPAddr, port, out deviceID);
                Marshal.FreeHGlobal(ptrIPAddr);

                if(nCnt > 7)
                {
                    Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                    return false;
                }

                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    nCnt++;
                    continue;
                }
                else
                    break;
            }

            Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
            return true;
        }       

        void PrintDeviceInfo(BS2SimpleDeviceInfo deviceInfo)
        {
            Console.WriteLine("                        <Device information>");
            Console.WriteLine("+-------------------------------------------------------------+");
            Console.WriteLine("|  ID                                : {0}", deviceInfo.id);
            Console.WriteLine("|  Type                              : {0}({1})", API.productNameDictionary.ContainsKey((BS2DeviceTypeEnum)deviceInfo.type) ? API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type] : API.productNameDictionary[BS2DeviceTypeEnum.UNKNOWN], deviceInfo.type);
            Console.WriteLine("|  Connection mode                   : {0}", (BS2ConnectionModeEnum)deviceInfo.connectionMode);
            Console.WriteLine("|  Ip address                        : {0}", new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString());
            Console.WriteLine("|  Port number                       : {0}", deviceInfo.port);
            Console.WriteLine("|  Maximum user                      : {0}", deviceInfo.maxNumOfUser);
            Console.WriteLine("|  Supporting user name              : {0}", Convert.ToBoolean(deviceInfo.userNameSupported));
            Console.WriteLine("|  Supporting user profile           : {0}", Convert.ToBoolean(deviceInfo.userPhotoSupported));
            Console.WriteLine("|  Supporting pin code               : {0}", Convert.ToBoolean(deviceInfo.pinSupported));
            Console.WriteLine("|  Supporting card                   : {0}", Convert.ToBoolean(deviceInfo.cardSupported));
            Console.WriteLine("|  Supporting fingerprint            : {0}", Convert.ToBoolean(deviceInfo.fingerSupported));
            Console.WriteLine("|  Supporting face recognition       : {0}", Convert.ToBoolean(deviceInfo.faceSupported));
            Console.WriteLine("|  Supporting wlan                   : {0}", Convert.ToBoolean(deviceInfo.wlanSupported));
            Console.WriteLine("|  Supporting T&A                    : {0}", Convert.ToBoolean(deviceInfo.tnaSupported));
            Console.WriteLine("|  Supporting trigger action         : {0}", Convert.ToBoolean(deviceInfo.triggerActionSupported));
            Console.WriteLine("|  Supporting wiegand                : {0}", Convert.ToBoolean(deviceInfo.wiegandSupported));
            Console.WriteLine("+-------------------------------------------------------------+");
        }

        void DeviceFound(UInt32 deviceID)
        {
            Console.WriteLine("[CB] Device[{0, 10}] has been found.", deviceID);
        }

        void DeviceAccepted(UInt32 deviceID)
        {
            Console.WriteLine("[CB] Device[{0, 10}] has been accepted.", deviceID);
            deviceIDForServerMode = deviceID;
            eventWaitHandle.Set();
        }

        void DeviceConnected(UInt32 deviceID)
        {
            Console.WriteLine("[CB] Device[{0, 10}] has been connected.", deviceID);
        }

        void DeviceDisconnected(UInt32 deviceID)
        {
            Console.WriteLine("[CB] Device[{0, 10}] has been disconnected.", deviceID);
#if !SDK_AUTO_CONNECTION
            if (reconnectionTask != null)
            {
                Console.WriteLine("enqueue");
                reconnectionTask.enqueue(deviceID);
            }
#endif
        }

        UInt32 PreferMethodHandle(UInt32 deviceID)
        {
            return (UInt32)(BS2SslMethodMaskEnum.TLS1 | BS2SslMethodMaskEnum.TLS1_1 | BS2SslMethodMaskEnum.TLS1_2);
        }

        IntPtr GetRootCaFilePathHandle(UInt32 deviceID)
        {
            //return ssl_server_root_crt;
            if (ptr_server_root_crt == IntPtr.Zero)
                ptr_server_root_crt = Marshal.StringToHGlobalAnsi(ssl_server_root_crt);
            return ptr_server_root_crt;
        }

        IntPtr GetServerCaFilePathHandle(UInt32 deviceID)
        {
            //return ssl_server_crt;
            if (ptr_server_crt == IntPtr.Zero)
                ptr_server_crt = Marshal.StringToHGlobalAnsi(ssl_server_crt);
            return ptr_server_crt;
        }

        IntPtr GetServerPrivateKeyFilePathHandle(UInt32 deviceID)
        {
            //return ssl_server_pem;
            if (ptr_server_pem == IntPtr.Zero)
                ptr_server_pem = Marshal.StringToHGlobalAnsi(ssl_server_pem);
            return ptr_server_pem;
        }

        IntPtr GetPasswordHandle(UInt32 deviceID)
        {
            //return ssl_server_passwd;
            if (ptr_server_passwd == IntPtr.Zero)
                ptr_server_passwd = Marshal.StringToHGlobalAnsi(ssl_server_passwd);
            return ptr_server_passwd;
        }

        void OnErrorOccuredHandle(UInt32 deviceID, int errCode)
        {
            Console.WriteLine("Got ssl error{0} Device[{1, 10}].", (BS2ErrorCode)errCode, deviceID);
        }

        void SendRootCA(int result)
        {
            if (result == 1)
                Console.WriteLine("send RootCA Success!!\n");
            else
                Console.WriteLine("send RootCA Fail!!\n");
            
        }

        private void DebugExPrint(UInt32 level, UInt32 module, string msg)
        {
            //string printmsg = String.Format("[{0}-{1}] {2}", getModuleName(module), getLevelName(level), msg);
            string printmsg = String.Format("{0}", msg);
            //Trace.WriteLine(printmsg);
            Console.WriteLine(printmsg);
        }

        private string getModuleName(UInt32 module)
        {
            switch (module)
            {
            case Constants.DEBUG_MODULE_KEEP_ALIVE:         return "KAV";
            case Constants.DEBUG_MODULE_SOCKET_MANAGER:     return "SOM";
            case Constants.DEBUG_MODULE_SOCKET_HANDLER:     return "SOH";
            case Constants.DEBUG_MODULE_DEVICE:             return "DEV";
            case Constants.DEBUG_MODULE_DEVICE_MANAGER:     return "DVM";
            case Constants.DEBUG_MODULE_EVENT_DISPATCHER:   return "DIS";
            case Constants.DEBUG_MODULE_API:                return "API";
            case Constants.DEBUG_MODULE_ALL:                return "ALL";
            }

            return "UnK";
        }

        private string getLevelName(UInt32 level)
        {
            switch (level)
            {
                case Constants.DEBUG_LOG_FATAL:             return "FAT";
                case Constants.DEBUG_LOG_ERROR:             return "ERR";
                case Constants.DEBUG_LOG_WARN:              return "WRN";
                case Constants.DEBUG_LOG_INFO:              return "INF";
                case Constants.DEBUG_LOG_TRACE:             return "TRC";
                case Constants.DEBUG_LOG_OPERATION_ALL:     return "OPR";
                case Constants.DEBUG_LOG_ALL:               return "ALL";
            }

            return "UnK";
        }


        private string ToStringYesNo(bool value)
        {
            return value ? "y" : "n";
        }

        public bool noConnectionMode = false;
        public void runWithIPv6()
        {            
            int delayTerminate = 0;
            UInt32 deviceID = 0;
            IntPtr versionPtr = API.BS2_Version();
            //bool bSsl = false;
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            if (title.Length > 0)
            {
                Console.Title = title;
            }

            Console.WriteLine("SDK version : " + Marshal.PtrToStringAnsi(versionPtr));

            cbDebugExPrint = null;
            Console.WriteLine("Do you want print debug message? [y/N]");
            Console.Write(">>>> ");
            if (!Util.IsNo())
            {
                cbDebugExPrint = new API.CBDebugExPrint(DebugExPrint);
                result = (BS2ErrorCode)API.BS2_SetDebugExCallback(cbDebugExPrint, Constants.DEBUG_LOG_OPERATION_ALL, Constants.DEBUG_MODULE_ALL);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("SetDebugExCallback: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
            }

            sdkContext = API.BS2_AllocateContext();
            if (sdkContext == IntPtr.Zero)
            {
                Console.WriteLine("Can't allocate sdk context.");
                ClearSDK(delayTerminate);
                return;
            }

            Int32 responseTimeoutMs = 0;
            result = (BS2ErrorCode)API.BS2_GetDefaultResponseTimeout(sdkContext, out responseTimeoutMs);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetDefaultResponseTimeout: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
            }
            Console.WriteLine("How long do you have to wait by default for response time? [{0} ms (Default)]", responseTimeoutMs);
            Console.Write(">>>> ");
            responseTimeoutMs = (Int32)(Util.GetInput((UInt32)responseTimeoutMs));
            result = (BS2ErrorCode)API.BS2_SetDefaultResponseTimeout(sdkContext, responseTimeoutMs);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SetDefaultResponseTimeout: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
            }


            int IPv4 = 1;
            int IPv6 = 0;
            result = (BS2ErrorCode)API.BS2_GetEnableIPV4(sdkContext, out IPv4);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetEnableIPV4: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
            }
            result = (BS2ErrorCode)API.BS2_GetEnableIPV6(sdkContext, out IPv6);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetEnableIPV4: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
            }

            while (true) 
            { 
                Console.WriteLine("What do you want to be active between IPv4 and IPv6? [0(IPv4: Default), 1(IPv6), 2(Both)]");
                Console.Write(">>>> ");
                byte choiceIP = Util.GetInput((byte)0);
                if (choiceIP == 0)
                {
                    IPv4 = 1;
                    IPv6 = 0;
                }
                else if (choiceIP == 1)
                {
                    IPv4 = 0;
                    IPv6 = 1;
                }
                else if (choiceIP == 2)
                {
                    IPv4 = 1;
                    IPv6 = 1;
                }
                else
                {
                    Console.WriteLine("Wrong selection");
                    continue;
                }
                break;
            }

            if (IPv4 == 1)
            {
                UInt16 port = BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT;
                result = (BS2ErrorCode)API.BS2_GetServerPort(sdkContext, out port);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetServerPort: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }

                Console.WriteLine("What server port number will you use in IPv4? [{0} Default]", port);
                Console.Write(">>>> ");
                port = Util.GetInput((UInt16)port);
                result = (BS2ErrorCode)API.BS2_SetServerPort(sdkContext, port);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("SetServerPort: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
            }

            if (IPv6 == 1)
            {
                UInt16 port = BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT_V6;
                result = (BS2ErrorCode)API.BS2_GetServerPortIPV6(sdkContext, out port);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetServerPortIPV6: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }

                Console.WriteLine("What server port number will you use in IPv6? [{0} Default]", port);
                Console.Write(">>>> ");
                port = Util.GetInput((UInt16)port);
                result = (BS2ErrorCode)API.BS2_SetServerPortIPV6(sdkContext, port);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("SetServerPortIPV6: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
            }

	        result = (BS2ErrorCode)API.BS2_SetEnableIPV4(sdkContext, IPv4);
	        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
	        {
	            Console.WriteLine("SetEnableIPV4: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
	        }

	        result = (BS2ErrorCode)API.BS2_SetEnableIPV6(sdkContext, IPv6);
	        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
	        {
	            Console.WriteLine("SetEnableIPV6: Got error({0}).", result);
                ClearSDK(delayTerminate);
                return;
	        }

            Console.WriteLine("Do you want to set up ssl configuration? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                if (IPv4 == 1)
                {
                    UInt16 sslPort = BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT;
                    result = (BS2ErrorCode)API.BS2_GetSSLServerPort(sdkContext, out sslPort);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("GetSSLServerPort: Got error({0}).", result);
                        ClearSDK(delayTerminate);
                        return;
                    }

                    Console.WriteLine("What ssl server port number will you use in IPv4? [{0} Default]", sslPort);
                    Console.Write(">>>> ");
                    sslPort = Util.GetInput((UInt16)sslPort);
                    result = (BS2ErrorCode)API.BS2_SetSSLServerPort(sdkContext, sslPort);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("SetSSLServerPort: Got error({0}).", result);
                        ClearSDK(delayTerminate);
                        return;
                    }
                }

                if (IPv6 == 1)
                {
                    UInt16 sslPort = BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT_V6;
                    result = (BS2ErrorCode)API.BS2_GetSSLServerPortIPV6(sdkContext, out sslPort);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("GetSSLServerPortIPV6: Got error({0}).", result);
                        ClearSDK(delayTerminate);
                        return;
                    }

                    Console.WriteLine("What ssl server port number will you use in IPv6? [{0} Default]", sslPort);
                    Console.Write(">>>> ");
                    sslPort = Util.GetInput((UInt16)sslPort);
                    result = (BS2ErrorCode)API.BS2_SetSSLServerPortIPV6(sdkContext, sslPort);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("SetSSLServerPortIPV6: Got error({0}).", result);
                        ClearSDK(delayTerminate);
                        return;
                    }
                }

                cbPreferMethod = new API.PreferMethod(PreferMethodHandle);
                cbGetRootCaFilePath = new API.GetRootCaFilePath(GetRootCaFilePathHandle);
                cbGetServerCaFilePath = new API.GetServerCaFilePath(GetServerCaFilePathHandle);
                cbGetServerPrivateKeyFilePath = new API.GetServerPrivateKeyFilePath(GetServerPrivateKeyFilePathHandle);
                cbGetPassword = new API.GetPassword(GetPasswordHandle);
                cbOnErrorOccured = new API.OnErrorOccured(OnErrorOccuredHandle);
                //ServicePointManager.SecurityProtocol = (SecurityProtocolType)SecurityProtocolType.Ssl3;

                result = (BS2ErrorCode)API.BS2_SetSSLHandler(sdkContext, cbPreferMethod, cbGetRootCaFilePath, cbGetServerCaFilePath, cbGetServerPrivateKeyFilePath, cbGetPassword, null);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("SetSSLHandler: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
                else
                {
                    //bSsl = true;
                }
            }

            if (IPv4 == 1)
            { 
                UInt16 serverPort = 0;
                result = (BS2ErrorCode)API.BS2_GetServerPort(sdkContext, out serverPort);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetServerPort: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
                Console.WriteLine("Server Port on IPv4: {0}", serverPort);

                UInt16 sslServerPort = 0;
                result = (BS2ErrorCode)API.BS2_GetSSLServerPort(sdkContext, out sslServerPort);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetSSLServerPort: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
                Console.WriteLine("SSL Server Port on IPv4: {0}", sslServerPort);
            }

            if (IPv6 == 1)
            {
                UInt16 serverPort = 0;
                result = (BS2ErrorCode)API.BS2_GetServerPortIPV6(sdkContext, out serverPort);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetServerPortIPV6: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
                Console.WriteLine("Server Port on IPv6: {0}", serverPort);

                UInt16 sslServerPort = 0;
                result = (BS2ErrorCode)API.BS2_GetSSLServerPortIPV6(sdkContext, out sslServerPort);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("GetSSLServerPort: Got error({0}).", result);
                    ClearSDK(delayTerminate);
                    return;
                }
                Console.WriteLine("SSL Server Port on IPv6: {0}", sslServerPort);
            }

            result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SDK initialization failed with : {0}", result);
                ClearSDK(delayTerminate);
                return;
            }

            cbOnDeviceFound = new API.OnDeviceFound(DeviceFound);
            cbOnDeviceAccepted = new API.OnDeviceAccepted(DeviceAccepted);
            cbOnDeviceConnected = new API.OnDeviceConnected(DeviceConnected);
            cbOnDeviceDisconnected = new API.OnDeviceDisconnected(DeviceDisconnected);

            result = (BS2ErrorCode)API.BS2_SetDeviceEventListener(sdkContext,
                                                                cbOnDeviceFound,
                                                                cbOnDeviceAccepted,
                                                                cbOnDeviceConnected,
                                                                cbOnDeviceDisconnected);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Can't register a callback function/method to a sdk.({0})", result);
                ClearSDK(delayTerminate);
                return;
            }

            /*
            if (bSsl)
            {
                cbOnSendRootCA = new API.OnSendRootCA(SendRootCA);
                result = (BS2ErrorCode)API.BS2_SetDeviceSSLEventListener(sdkContext, cbOnSendRootCA);
            }
            */

#if SDK_AUTO_CONNECTION
            result = (BS2ErrorCode)API.BS2_SetAutoConnection(sdkContext, 1);
#endif

            noConnectionMode = false;
            do
            {
                Console.WriteLine("+-----------------------------------------------------------+");
                Console.WriteLine("| 1. Search and connect device                              |");
                Console.WriteLine("| 2. Connect to device via Ip                               |");
                Console.WriteLine("| 3. Server mode test                                       |");
                Console.WriteLine("| 4. Get IP Config via UDP                                  |");
                Console.WriteLine("| 5. Set IP Config via UDP                                  |");
                Console.WriteLine("| 6. Get IPV6 Config via UDP                                |");
                Console.WriteLine("| 7. Set IPV6 Config via UDP                                |");
                Console.WriteLine("| 8. No Connection for USB                                  |");
                Console.WriteLine("+-----------------------------------------------------------+");
                Console.WriteLine("How to connect to device? [2(default)]");
                Console.Write(">>>> ");
                int selection = Util.GetInput(2);

                switch (selection)
                {
                    case 1:
                        if (!SearchAndConnectDeviceWithIPv6(ref deviceID))
                        {
                            deviceID = 0;
                        }
                        break;
                    case 2:
                        if (!ConnectToDeviceWithIPv6(ref deviceID))
                        {
                            deviceID = 0;
                        }
                        break;
                    case 3:
                        {
                            if (deviceIDForServerMode == 0)
                            {
                                Console.WriteLine("Waiting for client connection");
                                eventWaitHandle.WaitOne();
                            }


                            deviceID = deviceIDForServerMode;

                            /*
                            result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);

                            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                            {
                                Console.WriteLine("Can't connect to device(errorCode : {0}).", result);
                                deviceID = 0;
                            }
                            else
                            {
                                Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
                            }
                             */

                        }
                        break;
                    case 4:
                        if (!GetIPConfigViaUDP(ref deviceID))
                        {
                            deviceID = 0;
                        }
                        break;
                    case 5:
                        SetIPConfigViaUDP();
                        break;
                    case 6:
                        if (!GetIPV6ConfigViaUDP(ref deviceID))
                        {
                            deviceID = 0;
                        }
                        break;
                    case 7:
                        SetIPV6ConfigViaUDP();
                        break;
                    case 8:
                        noConnectionMode = true;
                        break;
                    default:
                        Console.WriteLine("Invalid parameter : {0}", selection);
                        break;
                }
            } while (deviceID == 0 && noConnectionMode == false);

            if (noConnectionMode == false && deviceID > 0)
            {
                Console.Title = String.Format("{0} connected deviceID[{1}]", title, deviceID);

#if !SDK_AUTO_CONNECTION
                reconnectionTask = new ReconnectionTask(sdkContext);
                reconnectionTask.start();
#endif
                runImpl(deviceID);
#if !SDK_AUTO_CONNECTION
                reconnectionTask.stop();
                reconnectionTask = null;
#endif

                Console.WriteLine("Trying to discconect device[{0}].", deviceID);
                result = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", result);
                    ClearSDK(delayTerminate);
                }
            }
            else if (noConnectionMode == true)
            {
                Console.Title = String.Format("{0} No Connection Mode", title);

                runImpl(deviceID);
            }

            eventWaitHandle.Close();
            ClearSDK(delayTerminate);

            cbOnDeviceFound = null;
            cbOnDeviceAccepted = null;
            cbOnDeviceConnected = null;
            cbOnDeviceDisconnected = null;
            //cbOnSendRootCA = null;

        }

        void ClearSDK(int delayTerminate)
        {
            if (sdkContext != IntPtr.Zero)
            { 
                API.BS2_ReleaseContext(sdkContext);
            }
            sdkContext = IntPtr.Zero;
            Thread.Sleep(delayTerminate);
        }
		
        bool SearchAndConnectDeviceWithIPv6(ref UInt32 deviceID)
        {
            bool IPv6 = true;
            bool IPv4 = true;
            Console.WriteLine("Which mode do you want to use between IPv4 and IPv6? [0(IPv4), 1(IPv6), 2(Both: Default)]");
            Console.Write(">>>> ");
            int choiceIP = Util.GetInput((int)2);
            if (choiceIP == 0)
            {
                IPv4 = true;
                IPv6 = false;
            }
            else if (choiceIP == 1)
            {
                IPv4 = false;
                IPv6 = true;
            }

            Console.WriteLine("Trying to broadcast on the network");

            IntPtr ptrV4Broad = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
            IntPtr ptrV6Multi = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);

            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_SearchDevices(sdkContext);
            else if (IPv4 && !IPv6)
                //result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
                result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, ptrV4Broad);
            else if (!IPv4 && IPv6)
                //result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
                result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, ptrV6Multi);

            Marshal.FreeHGlobal(ptrV4Broad);
            Marshal.FreeHGlobal(ptrV6Multi);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SearchDevices?? : Got error : {0}.", result);
                return false;
            }

            IntPtr deviceListObj = IntPtr.Zero;
            UInt32 numDevice = 0;

            const UInt32 LONG_TIME_STANDBY_7S = 7;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_7S);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                Console.WriteLine("SetDeviceSearchingTimeout: Got error : {0}.", result);
                return false;
            }

            result = (BS2ErrorCode)API.BS2_GetDevices(sdkContext, out deviceListObj, out numDevice);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetDevices: Got error : {0}.", result);
                return false;
            }

            if (numDevice > 0)
            {
                BS2SimpleDeviceInfo deviceInfo;
                Type structType = typeof(BS2IPv6DeviceInfo);
                int structSize = Marshal.SizeOf(structType);
                IntPtr buffer = Marshal.AllocHGlobal(structSize);
                UInt32 outStructSize = 0;

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));
                    
                    result = (BS2ErrorCode)API.BS2_GetSpecifiedDeviceInfo(sdkContext, deviceID, (UInt32)BS2SpecifiedDeviceInfo.BS2_SPECIFIED_DEVICE_INFO_IPV6, buffer, (UInt32)structSize, out outStructSize);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("GetSpecifiedDeviceInfo: Got error : {0}.", result);
                        Marshal.FreeHGlobal(buffer);
                        return false;
                    }
                    BS2IPv6DeviceInfo devicInfoIPv6 = (BS2IPv6DeviceInfo)Marshal.PtrToStructure(buffer, structType);

                    result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("GetDeviceInfo: Got error : {0}.", result);
                        Marshal.FreeHGlobal(buffer);
                        return false;
                    }

                    Console.WriteLine("[{0, 3:##0}] ==> ID[{1, 10}] Type[{2, 16}] Connection mode[{3}] IPv4[{4}] IPv4-Port[{5}], IPv6[{6}] IPv6-Port[{7}]",
                            idx,
                            deviceID,
                            API.productNameDictionary.ContainsKey((BS2DeviceTypeEnum)deviceInfo.type) ? API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type] : (API.productNameDictionary[BS2DeviceTypeEnum.UNKNOWN] + "(" + deviceInfo.type + ")") ,
                            (BS2ConnectionModeEnum)deviceInfo.connectionMode,
                            new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString(),
                            deviceInfo.port
                            ,Encoding.UTF8.GetString(devicInfoIPv6.ipv6Address).TrimEnd('\0')
                            ,devicInfoIPv6.portV6
                            ); 
                }

                Marshal.FreeHGlobal(buffer);

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                Console.WriteLine("Please, choose the index of the Device which you want to connect to. [-1: quit]");
                Console.Write(">>>> ");

                deviceID = 0;
                Int32 selection = Util.GetInput();

                if (selection >= 0)
                {
                    if (selection < numDevice)
                    {
                        deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)selection * sizeof(UInt32)));
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection[{0}]", selection);
                    }
                }

                API.BS2_ReleaseObject(deviceListObj);
                if (deviceID > 0)
                {
                    IntPtr buffer1 = Marshal.AllocHGlobal(structSize);
                    result = (BS2ErrorCode)API.BS2_GetSpecifiedDeviceInfo(sdkContext, deviceID, (UInt32)BS2SpecifiedDeviceInfo.BS2_SPECIFIED_DEVICE_INFO_IPV6, buffer1, (UInt32)structSize, out outStructSize);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("GetSpecifiedDeviceInfo: Got error : {0}.", result);
                        Marshal.FreeHGlobal(buffer1);
                        return false;
                    }
                    bool connectIPv6 = false;
                    BS2IPv6DeviceInfo devicInfoIPv6 = (BS2IPv6DeviceInfo)Marshal.PtrToStructure(buffer1, structType);
                    IPAddress tempAddress;
                    bool bCanUseIPv6 = Encoding.UTF8.GetString(devicInfoIPv6.ipv6Address).TrimEnd('\0').Length > 0
                        && IPAddress.TryParse(Encoding.UTF8.GetString(devicInfoIPv6.ipv6Address).TrimEnd('\0'), out tempAddress)
                        && tempAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
                    if (bCanUseIPv6)
                    {
                        Console.WriteLine("Do you want to connect via IPv6? [Y/n]");
                        Console.Write(">>>>");
                        if (Util.IsYes())
                        {
                            connectIPv6 = true;
                        }
                    }
                    Marshal.FreeHGlobal(buffer1);

                    Console.WriteLine("Trying to connect to device[{0}]", deviceID);                    

                    if (connectIPv6)
                        result = (BS2ErrorCode)API.BS2_ConnectDeviceIPV6(sdkContext, deviceID);
                    else
                        result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);                    

                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("BS2_ConnectDevice???: Got error : {0}.", result);
                        return false;
                    }

                    Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("There is no device to launch.");
            }

            return false;
        }

        bool ConnectToDeviceWithIPv6(ref UInt32 deviceID)
        {
            Console.WriteLine("Enter the IP Address to connect device");
            Console.Write(">>>> ");
            string deviceIpAddress = Console.ReadLine();
            IPAddress ipAddress;

            if (!IPAddress.TryParse(deviceIpAddress, out ipAddress))
            {
                Console.WriteLine("Invalid ip : " + deviceIpAddress);
                return false;
            }

            Console.WriteLine("Enter the port number to connect device : default[{0}]", ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT_V6 : BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT); //[IPv6] <=
            Console.Write(">>>> ");
            UInt16 port = Util.GetInput((UInt16)(ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT_V6 : BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT)); //[IPv6] <=

            Console.WriteLine("Trying to connect to device [ip :{0}, port : {1}]", deviceIpAddress, port);


            IntPtr ptrIPAddr = Marshal.StringToHGlobalAnsi(deviceIpAddress);
            //BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, deviceIpAddress, port, out deviceID);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ptrIPAddr, port, out deviceID);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("BS2_ConnectDeviceViaIP: Got error : {0}.", result);
                return false;
            }
            Marshal.FreeHGlobal(ptrIPAddr);

            Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
            return true;
        }

        void print(BS2IpConfig config)
        {
            Console.WriteLine(">>>> IP configuration ");
            Console.WriteLine("     |--connectionMode : {0}", config.connectionMode);
            Console.WriteLine("     |--useDHCP : {0}", config.useDHCP);
            Console.WriteLine("     |--useDNS : {0}", config.useDNS);
            Console.WriteLine("     |--ipAddress : {0}", Encoding.UTF8.GetString(config.ipAddress), BitConverter.ToString(config.ipAddress));
            Console.WriteLine("     |--gateway : {0}", Encoding.UTF8.GetString(config.gateway), BitConverter.ToString(config.gateway));
            Console.WriteLine("     |--subnetMask : {0}", Encoding.UTF8.GetString(config.subnetMask), BitConverter.ToString(config.subnetMask));
            Console.WriteLine("     |--serverAddr : {0}", Encoding.UTF8.GetString(config.serverAddr), BitConverter.ToString(config.serverAddr));
            Console.WriteLine("     |--port : {0}", config.port);
            Console.WriteLine("     |--serverPort : {0}", config.serverPort);
            Console.WriteLine("     |--mtuSize : {0}", config.mtuSize);
            Console.WriteLine("     |--baseband : {0}", config.baseband);
            Console.WriteLine("     |--sslServerPort : {0}", config.sslServerPort);
            Console.WriteLine("<<<< ");
        }

        void print(BS2IPV6Config config)
        {
            Console.WriteLine(">>>> IPV6 configuration ");
            Console.WriteLine("     |--useIPV6 : {0}", config.useIPV6);
            Console.WriteLine("     |--reserved1 : {0}", config.reserved1);// useIPV4);
            Console.WriteLine("     |--useDhcpV6 : {0}", config.useDhcpV6);
            Console.WriteLine("     |--useDnsV6 : {0}", config.useDnsV6);
            Console.WriteLine("     |--staticIpAddressV6 : {0}", Encoding.UTF8.GetString(config.staticIpAddressV6), BitConverter.ToString(config.staticIpAddressV6));
            Console.WriteLine("     |--staticGatewayV6 : {0}", Encoding.UTF8.GetString(config.staticGatewayV6), BitConverter.ToString(config.staticGatewayV6));
            Console.WriteLine("     |--dnsAddrV6 : {0}", Encoding.UTF8.GetString(config.dnsAddrV6), BitConverter.ToString(config.dnsAddrV6));
            Console.WriteLine("     |--serverIpAddressV6 : {0}", Encoding.UTF8.GetString(config.serverIpAddressV6), BitConverter.ToString(config.serverIpAddressV6));
            Console.WriteLine("     |--serverPortV6 : {0}", config.serverPortV6);
            Console.WriteLine("     |--sslServerPortV6 : {0}", config.sslServerPortV6);
            Console.WriteLine("     |--portV6 : {0}", config.portV6);
            Console.WriteLine("     |--numOfAllocatedAddressV6 : {0}", config.numOfAllocatedAddressV6);
            Console.WriteLine("     |--numOfAllocatedGatewayV6 : {0}", config.numOfAllocatedGatewayV6);
            byte[] tempIPV6 = new byte[BS2Environment.BS2_IPV6_ADDR_SIZE];
            for (int idx = 0; idx < config.numOfAllocatedAddressV6; ++idx)
            {
                Array.Copy(config.allocatedIpAddressV6, idx * BS2Environment.BS2_IPV6_ADDR_SIZE, tempIPV6, 0, BS2Environment.BS2_IPV6_ADDR_SIZE);
                Console.WriteLine("     |--allocatedIpAddressV6[{0}] : {1}", idx, Encoding.UTF8.GetString(tempIPV6), BitConverter.ToString(tempIPV6));
            }
            for (int idx = 0; idx < config.numOfAllocatedGatewayV6; ++idx)
            {
                Array.Copy(config.allocatedGatewayV6, idx * BS2Environment.BS2_IPV6_ADDR_SIZE, tempIPV6, 0, BS2Environment.BS2_IPV6_ADDR_SIZE);
                Console.WriteLine("     |--allocatedGatewayV6[{0}] : {1}", idx, Encoding.UTF8.GetString(tempIPV6), BitConverter.ToString(tempIPV6));
            }
            Console.WriteLine("<<<< ");
        }

        bool GetIPConfigViaUDP(ref UInt32 deviceID)
        {
            Console.WriteLine("What is the ID of the device for which you want to get IP config?");
            Console.Write(">>>> ");
            UInt32 inputID = Util.GetInput((UInt32)0);
            if (inputID == 0)
            {
                Console.WriteLine("Invalid Device ID");
                return false;
            }

            bool IPv6 = true;
            bool IPv4 = true;
            Console.WriteLine("Which mode do you want to use between IPv4 and IPv6? [0(IPv4), 1(IPv6), 2(Both: Default)]");
            Console.Write(">>>> ");
            int choiceIP = Util.GetInput((int)2);
            if (choiceIP == 0)
            {
                IPv4 = true;
                IPv6 = false;
            }
            else if (choiceIP == 1)
            {
                IPv4 = false;
                IPv6 = true;
            }

            Console.WriteLine("Trying to send packet via UDP on the network");

            BS2IpConfig config;
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            IntPtr ptrV4Broad = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
            IntPtr ptrV6Multi = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDP(sdkContext, inputID, out config);
            else if (IPv4 && !IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDPEx(sdkContext, inputID, out config, ptrV4Broad);
            else if (!IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDPEx(sdkContext, inputID, out config, ptrV6Multi);
            else
                config = default(BS2IpConfig);

            Marshal.FreeHGlobal(ptrV4Broad);
            Marshal.FreeHGlobal(ptrV6Multi);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetIPConfigViaUDP??: Got error : {0}.", result);
                return false;
            }
            else
            {
                print(config);

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");

                Console.WriteLine("==> ID[{0, 10}] Connection mode[{1}] IPv4[{2}] IPv4-Port[{3}]",
                        inputID,
                        (BS2ConnectionModeEnum)config.connectionMode,
                        Encoding.UTF8.GetString(config.ipAddress).TrimEnd('\0'),
                        config.port
                        );

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                Console.WriteLine("Do you want to connect? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    
                    Console.WriteLine("Trying to connect to device[{0}]", inputID);

                    IntPtr ptrIPAddr = Marshal.StringToHGlobalAnsi(Encoding.UTF8.GetString(config.ipAddress).TrimEnd('\0'));
                    //result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, Encoding.UTF8.GetString(config.ipAddress).TrimEnd('\0'), config.port, out deviceID);
                    result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ptrIPAddr, config.port, out deviceID);

                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("ConnectDeviceViaIP: Got error : {0}.", result);
                        return false;
                    }
                    Marshal.FreeHGlobal(ptrIPAddr);

                    Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
                    return true;
                }
                else
                    return false;
            }
        }

        bool GetIPV6ConfigViaUDP(ref UInt32 deviceID)
        {
            Console.WriteLine("What is the ID of the device for which you want to get IP config?");
            Console.Write(">>>> ");
            UInt32 inputID = Util.GetInput((UInt32)0);
            if (inputID == 0)
            {
                Console.WriteLine("Invalid Device ID");
                return false;
            }

            bool IPv6 = true;
            bool IPv4 = true;
            Console.WriteLine("Which mode do you want to use between IPv4 and IPv6? [0(IPv4), 1(IPv6), 2(Both: Default)]");
            Console.Write(">>>> ");
            int choiceIP = Util.GetInput((int)2);
            if (choiceIP == 0)
            {
                IPv4 = true;
                IPv6 = false;
            }
            else if (choiceIP == 1)
            {
                IPv4 = false;
                IPv6 = true;
            }

            Console.WriteLine("Trying to send packet via UDP on the network");

            BS2IPV6Config config;
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            IntPtr ptrV4Broad = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
            IntPtr ptrV6Multi = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDP(sdkContext, inputID, out config);
            else if (IPv4 && !IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDPEx(sdkContext, inputID, out config, ptrV4Broad);
            else if (!IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDPEx(sdkContext, inputID, out config, ptrV6Multi);
            else
                config = default(BS2IPV6Config);

            Marshal.FreeHGlobal(ptrV4Broad);
            Marshal.FreeHGlobal(ptrV6Multi);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetIPV6ConfigViaUDP??: Got error : {0}.", result);
                return false;
            }
            else
            {
                print(config);

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");

                byte[] allocatedIpAddressV6_0 = new byte[BS2Environment.BS2_IPV6_ADDR_SIZE];
                Array.Copy(config.allocatedIpAddressV6, 0, allocatedIpAddressV6_0, 0, BS2Environment.BS2_IPV6_ADDR_SIZE);
                Console.WriteLine("==> ID[{0, 10}] numOfAllocated[{1}] IPv6[{2}] IPv6-Port[{3}]",
                        inputID,
                        config.numOfAllocatedAddressV6,
                        Encoding.UTF8.GetString(allocatedIpAddressV6_0).TrimEnd('\0'),
                        config.portV6
                        );

                Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                Console.WriteLine("Do you want to connect? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    String strIpAddressV6 = Encoding.UTF8.GetString(allocatedIpAddressV6_0).TrimEnd('\0');
                    if (strIpAddressV6.IndexOf('/') != -1)
                    {
                        strIpAddressV6 = strIpAddressV6.Substring(0, strIpAddressV6.IndexOf('/'));
                    }
                    Console.WriteLine("Trying to connect to device[{0}][{1}]", inputID, strIpAddressV6);

                    IntPtr ptrIPAddr = Marshal.StringToHGlobalAnsi(strIpAddressV6);
                    result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ptrIPAddr, config.portV6, out deviceID);

                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("ConnectDeviceViaIP??: Got error : {0}.", result);
                        return false;
                    }
                    Marshal.FreeHGlobal(ptrIPAddr);

                    Console.WriteLine(">>>> Successfully connected to the device[{0}].", deviceID);
                    return true;
                }
                else
                    return false;
            }
        }

        public void SetIPConfigViaUDP()
        {
            Console.WriteLine("What is the ID of the device for which you want to set IP config?");
            Console.Write(">>>> ");
            UInt32 inputID = Util.GetInput((UInt32)0);
            if (inputID == 0)
            {
                Console.WriteLine("Invalid Device ID");
                return;
            }

            bool IPv6 = true;
            bool IPv4 = true;
            Console.WriteLine("Which mode do you want to use between IPv4 and IPv6? [0(IPv4), 1(IPv6), 2(Both: Default)]");
            Console.Write(">>>> ");
            int choiceIP = Util.GetInput((int)2);
            if (choiceIP == 0)
            {
                IPv4 = true;
                IPv6 = false;
            }
            else if (choiceIP == 1)
            {
                IPv4 = false;
                IPv6 = true;
            }

            BS2IpConfig config;
            Console.WriteLine("Trying to get Current IPConfig via UDP");
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDP(sdkContext, inputID, out config);
            else if (IPv4 && !IPv6)
            {
                IntPtr ptrV4Broad = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDPEx(sdkContext, inputID, out config, ptrV4Broad);
                Marshal.FreeHGlobal(ptrV4Broad);
            }
            else if (!IPv4 && IPv6)
            {
                IntPtr ptrV6Multi = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
                result = (BS2ErrorCode)API.BS2_GetIPConfigViaUDPEx(sdkContext, inputID, out config, ptrV6Multi);
                Marshal.FreeHGlobal(ptrV6Multi);
            }
            else
            {
                Console.WriteLine("Wrong selection");
                return;
            }

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetIPConfigViaUDP??: Got error : {0}.", result);
                return;
            }
            else
            {
                print(config);
            }

            do
            {
                Console.WriteLine("useDhcp ? [{0}]", config.useDHCP != 0 ? "Y/n" : "y/N");
                Console.Write(">>>> ");
                bool bInput = config.useDHCP != 0 ? Util.IsYes() : !Util.IsNo();
                config.useDHCP = (byte)(bInput ? 1 : 0);

                Console.WriteLine("useDns ? [{0}]", config.useDNS != 0 ? "Y/n" : "y/N");
                Console.Write(">>>> ");
                bInput = config.useDNS != 0 ? Util.IsYes() : !Util.IsNo();
                config.useDNS = (byte)(bInput ? 1 : 0);

                string strInput;
                byte[] bytesInput = null;
                if (config.useDHCP == 0)
                {
                    Console.WriteLine("ipAddress ? [(Blank:{0})]", Encoding.UTF8.GetString(config.ipAddress));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.ipAddress, 0, config.ipAddress.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.ipAddress, 0, config.ipAddress.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.ipAddress, 0, Math.Min(bytesInput.Length, config.ipAddress.Length));
                    }
                    if (Encoding.UTF8.GetString(config.ipAddress).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.ipAddress).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong ipAddress: {0})", Encoding.UTF8.GetString(config.ipAddress));
                            return;
                        }
                    }

                    Console.WriteLine("gateway ? [(Blank:{0})]", Encoding.UTF8.GetString(config.gateway));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    bytesInput = null;
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.gateway, 0, config.gateway.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.gateway, 0, config.gateway.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.gateway, 0, Math.Min(bytesInput.Length, config.gateway.Length));
                    }
                    if (Encoding.UTF8.GetString(config.gateway).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.gateway).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong gateway: {0})", Encoding.UTF8.GetString(config.gateway));
                            return;
                        }
                    }

                    Console.WriteLine("subnetMask ? [(Blank:{0})]", Encoding.UTF8.GetString(config.subnetMask));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    bytesInput = null;
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.subnetMask, 0, config.subnetMask.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.subnetMask, 0, config.subnetMask.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.subnetMask, 0, Math.Min(bytesInput.Length, config.subnetMask.Length));
                    }
                    if (Encoding.UTF8.GetString(config.subnetMask).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.subnetMask).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong subnetMask: {0})", Encoding.UTF8.GetString(config.subnetMask));
                            return;
                        }
                    }
                }

                Console.WriteLine("port ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT);
                Console.Write(">>>> ");
                int nInput = Util.GetInput(BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT);
                config.port = (UInt16)nInput;

                Console.WriteLine("Do you want to use server to device connection mode? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                    config.connectionMode = (byte)BS2ConnectionModeEnum.SERVER_TO_DEVICE;
                else
                    config.connectionMode = (byte)BS2ConnectionModeEnum.DEVICE_TO_SERVER;

                if (config.connectionMode == (byte)BS2ConnectionModeEnum.DEVICE_TO_SERVER)
                {
                    Console.WriteLine("serverAddr ? [(Blank:{0})]", Encoding.UTF8.GetString(config.serverAddr));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    bytesInput = null;
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.serverAddr, 0, config.serverAddr.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.serverAddr, 0, config.serverAddr.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.serverAddr, 0, Math.Min(bytesInput.Length, config.serverAddr.Length));
                    }
                    if (Encoding.UTF8.GetString(config.serverAddr).TrimEnd('\0').Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.serverAddr).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong serverAddr: {0})", Encoding.UTF8.GetString(config.serverAddr));
                            return;
                        }
                    }

                    Console.WriteLine("serverPort ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT);
                    Console.Write(">>>> ");
                    nInput = Util.GetInput(BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT);
                    config.serverPort = (UInt16)nInput;

                    Console.WriteLine("sslServerPort ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT);
                    Console.Write(">>>> ");
                    nInput = Util.GetInput(BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT);
                    config.sslServerPort = (UInt16)nInput;
                }
            } while (false);

            Console.WriteLine("Trying to set IPConfig via UDP");
            IntPtr ptrV4Broad2 = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
            IntPtr ptrV6Multi2 = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPConfigViaUDP(sdkContext, inputID, ref config);
            else if (IPv4 && !IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPConfigViaUDPEx(sdkContext, inputID, ref config, ptrV4Broad2);
            else if (!IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPConfigViaUDPEx(sdkContext, inputID, ref config, ptrV6Multi2);
            Marshal.FreeHGlobal(ptrV4Broad2);
            Marshal.FreeHGlobal(ptrV6Multi2);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SetIPConfigViaUDP??: Got error({0}).", result);
            }
            else
            {
                Console.WriteLine(">>>> Successfully set");
            }
        }

        public void SetIPV6ConfigViaUDP()
        {
            Console.WriteLine("What is the ID of the device for which you want to get IP config?");
            Console.Write(">>>> ");
            UInt32 inputID = Util.GetInput((UInt32)0);
            if (inputID == 0)
            {
                Console.WriteLine("Invalid Device ID");
                return;
            }

            bool IPv6 = true;
            bool IPv4 = true;
            Console.WriteLine("Which mode do you want to use between IPv4 and IPv6? [0(IPv4), 1(IPv6), 2(Both: Default)]");
            Console.Write(">>>> ");
            int choiceIP = Util.GetInput((int)2);
            if (choiceIP == 0)
            {
                IPv4 = true;
                IPv6 = false;
            }
            else if (choiceIP == 1)
            {
                IPv4 = false;
                IPv6 = true;
            }

            BS2IPV6Config config;
            Console.WriteLine("Trying to get Current IPV6Config via UDP");
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDP(sdkContext, inputID, out config);
            else if (IPv4 && !IPv6)
            {
                IntPtr ptrV4Broad = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDPEx(sdkContext, inputID, out config, ptrV4Broad);
                Marshal.FreeHGlobal(ptrV4Broad);
            }
            else if (!IPv4 && IPv6)
            {
                IntPtr ptrV6Multi = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
                result = (BS2ErrorCode)API.BS2_GetIPV6ConfigViaUDPEx(sdkContext, inputID, out config, ptrV6Multi);
                Marshal.FreeHGlobal(ptrV6Multi);
            }
            else
            {
                Console.WriteLine("Wrong selection");
                return;
            }

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("GetIPV6ConfigViaUDP??: Got error({0}).", result);
                return;
            }
            else
            {
                print(config);
            }

            do
            {
                Console.WriteLine("useDhcpV6 ? [{0}]", config.useDhcpV6 != 0 ? "Y/n" : "y/N");
                Console.Write(">>>> ");
                bool bInput = config.useDhcpV6 != 0 ? Util.IsYes() : !Util.IsNo();
                config.useDhcpV6 = (byte)(bInput ? 1 : 0);

                Console.WriteLine("useDnsV6 ? [{0}]", config.useDnsV6 != 0 ? "Y/n" : "y/N");
                Console.Write(">>>> ");
                bInput = config.useDnsV6 != 0 ? Util.IsYes() : !Util.IsNo();
                config.useDnsV6 = (byte)(bInput ? 1 : 0);

                string strInput;
                byte[] bytesInput = null;
                if (config.useDhcpV6 == 0)
                {
                    Console.WriteLine("staticIpAddressV6 ? [(Blank:{0})]", Encoding.UTF8.GetString(config.staticIpAddressV6));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / N(clear), (Blank:Y)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.staticIpAddressV6, 0, config.staticIpAddressV6.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.staticIpAddressV6, 0, config.staticIpAddressV6.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.staticIpAddressV6, 0, Math.Min(bytesInput.Length, config.staticIpAddressV6.Length));
                    }
                    if (Encoding.UTF8.GetString(config.staticIpAddressV6).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.staticIpAddressV6).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong staticIpAddressV6: {0})", Encoding.UTF8.GetString(config.staticIpAddressV6));
                            return;
                        }
                    }


                    Console.WriteLine("staticGatewayV6 ? [(Blank:{0})]", Encoding.UTF8.GetString(config.staticGatewayV6));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    bytesInput = null;
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.staticGatewayV6, 0, config.staticGatewayV6.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.staticGatewayV6, 0, config.staticGatewayV6.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.staticGatewayV6, 0, Math.Min(bytesInput.Length, config.staticGatewayV6.Length));
                    }
                    if (Encoding.UTF8.GetString(config.staticGatewayV6).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.staticGatewayV6).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong staticGatewayV6: {0})", Encoding.UTF8.GetString(config.staticGatewayV6));
                            return;
                        }
                    }
                }

                if (config.useDnsV6 == 1)
                {
                    Console.WriteLine("dnsAddrV6 ? [(Blank:{0})]", Encoding.UTF8.GetString(config.dnsAddrV6));
                    Console.Write(">>>> ");
                    strInput = Console.ReadLine();
                    bytesInput = null;
                    if (strInput.Length == 0)
                    {
                        Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                        Console.Write("   >>>> ");
                        if (!Util.IsYes())
                        {
                            Array.Clear(config.dnsAddrV6, 0, config.dnsAddrV6.Length);
                        }
                    }
                    else
                    {
                        Array.Clear(config.dnsAddrV6, 0, config.dnsAddrV6.Length);
                        bytesInput = Encoding.UTF8.GetBytes(strInput);
                        Array.Copy(bytesInput, 0, config.dnsAddrV6, 0, Math.Min(bytesInput.Length, config.dnsAddrV6.Length));
                    }
                    if (Encoding.UTF8.GetString(config.dnsAddrV6).Length > 0)
                    {
                        IPAddress dummy;
                        if (IPAddress.TryParse(Encoding.UTF8.GetString(config.dnsAddrV6).TrimEnd('\0'), out dummy) == false)
                        {
                            Console.WriteLine("Wrong dnsAddrV6: {0})", Encoding.UTF8.GetString(config.dnsAddrV6));
                            return;
                        }
                    }
                }

                Console.WriteLine("serverIpAddressV6 ? [(Blank:{0})]", Encoding.UTF8.GetString(config.serverIpAddressV6));
                Console.Write(">>>> ");
                strInput = Console.ReadLine();
                bytesInput = null;
                if (strInput.Length == 0)
                {
                    Console.WriteLine("   Do you want to keep the value? [Y(keep) / n(clear)]");
                    Console.Write("   >>>> ");
                    if (!Util.IsYes())
                    {
                        Array.Clear(config.serverIpAddressV6, 0, config.serverIpAddressV6.Length);
                    }
                }
                else
                {
                    Array.Clear(config.serverIpAddressV6, 0, config.serverIpAddressV6.Length);
                    bytesInput = Encoding.UTF8.GetBytes(strInput);
                    Array.Copy(bytesInput, 0, config.serverIpAddressV6, 0, Math.Min(bytesInput.Length, config.serverIpAddressV6.Length));
                }
                if (Encoding.UTF8.GetString(config.serverIpAddressV6).TrimEnd('\0').Length > 0)
                {
                    IPAddress dummy;
                    if (IPAddress.TryParse(Encoding.UTF8.GetString(config.serverIpAddressV6), out dummy) == false)
                    {
                        Console.WriteLine("Wrong serverIpAddressV6: {0})", Encoding.UTF8.GetString(config.serverIpAddressV6));
                        return;
                    }
                }

                Console.WriteLine("serverPortV6 ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT_V6);
                Console.Write(">>>> ");
                int nInput = Util.GetInput(BS2Environment.BS2_TCP_SERVER_PORT_DEFAULT_V6);
                config.serverPortV6 = (UInt16)nInput;

                Console.WriteLine("sslServerPortV6 ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT_V6);
                Console.Write(">>>> ");
                nInput = Util.GetInput(BS2Environment.BS2_TCP_SSL_SERVER_PORT_DEFAULT_V6);
                config.sslServerPortV6 = (UInt16)nInput;

                Console.WriteLine("portV6 ? [1~65535 (Blank:{0})]", BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT_V6);
                Console.Write(">>>> ");
                nInput = Util.GetInput(BS2Environment.BS2_TCP_DEVICE_PORT_DEFAULT_V6);
                config.portV6 = (UInt16)nInput;

                config.numOfAllocatedAddressV6 = 0;
                config.numOfAllocatedGatewayV6 = 0;

            } while (false);

            Console.WriteLine("Trying to set IPV6Config via UDP");
            IntPtr ptrV4Broad2 = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_BROADCAST_IPV4_ADDRESS);
            IntPtr ptrV6Multi2 = Marshal.StringToHGlobalAnsi(BS2Environment.DEFAULT_MULTICAST_IPV6_ADDRESS);
            if (IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPV6ConfigViaUDP(sdkContext, inputID, ref config);
            else if (IPv4 && !IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPV6ConfigViaUDPEx(sdkContext, inputID, ref config, ptrV4Broad2);
            else if (!IPv4 && IPv6)
                result = (BS2ErrorCode)API.BS2_SetIPV6ConfigViaUDPEx(sdkContext, inputID, ref config, ptrV6Multi2);

            Marshal.FreeHGlobal(ptrV4Broad2);
            Marshal.FreeHGlobal(ptrV6Multi2);

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("SetIPV6ConfigViaUDP??: Got error({0}).", result);
            }
            else
            {
                Console.WriteLine(">>>> Successfully set");
            }
        }
    }
}
