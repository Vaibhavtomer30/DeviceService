using DeviceService;
using DeviceServices;
using DeviceServices.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using System;
using System.Windows.Forms;

namespace VirdhiService
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool isInteractive = Environment.UserInteractive && !WindowsServiceHelpers.IsWindowsService();

            var builder = Host.CreateApplicationBuilder(args);   // 1. create builder

            builder.Services.AddWindowsService(options =>        // 2. Program-specific config
            {
                options.ServiceName = "VirdhiService";
            });

            ServiceHost.ConfigureServices(builder);               // 3. delegate app services

            if (isInteractive)
            {
                builder.Logging.AddProvider(new FormLoggerProvider()); // 4. more Program-specific config
            }

            var host = builder.Build();                           // 5. Program.cs builds it — only once

            if (isInteractive)
                RunInteractive(host);
            else
                host.Run();
        }

        private static void RunInteractive(IHost host)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new StatusForm();
            var cts = new System.Threading.CancellationTokenSource();

            form.StopRequested += (_, _) => cts.Cancel();

            var hostTask = host.RunAsync(cts.Token);

            form.FormClosed += async (_, _) =>
            {
                if (!cts.IsCancellationRequested) cts.Cancel();
                await hostTask;
                host.Dispose();
                Application.ExitThread();
            };

            Application.Run(form);
        }
    }
}