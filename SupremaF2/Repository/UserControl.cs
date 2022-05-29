using Suprema;
using SupremaF2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Repository
{
    public class UserControl : UnitTest
    {
        public async Task<string> ConnectToDeviceWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = 0;
                return program.ConnectToDeviceUnit(ref deviceID, reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DisconnectDeviceWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.DisConnectDeviceUnitwithIp(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<string> DisconnectDeviceWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.DisConnectDeviceUnitWithDeviceID(ref deviceID);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<respDeviceModel> ConnectToDeviceWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.ConnectToDeviceWithDeviceIDUnit(ref deviceID);
            }
            catch (Exception ex)
            {
                respDeviceModel data = new respDeviceModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<respGetUserModel> listUserFromDeviceWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.listUserFromDeviceWithIPUnit(reqDevice);
            }
            catch (Exception ex)
            {
                respGetUserModel data = new respGetUserModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<respGetUserModel> listUserFromDeviceWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.listUserFromDeviceWithDeviceIDUnit(ref deviceID);
            }
            catch (Exception ex)
            {
                respGetUserModel data = new respGetUserModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public respLogs LogWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.getLogWithIP(reqDevice);
            }
            catch (Exception ex)
            {
                respLogs data = new respLogs();
                data.code = Codes.ERROR;
                data.message = ex.Message;
               return data;
            }
        }

        public respLogs LogWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.getLogWithDeviceID(ref deviceID);
            }
            catch (Exception ex)
            {
                respLogs data = new respLogs();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<string> DeleteUserWithIP(reqDevicenIDConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.deleteSingleUserFromDeviceWithIP(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteUserWithDeviceID(int DeviceID,string UserID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.deleteSingleUserFromDeviceWithDeviceID(ref deviceID, UserID);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAllWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.deleteAllUserFromDeviceWithDeviceID(ref deviceID);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAllWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.deleteAllUserFromDeviceWithIP(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<respGetUserModel> SingleUserFromDeviceWithIPen(reqDevicenIDConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.getsingleUserFromDeviceWithIP(reqDevice);
            }
            catch (Exception ex)
            {
                respGetUserModel data = new respGetUserModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<respGetUserModel> SingleUserFromDeviceWithDeviceen(int DeviceID, string UserID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.getsingleUserFromDeviceWithDeviceID(ref deviceID, UserID);
            }
            catch (Exception ex)
            {
                respGetUserModel data = new respGetUserModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<string> SetAuthorizationWithDeviceID(int DeviceID, string UserID,int level)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.setAuthOperatorLevelExDeviceID(ref deviceID, UserID,level);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<string> SetAuthorizationWithIP(reqDevicenIDlvConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.setAuthOperatorLevelExIP(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAllAuthorizationWithDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.delAllAuthOperatorLevelExDeviceID(ref deviceID);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAllAuthorizationWithIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.delAllAuthOperatorLevelExIP(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAuthorizationWithDeviceID(int DeviceID, string UserID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.delAuthOperatorLevelEx(ref deviceID, UserID);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteAuthorizationWithIP(reqDevicenIDConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.delAuthOperatorLevelExIP(reqDevice);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<resLevelModel> AuthOperatorLevelDeviceID(int DeviceID, string UserID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.getAuthOperatorLevelExDeviceID(ref deviceID, UserID);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<resLevelModel> AuthOperatorLevelIP(reqDevicenIDConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.getAuthOperatorLevelExIP(reqDevice);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<resLevelModel> GetAllAuthOperatorLevelDeviceID(int DeviceID)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.getAllAuthOperatorLevelEx(ref deviceID);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<resLevelModel> GetAllAuthOperatorLevelIP(reqDeviceConnection reqDevice)
        {
            try
            {
                UserControl program = new UserControl();
                return program.getAllAuthOperatorLevelExIP(reqDevice);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return data;
            }
        }

        public async Task<string> InsertUsers(int DeviceID, reqUserDetailsModel rqdetails)
        {
            try
            {
                UserControl program = new UserControl();
                UInt32 deviceID = (uint)DeviceID;
                return program.InsertUserDetailsDeviceID(ref deviceID,rqdetails);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return ex.Message;
            }
        }

        public async Task<string> InsertUsersIP(reqUserDetailsIPModel rqdetails)
        {
            try
            {
                UserControl program = new UserControl();
                return program.InsertUserDetailsIP(rqdetails);
            }
            catch (Exception ex)
            {
                resLevelModel data = new resLevelModel();
                data.code = Codes.ERROR;
                data.message = ex.Message;
                return ex.Message;
            }
        }

        protected override void runImpl(uint deviceID)
        {
            throw new NotImplementedException();
        }
    }
}
