using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Suprema;
using SupremaF2.Controllers;
using SupremaF2.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SupremaF2
{
    public class Program : UnitTest
    {
        private readonly IUserControl _control;
        private DeviceIntergrationWithIP uc = new DeviceIntergrationWithIP();
        //public UnitTest unitTest;

        protected override void runImpl(UInt32 deviceID)
        {
            uc.execute(sdkContext, deviceID, true);
        }

        public static void Main(string[] args)
        {
            Program program = new Program();
            program.Title = "Test for user management";
            program.run();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
