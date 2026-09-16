using System;
using System.Collections.Concurrent;
using DeviceServices.Models;
using System.IO;
using DeviceServices.Services;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office.CustomXsn;
using DocumentFormat.OpenXml.Office.Word;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace AVTech.Device.BusinessServices;

public class WebSocketHandler:WebSocketBehavior
{
    private string msg="No message";
    private const string V = "yyyy-MM-dd";
    private string _clientIp;
    private int _clientPort;
    private ILogger<WebSocketHandler> _logger;
    private IServiceProvider _serviceProvider;
    public static readonly ConcurrentDictionary<string, WebSocketSharp.WebSocket> WsDevice = new ConcurrentDictionary<string, WebSocketSharp.WebSocket>();
    public static readonly Dictionary<long, TaskCompletionSource<string>> PendingResponse = new Dictionary<long, TaskCompletionSource<string>>();
    private static readonly object _lock = new object();
    private bool wsListening = false;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

    public object MappedDeviceGroup { get; private set; }

    public static ConcurrentDictionary<string, WebSocketSharp.WebSocket> GetInstance()
    {
        return WsDevice;
    }

    public WebSocketHandler(ILogger<WebSocketHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _clientIp = " ";
        _clientPort = 0;
        
    }

    
    protected override async void OnOpen()
    {
        try
        {
            await Task.Delay(100);  
            var clientPath = Context?.RequestUri?.PathAndQuery; 
            if (clientPath != null)
            {
                msg = $"Client connected with path: {clientPath}";
                await GlobalLogger.LogToFileAsync(msg);
                _logger.LogInformation(msg);
            }
            else
            {
                 msg = "Client path is null.";
                await GlobalLogger.LogToFileAsync(msg);
                _logger.LogError("Client path is null.");
            }
            //       var remoteEndpoint = Context?.UserEndPoint;
            var remoteEndpoint = Context?.UserEndPoint ?? null; 

            if (remoteEndpoint != null)
            {
                _clientIp = remoteEndpoint.Address.ToString();
                _clientPort = remoteEndpoint.Port;
                msg = $"Someone Socket conn: {_clientIp}:{_clientPort}";
                await GlobalLogger.LogToFileAsync(msg);
                Console.WriteLine($"Someone Socket conn: {_clientIp}:{_clientPort}");
                _logger.LogInformation($"WebSocket connected from {_clientIp}:{_clientPort}");
            }
            else
            {
                msg = "Failed to retrieve remote endpoint.";
                await GlobalLogger.LogToFileAsync(msg);
                Console.WriteLine("Failed to retrieve remote endpoint.");
                _logger.LogWarning("Failed to retrieve remote endpoint.");
                
            }

        }
        catch (Exception ex)
        {
            msg = $"Error during WebSocket connection establishment: {ex.Message}";
            await GlobalLogger.LogToFileAsync(msg);
            _logger.LogError($"Error during WebSocket connection establishment: {ex.Message}");
        }
    }
    protected override async void OnMessage(MessageEventArgs e)
    {
        base.OnMessage(e);
        string message = e.Data;

        string msg = $"----Ask Message---- {message}";
        Console.WriteLine(msg);
        _logger.LogInformation(msg);
        await GlobalLogger.LogToFileAsync(msg);

        var clientPath = Context?.RequestUri?.PathAndQuery;
        if (clientPath != null)
        {
            msg = $"Client connected with path: {clientPath}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
        else
        {
            msg = "Client path is null.";
            Console.WriteLine(msg);
            _logger.LogError(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }

        var remoteEndpoint = Context?.UserEndPoint;
        if (remoteEndpoint != null)
        {
            _clientIp = remoteEndpoint.Address.ToString();
            _clientPort = remoteEndpoint.Port;

            msg = $"Someone Socket conn: {_clientIp}:{_clientPort}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
        else
        {
            msg = "Failed to retrieve remote endpoint.";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }

        await Process(message, _clientIp, _clientPort);
    }

   

    //private async Task AcceptWsClientsAsync()
    //{
    //    wsListening = true;
    //    _logger.LogInformation("[WebSocket] Device monitoring started.");

    //    while (wsListening)
    //    {
    //        try
    //        {
    //            using var scope = _serviceProvider.CreateScope();
    //            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceMasterService>();
    //            foreach (var kvp in WsDevice.ToList()) // WsDevice: ConcurrentDictionary<string, WebSocket>
    //            {
    //                var sn = kvp.Key;
    //                var socket = kvp.Value;

    //                if (socket == null || !socket.IsAlive)
    //                {
    //                    _logger.LogWarning($"[WebSocket] Device {sn} is offline. Removing from list.");

    //                    // mark offline in DB
                        
    //                    await _deviceservice.UpdateDeviceStatus(sn, "0", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));

    //                    WsDevice.Remove(sn, out _);
    //                }
    //                else
    //                {
    //                    _logger.LogInformation($"[WebSocket] Device {sn} is online.");
    //                    await _deviceservice.UpdateDeviceStatus(sn, "1", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "[WebSocket] Error checking device connections.");
    //        }

    //        await Task.Delay(_checkInterval);
    //    }
    //}

    public void SendMessage(string message)
    {
        if (Context.WebSocket?.IsAlive == true)
        {
            string msg = $"Message sent (self): {message}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            GlobalLogger.LogToFileAsync(msg).Wait();

            Send(message);
        }
        else
        {
            string msg = $"Cannot send message — socket not alive. State: {State}";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            GlobalLogger.LogToFileAsync(msg).Wait();
        }
    }

    public static int SendToDevice(string sn, string message, ILogger logger = null)
    {
        if (WsDevice.TryGetValue(sn, out var ws))
        {
            if (ws.IsAlive)
            {
                try
                {
                    ws.Send(message);

                    string msg = $"Message sent to {sn}: {message}";
                    Console.WriteLine(msg);
                    logger?.LogInformation(msg);
                    // avoid .Wait() on async logger if possible; keep for parity with your code
                    GlobalLogger.LogToFileAsync(msg).Wait();

                    return 1; // <-- success (standardized)
                }
                catch (Exception ex)
                {
                    string msg = $"Failed to send message to {sn}. Exception: {ex}";
                    Console.WriteLine(msg);
                    logger?.LogError(ex, msg);
                    GlobalLogger.LogToFileAsync(msg).Wait();

                    return -1; // failure
                }
            }
            else
            {
                string msg = $"WebSocket for {sn} is not alive.";
                Console.WriteLine(msg);
                logger?.LogWarning(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();

                return -1;
            }
        }
        else
        {
            string msg = $"No WebSocket found for {sn}";
            Console.WriteLine(msg);
            logger?.LogWarning(msg);
            GlobalLogger.LogToFileAsync(msg).Wait();

            return -1;
        }
    }


    protected override async void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);

        string msg = $"WebSocket closed. Reason: {e.Reason}, Code: {e.Code}";
        Console.WriteLine(msg);
        _logger.LogInformation(msg);
        await GlobalLogger.LogToFileAsync(msg);

        await HandleCloseAsync();
    }


    private async Task HandleCloseAsync()
    {
        WebSocketSharp.WebSocket webSocket = Context.WebSocket;

        // 🔍 Find matching device by WebSocket
        var match = WsDevice.FirstOrDefault(x => ReferenceEquals(x.Value, webSocket));

        if (match.Key != null)
        {
            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

            // Fetch the device entity by serial number
            var dev = await _deviceservice.GetDeviceSn(match.Key);
            if (dev != null)
            {
                dev.LastConnected = "0";
                await _deviceservice.HandleDeviceStatus(dev);
                WsDevice.Remove(match.Key, out _);

                string msg = $"Device {match.Key} disconnected and status updated.";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
            else
            {
                string msg = $"Device not found for serial number: {match.Key}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }

            string closeMsg = $"WebSocket closed: {this.Context.RequestUri}";
            Console.WriteLine(closeMsg);
            _logger.LogInformation(closeMsg);
            await GlobalLogger.LogToFileAsync(closeMsg);
        }
        else
        {
            string msg = $"WebSocket connection alive: {this.Context.RequestUri}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
    }

    protected override async void OnError(WebSocketSharp.ErrorEventArgs e)
    {
        string msg = $"WebSocket error: {e.Message}";
        Console.WriteLine(msg);
        _logger.LogError(e.Exception, msg);
        await GlobalLogger.LogToFileAsync(msg);

        await HandleCloseAsync();
    }


    private async Task Process(string message, string clientPath, int clientPort)
    {
        try
        {
            JObject jsonNode = JObject.Parse(message);
            var cmd = jsonNode.Value<string>("cmd");
            var ret = jsonNode.Value<string>("ret");
            // prefer cmd if available
            if (cmd != null)
            {
                ret = cmd;
            }
            using var scope = _serviceProvider.CreateScope();
            var _machineCommand = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

            switch (ret)
            {
                case "reg":
                    {
                       
                        await GetDeviceInfo(jsonNode, _machineCommand, clientPath, clientPort);
                        //await AcceptWsClientsAsync();
                        //await HandleDeviceStatus(jsonNode,clientPath,clientPort);
                        break;
                    }

                case "sendlog":
                    {
                       
                        string? result = jsonNode.Value<string?>("result");
                        if(!String.IsNullOrEmpty(result))
                        await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await GetAttendance(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }

                case "senduser":
                    {
                       
                       // var easyaccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await SetUserInfo(jsonNode, _machineCommand);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }

                case "getalllog":
                    {
                     
                        var count = jsonNode.Value<Int32>("count");
                        var result = jsonNode.Value<string?>("result");
                        var indexto= jsonNode.Value<Int32>("to");
                        var sn = jsonNode.Value<string?>("sn");
                       
                        await GetAttendance(jsonNode, _machineCommand, clientPath, clientPort);
                        if (indexto < count)
                        {
                            var command = new
                            {
                                cmd = "getalllog",
                                sn = sn,
                                stn = false
                            };
                            string jsoncommand = JsonConvert.SerializeObject(command);
                            Console.WriteLine("index:" + indexto + ";count:" + count + ";");
                            await GlobalLogger.LogToFileAsync("index:" + indexto + ";count:" + count + ";");
                            SendToDevice(sn, jsoncommand);
                        }
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "setuserinfo":
                    {
                      
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "deleteuser":
                    {
                     
                       // var easyaccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "getuserinfo":
                    {
                      
                        //var easyaccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await SetUserInfo(jsonNode, _machineCommand);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "getallusers":
                    {
                        //var _employeeservice = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
                        //var _masterDDLservice = scope.ServiceProvider.GetRequiredService<IMasterDDLService>();
                        //var easyaccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                        var result = jsonNode.Value<string>("result");
                        if(result != null)
                        {
                            var count = jsonNode.Value<Int32>("count");
                            var index = jsonNode.Value<Int32>("index");
                            var sn = jsonNode.Value<string>("sn");
                            await SetUserInfo(jsonNode, _machineCommand);
                            if (index < (count - 1))
                            {
                                var command = new
                                {
                                    cmd = "getallusers",
                                    sn = sn,
                                    stn = false
                                };
                                string jsoncommand = JsonConvert.SerializeObject(command);
                                Console.WriteLine("index:" + index + ";count:" + count + ";");
                                await GlobalLogger.LogToFileAsync("index:" + index + ";count:" + count + ";");
                                SendToDevice(sn, jsoncommand);
                            }
                            else
                            {
                                await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                            }
                        }
                        

                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "adduser":
                    {
                      
                      //  var easyaccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "settime":
                    {
                       
                        
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                            await UpdateCommandAsync(jsonNode, _machineCommand, clientPath, clientPort);
                        await HandleDeviceStatus(jsonNode, clientPath, clientPort);
                        break;
                    }
                case "getdevcap": 
                    {
                        string? sn = jsonNode.Value<string?>("sn");
                        string? result = jsonNode.Value<string?>("result");
                        if (!String.IsNullOrEmpty(result))
                        {
                            var entity = new DeviceMasterEntity
                            {
                                DeviceSerialNumber = sn,
                                TotalUser = jsonNode.Value<string?>("usersize") ?? "0",
                                TotalCardUser = jsonNode.Value<string?>("cardsize") ?? "0",
                                TotalFaceUser = jsonNode.Value<string?>("facesize") ?? "0",
                                UsedCardUser = jsonNode.Value<string?>("usedcard") ?? "0",
                                UsedFaceUser= jsonNode.Value<string?>("usedface") ?? "0",
                                UsedUser = jsonNode.Value<string?>("useduser") ?? "0",
                                IsConnected="1"


                            };
                            await _machineCommand.HandleDeviceStatus(entity, clientPath, clientPort);
                        }
                            break;
                    }
                default:
                    // optional: handle unknown commands
                    msg = "Unknown command received: {Ret}" + ret;
                    await GlobalLogger.LogToFileAsync(msg);
                    _logger.LogWarning(msg);
                    break;
            }
        }
        catch (JsonReaderException ex)
        {
            msg = ex+ "Fail: {Message}"+ message;
            await GlobalLogger.LogToFileAsync(msg);
            _logger.LogWarning(msg);
        }
        catch (Exception ex)
        {
            msg = ex.Message+"Fail";
            await GlobalLogger.LogToFileAsync(msg);
            _logger.LogWarning(msg);
        }
    }



    private async Task GetDeviceInfo(JObject jsonNode, IDeviceDataDownloadService _deviceservice, string clientIp,
                                   int clientPort)
    {
        try
        {
            WebSocketSharp.WebSocket webSocket = Context.WebSocket;
            var sn = jsonNode.Value<string>("sn");
            if (sn != null)
            {
                string? macAddress = jsonNode["devinfo"]?["mac"]?.ToString();
                string? TotalUser = jsonNode["devinfo"]?["usersize"]?.ToString();
                string? TotalFaceUser = jsonNode["devinfo"]?["facesize"]?.ToString();
                string? TotalCardUser = jsonNode["devinfo"]?["cardsize"]?.ToString();
                string? DeviceUser = jsonNode["devinfo"]?["useduser"]?.ToString();
                string? FaceUser = jsonNode["devinfo"]?["usedface"]?.ToString();
                string? CardUser = jsonNode["devinfo"]?["usedcard"]?.ToString();
                string? DevInfo = jsonNode["devinfo"].ToString();
                
              
               
                    var device = new DeviceMasterEntity
                    {
                        DeviceSerialNumber = sn,
                        DeviceName = sn,
                        DeviceIp = clientIp,
                        DevicePort = clientPort.ToString(),
                        IsRegistered = "0",
                        DeviceType = "A",
                        InoutFlag = "I",
                        IsActive = "1",
                        IsConnected = "1",
                        LastConnected = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"),
                        CreatedDate = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"),
                        DeviceInfo = DevInfo,
                        MacAddress = macAddress,
                        DeviceUser = DeviceUser + "/" + TotalUser,
                        FaceUser = FaceUser + "/" + TotalFaceUser,
                        CardUser = CardUser + "/" + TotalCardUser,
                        TotalCardUser = TotalCardUser,
                        TotalFaceUser= TotalFaceUser,
                        TotalUser= TotalUser,
                        UsedCardUser=CardUser,
                        UsedFaceUser=FaceUser,
                       
                    };

                await _deviceservice.SetDeviceInfo(device);
                
                if (!WsDevice.ContainsKey(sn))
                {
                    WsDevice.AddOrUpdate(
                                               sn,
                                               // Value factory if the key does not exist
                                               key => webSocket,
                                               // Update factory if the key already exists
                                               (key, oldValue) => webSocket
                                           );

                }
                var response = new
                {
                    ret = "reg",
                    result = true,
                    cloudtime = DateTime.Now
                };
                var responseData = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(response));
                webSocket.Send(responseData);
                //SetDeviceTime(sn, webSocket);
            }
            else
            {
                var errorResponse = new
                {
                    ret = "reg",
                    result = false,
                    reason = 1
                };

                var responseData = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                webSocket.Send(responseData);
            }
        }
        catch (Exception)
        {

            throw;
        }

    }
    public void SetDeviceTime(string sn, WebSocketSharp.WebSocket webSocket)
    {
        if (string.IsNullOrWhiteSpace(sn)) throw new ArgumentException("sn is required", nameof(sn));
        if (webSocket == null) throw new ArgumentNullException(nameof(webSocket));
        if (!webSocket.IsAlive) throw new InvalidOperationException("WebSocket is not connected.");

        var setTimeCommand = new
        {
            cmd = "settime",
            sn = sn,
            cloudtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = System.Text.Json.JsonSerializer.Serialize(setTimeCommand);

        try
        {
            string msg = "Settime JSON sent: " + json;
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            GlobalLogger.LogToFileAsync(msg).Wait();

            webSocket.Send(json);
        }
        catch (Exception ex)
        {
            string msg = "Send failed: " + ex;
            Console.WriteLine(msg);
            _logger.LogError(ex, msg);
            GlobalLogger.LogToFileAsync(msg).Wait();
            throw;
        }
    }

    public async Task GetAttendance(JObject jsonNode, IDeviceDataDownloadService _service, string clientIp, int clientPort)
    {
        var sn = jsonNode.Value<string>("sn");
        int count = jsonNode.Value<int>("count");
        int logIndex = jsonNode.Value<int>("logindex");
        List<PunchLogEntity> recordAll = new List<PunchLogEntity>();
        WebSocketSharp.WebSocket webSocket = Context.WebSocket;

        if (count > 0)
        {
            JArray records = jsonNode.Value<JArray>("record");
            for (int i = 0; i < records.Count(); i++)
            {
                JObject type = records[i] as JObject;
                string enrollId = type["aliasid"]?.Value<string>() == null? type["enrollid"]?.Value<string>(): type["aliasid"]?.Value<string>();
                string timeStr = type["time"]?.ToString() ?? "";
                int inOut = type["inout"]?.Value<int>() ?? 0;
                if (type["image"] != null)
                {
                  
                   
                    var logimagepath = type["image"]?.ToString();

                }
                if (type["Card"] != null)
                {
                    var ActualCardNumber = type["Card"]?.ToString();
                }


                PunchLogEntity record = new PunchLogEntity
                {
                    DeviceSerialNumber = sn,
                    DeviceEmployeeCode = enrollId,
                    PunchDateTime = DateTime.Parse(timeStr),
                    CreatedDate = DateTime.Now.ToString(),
                    PunchInoutFlag = inOut.ToString(),
                    IsManual = "0",
                    IsDeleted = "0",
                    PhtBase64Img = type["image"] != null ? type["image"].ToString() : null,
                    ActualCardNumber = type["Card"] != null ? type["Card"].ToString() : null
                };

              
                recordAll.Add(record);
            }

            string response = logIndex >= 0
                ? $"{{\"ret\":\"sendlog\",\"result\":true,\"count\":{count},\"logindex\":{logIndex},\"cloudtime\":\"{DateTime.Now}\"}}"
                : $"{{\"ret\":\"sendlog\",\"result\":true,\"cloudtime\":\"{DateTime.Now}\"}}";

            webSocket.Send(Encoding.UTF8.GetBytes(response));

            foreach (var record in recordAll)
            {
                if (record != null)
                {
                    await _service.SetAttendance(record);
                }
            }

            string msg = $"Attendance logs processed for SN {sn}, Count={count}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
        else if (count == 0)
        {
            string response = "{\"ret\":\"sendlog\",\"result\":false,\"reason\":1}";
            webSocket.Send(Encoding.UTF8.GetBytes(response));

            string msg = $"Attendance empty log response sent for SN {sn}";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
    }

    public async Task<string> SaveDeviceLogImageAsync(string base64Image, string deviceSerialNumber, string punchDate, string enrollId)
    {
        try
        {
            string rootPath = Directory.GetCurrentDirectory();
            string parentPath = Directory.GetParent(rootPath)!.FullName;
            string documentsPath = System.IO.Path.Combine(parentPath, "Documents");

            if (base64Image.StartsWith("data:image"))
            {
                var commaIndex = base64Image.IndexOf(',');
                base64Image = base64Image[(commaIndex + 1)..];
            }

            byte[] imageBytes = Convert.FromBase64String(base64Image);

            var safedate = (punchDate ?? " ").Split("_");
            var actualdate = safedate[0];
            string baseFolder = System.IO.Path.Combine("logImage", deviceSerialNumber, actualdate);
            string directoryPath = System.IO.Path.Combine(documentsPath, baseFolder);
            Directory.CreateDirectory(directoryPath);

            string fileName = $"{enrollId}_{punchDate}.jpg";
            string fullImagePath = System.IO.Path.Combine(directoryPath, fileName);

            await File.WriteAllBytesAsync(fullImagePath, imageBytes);
            string dbImagePath = System.IO.Path.Combine(baseFolder, fileName).Replace("\\", "/");

            string msg = $"Image saved for SN {deviceSerialNumber}, Path={dbImagePath}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);

            return dbImagePath;
        }
        catch (Exception ex)
        {
            string msg = $"Failed to save image for SN {deviceSerialNumber}: {ex.Message}";
            Console.WriteLine(msg);
            _logger.LogError(ex, msg);
            await GlobalLogger.LogToFileAsync(msg);
            throw;
        }
    }

    public async Task SetUserInfo(JObject jsonNode, IDeviceDataDownloadService _service)
    {
        try
        {
            if (jsonNode != null)
            {
                var sn = jsonNode.Value<string>("sn");
                string enroll_no = jsonNode.Value<string>("aliasid") ?? jsonNode.Value<string>("enrollid");
                var base64Image = jsonNode.Value<string>("backupnum") == "50" ? jsonNode.Value<string>("record") : null;
                string cardno = jsonNode.Value<string>("backupnum") == "11" ? jsonNode.Value<string>("record") : null;
                var name = jsonNode.Value<string>("name");
                WebSocketSharp.WebSocket webSocket = Context.WebSocket;
               

                if (!string.IsNullOrEmpty(enroll_no))
                {
                    
                   
                      

                        var entity = new EmployeeEntity
                        {
                            //CompanyId = compId,
                            EmployeeCode = enroll_no,
                            DeviceEmployeeCode = enroll_no,
                           DevSerialNo=sn,
                            JoiningDate = DateTime.Now.ToString("yyyy-MM-dd"),
                            //MappedDeviceGroup = devicegroup.ToString(),
                            //FaceImagePath = dbImagePath,
                            EmpBase64Img=base64Image,
                            CardNo = cardno,
                           
                        };
                        await _service.SetUserInfo(entity); ;

                        string msg = $"Added new employee {enroll_no} from device {sn}";
                        Console.WriteLine(msg);
                        _logger.LogInformation(msg);
                        await GlobalLogger.LogToFileAsync(msg);
                    

                    string response = $"{{\"ret\":\"senduser\",\"result\":true,\"cloudtime\":\"{DateTime.Now}\"}}";
                    webSocket.Send(Encoding.UTF8.GetBytes(response));
                }
            }
        }
        catch (JsonReaderException ex)
        {
            string msg = $"JSON parsing failed in SetUserInfo: {ex.Message}";
            Console.WriteLine(msg);
            _logger.LogError(ex, msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
        catch (Exception ex)
        {
            string msg = $"Unhandled error in SetUserInfo: {ex.Message}";
            Console.WriteLine(msg);
            _logger.LogError(ex, msg);
            await GlobalLogger.LogToFileAsync(msg);
        }
    }

    private async Task HandleDeviceStatus(JObject jsonNode, string clientIp, int clientPort)
    {
        var sn = jsonNode.Value<string>("sn");
        WebSocketSharp.WebSocket webSocket = Context.WebSocket;
        if (string.IsNullOrEmpty(sn))
        {
            string msg = $"Device SN missing in status check from {clientIp}:{clientPort}";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            await GlobalLogger.LogToFileAsync(msg);
            return;
        }

        //using var scope = _serviceProvider.CreateScope();
        //var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceMasterService>();
        //var db = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
        if (!WsDevice.ContainsKey(sn))
        {
            WsDevice.AddOrUpdate(
                                                  sn,
                                                  // Value factory if the key does not exist
                                                  key => webSocket,
                                                  // Update factory if the key already exists
                                                  (key, oldValue) => webSocket
                                              );

        }
        else
        {
            WsDevice[sn] = webSocket;
        }
        // Any message received is proof the device is alive
        //if (WsDevice.TryGetValue(sn, out var ws))
        //{
        //    if (ws != null && ws.IsAlive)
        //    {
        //        // Device is alive, update status to online if not already
        //        await _deviceservice.UpdateDeviceStatus(sn, "1", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));
        //        return;
        //    }
        //    else
        //    {
        //        // WebSocket object exists but not alive -> mark offline
        //        if (WsDevice.Remove(sn, out _))
        //        {
        //            await _deviceservice.UpdateDeviceStatus(sn, "0", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));

        //            string msg = $"Device {sn} marked offline. Connection closed.";
        //            Console.WriteLine(msg);
        //            _logger.LogInformation(msg);
        //            await GlobalLogger.LogToFileAsync(msg);
        //        }
        //    }
        //}
        //else
        //{
        //    // No entry in WsDevice for this SN
        //    string msg = $"No active WebSocket found for SN: {sn}";
        //    Console.WriteLine(msg);
        //    _logger.LogDebug(msg);
        //    await GlobalLogger.LogToFileAsync(msg);
        //}
    }


    private async Task<string> SaveEmployeeImageAsync(string base64Image,string compId,string EmployeeCode)
    {
        string rootPath = Directory.GetCurrentDirectory(); 
        string parentPath = Directory.GetParent(rootPath)!.FullName; // goes up one level
        string documentsPath = System.IO.Path.Combine(parentPath, "Documents"); 
        string dbImagePath; 
        if (base64Image != "0") 
        { 
            if(base64Image.StartsWith("record")) 
            { var commaIndex = base64Image.IndexOf(','); 
                base64Image = base64Image[(commaIndex + 1)..];
            }
            byte[] imageBytes = Convert.FromBase64String(base64Image); // Step 2: Create the directory path
            string baseFolder = System.IO.Path.Combine(compId, "DeviceFace");
            string directoryPath = System.IO.Path.Combine(documentsPath, baseFolder);
            Directory.CreateDirectory(directoryPath); 
            string fileName = $"{EmployeeCode}.png"; string fullImagePath = System.IO.Path.Combine(directoryPath, fileName); // Step 4: Save image
            await File.WriteAllBytesAsync(fullImagePath, imageBytes);
            dbImagePath = System.IO.Path.Combine(baseFolder, fileName).Replace("\\", "/"); 
        } 
        else 
        { 
            dbImagePath = " "; 
        }
        return dbImagePath;
    }

    private async Task UpdateCommandAsync(JObject jsonNode, IDeviceDataDownloadService _service, string clientIp, int clientPort)
    {
        var sn = jsonNode.Value<string>("sn");
        if (string.IsNullOrEmpty(sn))
        {
            string msg = $"Device SN missing in status check from {clientIp}:{clientPort}";
            Console.WriteLine(msg);
            _logger.LogWarning(msg);
            await GlobalLogger.LogToFileAsync(msg);
            return;
        }
        // Process command responses if present
        string? cmd = jsonNode.Value<string>("cmd") ?? jsonNode.Value<string>("ret");
        string? result = jsonNode.Value<string?>("result");

        var CMD = new DeviceCommandEntity
        {
            DeviceCommand = cmd,
            CommondResponce = result
            


        };


        await _service.UpdateCommandAsync(CMD);
    }

   
    public async Task Commands(DeviceCommandEntity cmd)
    {
        using var scope = _serviceProvider.CreateScope();
        DeviceCommandEntity  device = new DeviceCommandEntity();
        var _deviceService = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
        switch (cmd.DeviceCommand)  // use Value (your DeviceCommand)  
        {
            
            case "DEVICE_LOG":
                device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                {
                    var command = new
                    {
                        cmd = "getalllog",
                        sn = device.DeviceSerialNumber,
                        stn = true,
                        from = device.FromDate,
                        to = device.ToDate,
                    };
                    await SendCommandToDevice(cmd, command, "getalllog", _deviceService);
                }
                break;

            case "ADD_USER":
            case "UPDATE_USER":
                device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                {
                    var employee =  await _deviceService.GetEmployee(cmd);

                    if (employee != null)
                    {
                        string base64String = " ";
                        if (!string.IsNullOrEmpty(employee.FaceImagePath))
                        {
                            string rootPath = Directory.GetCurrentDirectory();
                            string basePath = Directory.GetParent(rootPath)!.FullName;
                            var imagePath = System.IO.Path.Combine(basePath, "Documents", employee.FaceImagePath);
                            byte[] imageBytes = File.ReadAllBytes(imagePath);
                            base64String = Convert.ToBase64String(imageBytes);
                        }

                        var command = new
                        {
                            cmd = "setuserinfo",
                            sn = device.DeviceSerialNumber,
                            enrollid = employee.DeviceEmployeeCode,
                            name = employee.EmployeeName,
                            backupnum = 50,
                            admin = employee.EmployeeTypeId == 2 ? 1 : 0,
                            record = base64String,
                        };

                        await SendCommandToDevice(cmd, command, "setuserinfo", _deviceService);
                    }
                }
                break;

            
            case "DOWNLOAD_USER":
                 device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                {
                    var command = new
                    {
                        cmd = "getuserinfo",
                        enrollid = int.Parse(device.DeviceUserId),
                        sn = device.DeviceSerialNumber,
                        backupnum = 50
                    };
                    await SendCommandToDevice(cmd, command, "getuserinfo", _deviceService);
                }
                // handle senduser  
                break;
            case "DELETE_USER":
                device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                    if (device != null)
                {
                    var command = new
                    {
                        cmd = "deleteuser",
                        sn = device.DeviceSerialNumber,
                        enrollid = device.DeviceUserId,
                        backupnum = 13
                    };
                    await SendCommandToDevice(cmd, command, "deleteuser", _deviceService);
                }
                break;
            case "GET_USER":
                device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                {
                    var command = new
                    {
                        cmd = "getallusers",
                        sn = device.DeviceSerialNumber,
                        stn = true
                    };
                    await SendCommandToDevice(cmd, command, "getallusers", _deviceService);
                }
                break;
            case "ENROLL_ID":
                device = await _deviceService.GetDeviceCommand(cmd.DeviceCommandId);
                if (device != null)
                {
                    var command = new
                    {
                        cmd = "adduser",
                        sn = device.DeviceSerialNumber,
                        enrollid = device.DeviceUserId,
                        backupnum = 50,
                        admin = 0,
                        flag = 10
                    };
                    await SendCommandToDevice(cmd, command, "adduser", _deviceService);
                }
                break;

            default:
                // unknown command  
                break;
        }
    }

    private async Task SendCommandToDevice(DeviceCommandEntity command, object commandPayload, string commandType,IDeviceDataDownloadService _deviceService)
    {
        var device = await _deviceService.GetDeviceCommand(command.DeviceCommandId);

        if (device != null)
        {
            device.CommondStatusId = 2;
            device.CommondResponce = "Pending";

            device.UpdatedDate = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

            await _deviceService.UpdateCommandAsync(command);
            string jsonCommand = JsonConvert.SerializeObject(commandPayload);
            var result = WebSocketHandler.SendToDevice(device.DeviceSerialNumber, jsonCommand);

            
        }
    }





}

