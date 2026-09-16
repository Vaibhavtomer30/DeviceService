using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
///using AVTech.Device.BusinessServices.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebSocketSharp;
using WebSocketSharp.Server;
using DeviceServices.Models;
using AVTech.Device.BusinessServices;

namespace DeviceServices.Services;

public class WebSocketServerManage : BackgroundService
{
    private WebSocketServer? _webSocketServer;
    private TcpListener _zktTcpListener;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly ILogger<ZktTCPHandler> _zktlogger;
    private readonly IConfiguration _configuration;

    public WebSocketServerManage(IServiceProvider serviceProvider, ILogger<WebSocketHandler> logger, IConfiguration configuration, ILogger<ZktTCPHandler> zktlogger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _zktlogger = zktlogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Start();

            _logger.LogInformation("WebSocket server started");

            // Scheduler loop (replaces Timer)
            while (!stoppingToken.IsCancellationRequested)
            {
               // await ExecuteDeviceCheckAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket service crashed");
        }
        finally
        {
            Close();
            _logger.LogInformation("WebSocket server stopped");
        }
    }
    public async void Start()
    {
        if (_webSocketServer != null && _webSocketServer.IsListening)
        {
            Close();
        }

        int port = GetPort();
        _webSocketServer = new WebSocketServer($"ws://0.0.0.0:{port}");

        
        _webSocketServer.AddWebSocketService<WebSocketHandler>(
            "/pub/chat",
            () => new WebSocketHandler(_logger, _serviceProvider));

        //_webSocketServer.AddWebSocketService<ZktWebSocketHandler>(
        //    "/iclock/cdata",
        //    () => new ZktWebSocketHandler(_zktlogger, _serviceProvider));

       

        try
        {
            _webSocketServer.Start();
            var msg = $"WebSocket server started on port {port}.";
            await GlobalLogger.LogToFileAsync(msg);
            _logger.LogInformation(msg);
            await ScheduleService();
        }
        catch (SocketException ex)
        {
            var msg = ex + "Failed to start WebSocket server.";
            await GlobalLogger.LogToFileAsync(msg);
            _logger.LogInformation(msg);
        }
      


    }
    private int GetPort()
    {
        var port =_configuration.GetValue<int>("SocketServer:Port");
        return (port is > 0 and <= 65535) ? port : 8185;
    }
    public void Close()
    {
        if (_webSocketServer != null && _webSocketServer.IsListening)
        {
            _webSocketServer.Stop();
            _logger.LogInformation("WebSocket server stopped.");
        }
    }

    public async void Restart()
    {
        var msg = "Restarting WebSocket server...";
        await GlobalLogger.LogToFileAsync(msg);
        _logger.LogInformation(msg);
        Close();

        // Implement a delay or retry mechanism here if needed
        Task.Delay(500).ContinueWith(_ => Start());
    }

   

public Task CloseAllConnectionsAsync(List<WebSocketSharp.WebSocket> connections)
{
    foreach (var webSocket in connections)
    {
        if (webSocket.ReadyState == WebSocketSharp.WebSocketState.Open) // <-- This is WebSocketSharp.WebSocketState
        {
            webSocket.Close(CloseStatusCode.Normal, "Server shutting down");
        }
    }

    return Task.CompletedTask;
}


    private System.Threading.Timer? Schedular;
    public async Task ScheduleService()
    {
        try
        {
            
            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService< IDeviceDataDownloadService> ();
            Schedular = new System.Threading.Timer(
            callback: SchedularCallback,
            state: null,
            dueTime: TimeSpan.Zero,          // start immediately (or set a delay)
            period: TimeSpan.FromMinutes(5)  // set your actual interval
        );
            try
            {
                foreach (var device in WebSocketHandler.WsDevice)
                {
                    var sn = device.Key;
                    var socket = device.Value;
                    if (socket == null || !socket.IsAlive)
                    {
                        _logger.LogWarning($"[WebSocket] Device {sn} is offline. Removing from list.");

                        // mark offline in DB
                        var dev = await _deviceservice.GetDeviceSn(sn);
                        if (dev != null)
                        {
                            dev.LastConnected = "0";
                            await _deviceservice.HandleDeviceStatus(dev);
                        }

                            WebSocketHandler.WsDevice.Remove(sn, out _);
                    }
                    else
                    {
                        WebSocketHandler.SendToDevice(sn,"{\"cmd\":\"getdevcap\"}");
                    }
                }
                //TimmyLogHelper.Info("Service Next ScheduleService Send devcie commond start");
                #region Send devcie commond
                //Parallel.ForEach(WebSocketLoader._registeredDevices, device =>
                //{
                //    using (ServicesTaskManage commondDB = new ServicesTaskManage())
                //    {
                //        var commands = commondDB.GetDeviceCommand(ConstDeviceModel.Timmy, device.Value.SN);
                //        if (commands != null && commands.Count > 0)
                //        {
                //            DeviceLogHelper.Info(string.Format("{0} commond found for {1}", commands.Count(), device.Value.SN));
                //            foreach (var command in commands)
                //            {
                //                string thredId = Thread.CurrentThread.ManagedThreadId.ToString();
                //                DeviceLogHelper.Info(string.Format("Start running {0} commond for device {1}", command.DeviceCommand, command.DeviceSerialNumber), command.DeviceSerialNumber);
                //                try
                //                {
                //                    RunCommands(command);
                //                }
                //                catch (Exception ex)
                //                {
                //                    DeviceLogHelper.Error(string.Format("End commond {0} Error {1}", command.DeviceCommand, ex.Message), command.DeviceSerialNumber);
                //                }
                //            }
                //        }
                //        else
                //        {
                //            DeviceLogHelper.Info(string.Format("No commond fond {0}", device.Value.SN), device.Value.SN);
                //        }
                //    }
                //});
                //TimmyLogHelper.Info("Service Next ScheduleService Send devcie commond End");
                #endregion
            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync("Service ScheduleService (1) " + ex.Message);
            }

            DateTime scheduledTime = DateTime.Now.AddMinutes(5);
            if (DateTime.Now > scheduledTime)
            {
                scheduledTime = scheduledTime.AddMinutes(5);
            }
            TimeSpan timeSpan = scheduledTime.Subtract(DateTime.Now);
            string schedule = string.Format(" {0} minute(s) {1} seconds(s)", timeSpan.Minutes, timeSpan.Seconds);
            int dueTime = Convert.ToInt32(timeSpan.TotalMilliseconds);
            Schedular.Change(dueTime, Timeout.Infinite);
            await GlobalLogger.LogToFileAsync("Service Next ScheduleService " + schedule);
        }
        catch (Exception ex)
        {
            await GlobalLogger.LogToFileAsync("Service ScheduleService (2) " + ex.Message);
            //using (ServiceController serviceController = new ServiceController("AVTechDevice.AI"))
            //{
            //    serviceController.Stop();
            //}
        }
    }
    private void SchedularCallback(object e)
    {
        this.ScheduleService();
    }

    public static void CheckCommand(WebSocketServerManage handlerInstance, DeviceCommandEntity cmd)
    {
        var handler = new WebSocketHandler(handlerInstance._logger, handlerInstance._serviceProvider);
        handler.Commands(cmd).GetAwaiter().GetResult();
    }

}
