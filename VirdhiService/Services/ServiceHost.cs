using AVTech.Device.BusinessServices.Implementation;
using AVTech.Device.BusinessServices.WebSocket;
using DeviceServices.Implementation;
using DeviceServices.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DeviceServices.Services
{
    public static class ServiceHost
    {
        // Takes the builder Program.cs already created, adds services to it.
        // Does NOT call .Build() — Program.cs owns that.
        public static void ConfigureServices(HostApplicationBuilder builder)
        {

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddScoped<IDeviceDataDownloadService, DeviceDataDownloadService>();
            builder.Services.AddScoped<IDeviceDatabaseService, DeviceDatabaseService>();

            builder.Services.AddSingleton<WebSocketServerManage>();
            builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WebSocketServerManage>());

            builder.Services.AddSingleton<ZktTCPHandler>();
            builder.Services.AddSingleton<EsslTCPHandler>();

            builder.Services.AddSingleton<BsHttpHandler>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<BsHttpHandler>());

            builder.Services.AddHostedService<Worker>();
        }
    }
}