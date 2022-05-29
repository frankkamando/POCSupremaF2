using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Suprema;
using SupremaF2.Models;
using SupremaF2.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceIntergrationWithIP : ControllerBase
    {
        protected BS2SimpleDeviceInfo deviceInfo;
        protected BS2SimpleDeviceInfoEx deviceInfoEx;
        UserControl _control=new UserControl();

        private const int USER_PAGE_SIZE = 1024;

        private API.OnReadyToScan cbCardOnReadyToScan = null;
        private API.OnReadyToScan cbFingerOnReadyToScan = null;
        private API.OnReadyToScan cbFaceOnReadyToScan = null;
        private API.OnUserPhrase cbOnUserPhrase = null;
        private IntPtr sdkContext;
        //public UserManagementController()
        //{
        //}

        //public UserManagementController(IUserControl control)
        //{
        //    _control = control;
        //}  543617681



        [HttpGet("api/[controller]/{sdkContext}/{deviceID}/{isMasterDevice}/{noConnection}")]
        public void execute(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice, bool noConnection = false)
        {
            if (noConnection == false)
            {
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetDeviceInfoEx(sdkContext, deviceID, out deviceInfo, out deviceInfoEx);
                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Can't get device information(errorCode : {0}).", result);
                    return;
                }
            }
        }

        [AllowAnonymous]
        [HttpPost("ConnectToDeviceWithIP")]
        public async Task<IActionResult> ConnectToDeviceWithIP(reqDeviceConnection data)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.ConnectToDeviceWithIP(data);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }        

        [AllowAnonymous]
        [HttpPost("SearchDevices")]
        public async Task<IActionResult> SearchDevices()
        {
            if (ModelState.IsValid)
            {
                int deviceID = 0;
                var results = await this._control.ConnectToDeviceWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DisConnectDeviceWithIP")]
        public async Task<IActionResult> DisConnectDevice(reqDeviceConnection data)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DisconnectDeviceWithIP(data);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        

        [AllowAnonymous]
        [HttpPost("ListUserFromDeviceWithIP")]
        public async Task<IActionResult> ListUserFromDeviceWithIP(reqDeviceConnection data)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.listUserFromDeviceWithIP(data);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("UserLogsWithIP")]
        public async Task<IActionResult> UserLogsWithIP(reqDeviceConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
               var results= this._control.LogWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeleteSingleUserWithIP")]
        public async Task<IActionResult> DeleteSingleUserWithIP(reqDevicenIDConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = this._control.deleteSingleUserFromDeviceWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeleteAllUserWithIP")]
        public async Task<IActionResult> DeleteAllUserWithIP(reqDeviceConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = this._control.DeleteAllWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("SingleUserFromDeviceWithIP")]
        public async Task<IActionResult> SingleUserFromDeviceWithIP(reqDevicenIDConnection data)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.SingleUserFromDeviceWithIPen(data);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }


        [AllowAnonymous]
        [HttpPost("SettingAuthorizationWithIP")]
        public async Task<IActionResult> SettingAuthorizationWithIP(reqDevicenIDlvConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.SetAuthorizationWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeletingAllAuthorizationWithIP")]
        public async Task<IActionResult> DeletingAllAuthorizationWithIP(reqDeviceConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DeleteAllAuthorizationWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeletingsingleAuthorizationWithIP")]
        public async Task<IActionResult> DeletingsingleAuthorizationWithIP(reqDevicenIDConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DeleteAuthorizationWithIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("GetAuthOperatorWithIP")]
        public async Task<IActionResult> GetAuthOperatorWithIP(reqDevicenIDConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.AuthOperatorLevelIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("GetAllAuthOperatorWithIP")]
        public async Task<IActionResult> GetAllAuthOperatorWithIP(reqDeviceConnection reqDevice)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.GetAllAuthOperatorLevelIP(reqDevice);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("InsertUserWithIP")]
        public async Task<IActionResult> InsertUserWithIP(reqUserDetailsIPModel rqdetails)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.InsertUsersIP(rqdetails);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

    }
}
