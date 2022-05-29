using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SupremaF2.Models;
using SupremaF2.Repository;
using Suprema;

namespace SupremaF2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceIntergrationWithDeviceID : ControllerBase
    {
        protected BS2SimpleDeviceInfo deviceInfo;
        protected BS2SimpleDeviceInfoEx deviceInfoEx;
        UserControl _control = new UserControl();

        private const int USER_PAGE_SIZE = 1024;

        private API.OnReadyToScan cbCardOnReadyToScan = null;
        private API.OnReadyToScan cbFingerOnReadyToScan = null;
        private API.OnReadyToScan cbFaceOnReadyToScan = null;
        private API.OnUserPhrase cbOnUserPhrase = null;
        private IntPtr sdkContext;

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
        [HttpPost("ConnectToDeviceWithDeviceID")]
        public async Task<IActionResult> ConnectToDeviceWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
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
        [HttpPost("DisConnectDeviceWithDeviceID")]
        public async Task<IActionResult> DisConnectDeviceWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DisconnectDeviceWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("ListUserFromDeviceWithDeviceID")]
        public async Task<IActionResult> ListUserFromDeviceWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.listUserFromDeviceWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }


        [AllowAnonymous]
        [HttpPost("UserLogWithDeviceID")]
        public async Task<IActionResult> UserLogWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = this._control.LogWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeleteSingleUserWithDeviceID")]
        public async Task<IActionResult> DeleteSingleUserWithDeviceID(int deviceID, string UserID)
        {
            if (ModelState.IsValid)
            {
                var results = this._control.DeleteUserWithDeviceID(deviceID, UserID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeleteAllUserWithDeviceID")]
        public async Task<IActionResult> DeleteAllUserWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = this._control.DeleteAllWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("SingleUserFromDeviceWithDeviceID")]
        public async Task<IActionResult> SingleUserFromDeviceWithDeviceID(int deviceID, string UserID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.SingleUserFromDeviceWithDeviceen(deviceID, UserID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("SettingAuthorizationWithDeviceID")]
        public async Task<IActionResult> AuthorizationWithDeviceID(int deviceID, string UserID, int level)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.SetAuthorizationWithDeviceID(deviceID, UserID, level);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeletingAllAuthorizationWithDeviceID")]
        public async Task<IActionResult> DeletingAllAuthorizationWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DeleteAllAuthorizationWithDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("DeletingAuthorizationWithDeviceID")]
        public async Task<IActionResult> DeletingAuthorizationWithDeviceID(int deviceID, string UserID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.DeleteAuthorizationWithDeviceID(deviceID, UserID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("GetAuthOperatorWithDeviceID")]
        public async Task<IActionResult> GetAuthOperatorWithDeviceID(int deviceID, string UserID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.AuthOperatorLevelDeviceID(deviceID, UserID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("GetAllAuthOperatorWithDeviceID")]
        public async Task<IActionResult> GetAllAuthOperatorWithDeviceID(int deviceID)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.GetAllAuthOperatorLevelDeviceID(deviceID);

                if (results == null)
                {
                    return NotFound();
                }
                return Ok(results);
            }
            return BadRequest(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("InsertUserWithDeviceID")]
        public async Task<IActionResult> InsertUserWithDeviceID(int deviceID, reqUserDetailsModel rqdetails)
        {
            if (ModelState.IsValid)
            {
                var results = await this._control.InsertUsers(deviceID, rqdetails);

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
