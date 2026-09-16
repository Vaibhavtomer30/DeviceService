//using AVTech.Device.BusinessServices.Implementation;
//using AVTech.Device.BusinessServices.Services;
//using AVTech.Device.Common.Converter;
//using AVTech.Device.DataServices;
//using AVTech.Device.Models;
using DeviceServices.Models;
using DeviceServices.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;
//using ZstdSharp.Unsafe;
//using static Org.BouncyCastle.Crypto.Engines.SM2Engine;




namespace DeviceServices.Services

{
    public class ZktTCPHandler : WebSocketBehavior
    {
        private string msg = "No message";
        private string _clientIp;
        private int _clientPort;
        private bool listening = false;
        private TcpListener _zktTcpListener;
        private ILogger<ZktTCPHandler> _logger;
        private IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, DateTime> _lastSeen = new();
        private static readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _cmdWaiters
    = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks
     = new();

        private System.Windows.Forms.Timer _monitorTimer;


        public ZktTCPHandler(ILogger<ZktTCPHandler> zktLogger, IServiceProvider service, IConfiguration configuration)
        {
            _logger = zktLogger;
            _serviceProvider = service;
            _configuration = configuration;
        }

        public event Action<string> OnNewMachine;
        private void UpdateLastSeen(string sn)
        {
            if (_lastSeen.ContainsKey(sn))
                _lastSeen[sn] = DateTime.Now;  // update
            else
                _lastSeen.Add(sn, DateTime.Now);
        }

        public void Start()
        {
            int port = GetPort();
            var tcpport = port - 1;
            _zktTcpListener = new TcpListener(System.Net.IPAddress.Any, tcpport);
            try
            {
                _clientIp = System.Net.IPAddress.Any.ToString();
                _clientPort = tcpport;
                _zktTcpListener.Start();
                listening = true;
                Task.Run(() => AcceptZktClientsAsync());
                StartDeviceMonitor();
                msg = $"Tcp Listening started on port {tcpport}.";
                Console.WriteLine(msg);
                _logger.LogInformation(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
            catch (SocketException ex)
            {
                msg = $"Failed to start Tcp Listening. {ex}.";
                _logger.LogError(msg);
                Console.WriteLine(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();

            }
        }

        private int GetPort()
        {
            var port = _configuration.GetValue<int>("SocketServer:Port");
            return (port is > 0 and <= 65535) ? port : 8184;
        }


        public void StartDeviceMonitor()
        {
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 30000; // 30 seconds
            _monitorTimer.Tick += async (sender, e) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

                foreach (var kv in _lastSeen.ToList())
                {
                    if ((DateTime.Now - kv.Value).TotalSeconds > 300)
                    {
                        var entity = new DeviceMasterEntity
                        {
                            DeviceSerialNumber = kv.Key,
                            //LastConnected = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"),
                            IsConnected = "0"

                        };
                        await _deviceservice.HandleDeviceStatus(entity);
                        _lastSeen.Remove(kv.Key);
                        msg = $"[ZKT/ESSL] Device {kv.Key} timed out (no heartbeat).";
                        Console.WriteLine(msg);
                        _logger.LogInformation(msg);
                        GlobalLogger.LogToFileAsync(msg).Wait();

                    }
                }
            };
        }

        private async Task AcceptZktClientsAsync()
        {
            while (listening)
            {
                try
                {
                    TcpClient client = await _zktTcpListener.AcceptTcpClientAsync();

                    string msg = "[ZKT/ESSL] Device connected.";
                    Console.WriteLine(msg);
                    _logger.LogInformation(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    await Task.Delay(2000);
                    _ = Task.Run(() => HandleZktDeviceDataAsync(client));



                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "");
                    string msg = $"[ZKT/ESSL] Error accepting device.{ex}";
                    Console.WriteLine(msg);
                    _logger.LogError(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }
            }

        }

        private async Task HandleZktDeviceDataAsync(TcpClient client)
        {

            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            var esslService = scope.ServiceProvider.GetRequiredService<EsslTCPHandler>();

            var stream = client.GetStream();
            var buffer = new byte[1024 * 1024 * 2];


            int bytesRead = 0;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                string msge = $"[ZKT/ESSL] Error reading from device.{ex}";
                Console.WriteLine(msge);
                _logger.LogError(msge);
                await GlobalLogger.LogToFileAsync(msge);
                return;
            }

            if (bytesRead == 0) return;
            // disconnected
            //string hexDump = BitConverter.ToString(buffer, 0, bytesRead);
            //_logger.LogInformation($"[ZKT] Raw Hex: {hexDump}");
            string strReceive = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            await GlobalLogger.LogToFileAsync(
    $"Index cdata: {IndexOfEx(strReceive, "cdata")}"
);

            await GlobalLogger.LogToFileAsync(
                $"Index cdata?: {IndexOfEx(strReceive, "cdata?")}"
            );
            await GlobalLogger.LogToFileAsync(
                $"Index cdata?: {IndexOfEx(strReceive, "aspx")}"
            );
            if (ContainsOfEx(strReceive, "aspx"))
            {
                _logger.LogInformation("[ESSL] Received" + strReceive);

                string SN = GetValueByNameInPushHeader(strReceive, "SN");
                UpdateLastSeen(SN);
                var handlerInstance = _serviceProvider.GetRequiredService<EsslTCPHandler>();
                await Task.Run(() => EsslTCPHandler.Start(handlerInstance, buffer, client));
                return;

            }
            string msg = $"[ZKT] Received: {strReceive}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
            // await UpdateDeviceStatus(strReceive, _deviceservice);
            await GlobalLogger.LogToFileAsync("USING");
            using (client)
            {
                await GlobalLogger.LogToFileAsync("using client");

                if (IndexOfEx(strReceive, "cdata") > 0 || IndexOfEx(strReceive, "cdata?") > 0)
                {
                    // cdataProcess(bytesRead,client);
                    //string response = "OK\n";
                    //byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    //await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await GlobalLogger.LogToFileAsync("ZKT data");
                    await cdataProcess(buffer, client, _deviceservice);
                }
                else if (IndexOfEx(strReceive, "getrequest?") > 0)
                {
                    await CheckPendingCmd(strReceive, client);


                }
                else if (IndexOfEx(strReceive, "devicecmd?") > 0)
                {
                    DeviceCmdProcess(strReceive, client);
                }
                else if (IndexOfEx(strReceive, "ping?") > 0)
                {
                    SendDataToDevice("200 OK", "OK\r\n", client);
                    //client.Close();
                }

                else
                {
                    //UnknownCmdProcess(endsocket);
                    //if (OnError != null)
                    //{
                    //    OnError("UnKnown message from device: " + strReceive);
                    //}
                    //Console.WriteLine("Unkonwn Commnad"+strReceive);
                    await GlobalLogger.LogToFileAsync("ZZZ");
                    _logger.LogInformation("Unkonwn Commnad" + strReceive);

                    await GlobalLogger.LogToFileAsync("Unkonwn Commnad" + strReceive);
                    SendDataToDevice("200 OK", "OK\r\n", client);
                }






                // 👉 Handle the ZKT payload here (e.g., parse attendance logs)

                // Send back ACK to device


            }
            //string SN = GetValueByNameInPushHeader(strReceive, "SN");
            //await _deviceservice.UpdateDeviceStatus(SN, "0", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));
            msg = "[ZKT] Device disconnected.";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);
            await GlobalLogger.LogToFileAsync(msg);
        }

        //private async Task UpdateDeviceStatus(string StrReceive, IDeviceMasterService _deviceService)
        //{
        //    string SN = GetValueByNameInPushHeader(StrReceive, "SN");
        //    DeviceMasterEntity machine = await _deviceService.GetAllDeviceMasterBySerialNumber(SN);
        //    if (null == machine)
        //    {

        //        return;

        //    }
        //    else
        //    {
        //        await _deviceService.UpdateDeviceStatus(SN, "1", DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"));
        //    }
        //}
        private async Task DeviceCmdProcess(string strReceive, TcpClient client)
        {

            string errMessage = string.Empty;
            string machineSN = strReceive.Substring(IndexOfEx(strReceive, "\r\n\r\n") + 4);
            string[] cmdret = machineSN.Split('\n');

            foreach (var cnd in cmdret)
            {
                if (String.IsNullOrEmpty(cnd))
                    continue;
                int index = Int32.Parse(GetValueByNameInPushHeader(cnd, "ID"));
                int returntyp = Int32.Parse(GetValueByNameInPushHeader(cnd, "Return"));

                UpdateCommand(index, returntyp);
                //if (_cmdWaiters.TryRemove(index, out var tcs))
                //{
                //    tcs.TrySetResult(true); // ✅ allow next command }
                //}

                //remoteSocket.Close();

                //_deviceCmdBll.Update(strReceive.Substring(index));
            }
            SendDataToDevice("200 OK", "OK\r\n", client);
        }


        public static int IndexOfEx(string str, string value, int startIndex = 0, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return str.IndexOf(value, startIndex, stringComparison);
        }

        private static bool ContainsOfEx(string str, string value)
        {
            return str.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private void SendDataToDevice(string sStatusCode, string sDataStr, TcpClient tcp)
        {
            try
            {
                // Convert body to bytes (ZKTeco expects GB2312 encoding)
                byte[] bData = Encoding.GetEncoding("gb2312").GetBytes(sDataStr);

                // Build HTTP header
                string sHeader = "HTTP/1.1 " + sStatusCode + "\r\n";
                sHeader += "Content-Type: text/plain\r\n";
                sHeader += "Accept-Ranges: bytes\r\n";
                sHeader += "Date: " + DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).ToString("r") + "\r\n";
                sHeader += "Content-Length: " + bData.Length + "\r\n\r\n";

                // Get stream for writing
                NetworkStream stream = tcp.GetStream();
                // Send header
                byte[] headerBytes = Encoding.GetEncoding("gb2312").GetBytes(sHeader);
                SendToClient(stream, headerBytes);

                // Send body
                SendToClient(stream, bData);

            }
            catch (Exception ex)
            {
                string msg = "[SendDataToDevice Error] " + ex.Message;
                Console.WriteLine(msg);
                _logger.LogError(msg);
                GlobalLogger.LogToFileAsync(msg).Wait();
            }
        }

        private async void SendToClient(NetworkStream stream, byte[] bSendData)
        {
            try
            {
                if (stream.CanWrite)
                {
                    await stream.WriteAsync(bSendData, 0, bSendData.Length);
                    stream.Flush();

                    // (Optional) Debug log
                    string logData = Encoding.ASCII.GetString(bSendData);
                    string msg = "[Sent] " + logData;
                    Console.WriteLine(msg);
                    _logger.LogInformation(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }
                else
                {
                    string msge = "Stream not writable.";
                    Console.WriteLine(msge);
                    _logger.LogInformation(msge);
                    await GlobalLogger.LogToFileAsync(msge);
                }
            }
            catch (Exception ex)
            {
                string msg = "[SendToClient Error] " + ex.Message;
                Console.WriteLine(msg);
                _logger.LogError(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
        }

        private async Task GetRequestProcess(string strReceive, TcpClient tcp)
        {
            try
            {
                // string sBuffer = Encoding.GetEncoding("gb2312").GetString(bReceive);
                string cmdstring = "OK\r\n";
                string SN = GetValueByNameInPushHeader(strReceive, "SN");
                string ReplyCode = "200 OK";
                using var scope = _serviceProvider.CreateScope();
                UpdateLastSeen(SN);

                var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

                DeviceMasterEntity device = await _deviceservice.GetDeviceSn(SN);

                if (null == device)
                {
                    ReplyCode = "401 Unauthorized";
                    cmdstring = "Device Unauthorized";
                }
                else
                {


                    string strDevInfo = GetValueByNameInPushHeader(strReceive, "INFO");
                    if (string.IsNullOrEmpty(strDevInfo))
                    {


                        //device.LastConnected = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");
                        device.IsConnected = "1";


                        await _deviceservice.HandleDeviceStatus(device);
                        cmdstring = cmdstring + "\r\n";
                    }

                }
                SendDataToDevice(ReplyCode, cmdstring, tcp);


                // tcp.Close();
            }
            catch (Exception ex)
            {

                throw;
            }

        }



        private async Task cdataProcess(byte[] bReceive, TcpClient tcp, IDeviceDataDownloadService _devService)
        {
            await GlobalLogger.LogToFileAsync("cdata-process");
            string sBuffer = Encoding.ASCII.GetString(bReceive).TrimEnd().TrimEnd('\0');
            string SN = GetValueByNameInPushHeader(sBuffer, "SN");
            string ReplyCode = "200 OK";
            string strReply = "OK";
            UpdateLastSeen(SN);


            if (sBuffer.Substring(0, 3) == "GET") // iclock option
            {
                if (IndexOfEx(sBuffer, "options=all", 0) > 0)
                {
                    await GlobalLogger.LogToFileAsync("cdata-process 2");
                    //SendDataToDevice(ReplyCode, strReply, tcp);
                    ReplyCode = await InitDeviceConnected(SN, strReply);

                    //await HandleGetRequest(tcp, sBuffer);
                    string[] parts = ReplyCode.Split('~');
                    SendDataToDevice(parts[1], parts[0], tcp);
                    return;
                }
                else
                {
                    ReplyCode = "400 Bad Request";
                    strReply = "Unknown Command";
                    SendDataToDevice(ReplyCode, strReply, tcp);
                    return;
                }
            }

            if (sBuffer.Substring(0, 4) == "POST")
            {

                if (IndexOfEx(sBuffer, "~DeviceName") > 0)
                {
                    try
                    {

                        var devInfo = sBuffer.Substring(IndexOfEx(sBuffer, "~DeviceName"));
                        var list = devInfo.Split(',');
                        var MaxFace = list
                                    .FirstOrDefault(x => x.StartsWith("~MaxFaceCount"))
                                    ?.Split('=')[1];
                        var FaceCount = list
                                    .FirstOrDefault(x => x.StartsWith("FaceCount"))
                                    ?.Split('=')[1];


                        var device = new DeviceMasterEntity
                        {
                            DeviceSerialNumber = SN,
                            DeviceName = list[0].Substring("~DeviceName=".Length),
                            DeviceIp = list
                                    .FirstOrDefault(x => x.StartsWith("IPAddress"))
                                    ?.Split('=')[1],
                            DevicePort = _clientPort.ToString(),
                            IsRegistered = "0",
                            DeviceType = "A",
                            InoutFlag = "I",
                            IsActive = "1",
                            IsConnected = "1",
                            DeviceGroupId = 1,
                            LastConnected = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            DeviceInfo = " ",
                            MacAddress = list[1].Substring("MAC=".Length),
                            DeviceUser = "0/0",
                            FaceUser = MaxFace + "/" + FaceCount,
                            CardUser = "0/0",
                            FingerUser = "0/0",
                        };
                        await _devService.SetDeviceInfo(device);
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        string msgd = "Device Added Successfully : " + SN;
                        Console.WriteLine(msgd);
                        await GlobalLogger.LogToFileAsync(msgd);


                    }
                    catch (Exception ex)
                    {
                        ReplyCode = "401 Unauthorized";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("[SendToClient Error] " + ex.Message);
                        throw;
                    }

                }



                // Only PUSH SDK Ver 2.0.1 (In Version 1.0 String for AttLog have diferent format, example: CHECK LOG: stamp=392232960 1       2018-03-14 17:39:00     0       0       0       1)
                //table=ATTLOG
                if (IndexOfEx(sBuffer, "Stamp", 1) > 0
                    && IndexOfEx(sBuffer, "OPERLOG", 1) < 0
                    && IndexOfEx(sBuffer, "ATTLOG", 1) > 0
                    && IndexOfEx(sBuffer, "OPLOG", 1) < 0) // Upload AttLog
                {
                    try
                    {
                        await AttLog(sBuffer, _devService);
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        string msga = "Log Added Successfully : " + sBuffer;
                        Console.WriteLine(msga);
                        await GlobalLogger.LogToFileAsync(msga);

                    }
                    catch (Exception ex)
                    {
                        ReplyCode = "401 Unauthorized";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("[SendToClient Error] " + ex.Message);
                        throw;
                    }

                }

                //table=OPERLOG
                if (IndexOfEx(sBuffer, "Stamp", 1) > 0
                    && IndexOfEx(sBuffer, "OPERLOG", 1) > 0
                    && IndexOfEx(sBuffer, "ATTLOG", 1) < 0
                    && IndexOfEx(sBuffer, "USERPIC", 1) < 0)
                {
                    if (IndexOfEx(sBuffer, "Expect: 100-continue") > 0
                        && IndexOfEx(sBuffer, "FP", 1) < 0
                        && IndexOfEx(sBuffer, "FACE", 1) < 0)
                    {
                        ReplyCode = "100 Continue";
                        strReply = "Continue";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        //Thread.Sleep(5000);
                        //remoteSocket.Close();
                        return;
                    }
                    try
                    {
                        await OperLog(sBuffer);
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("Log Added Successfully : " + sBuffer);
                    }
                    catch (Exception ex)
                    {

                        ReplyCode = "401 Unauthorized";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("[SendToClient Error] " + ex.Message);
                        throw;
                    }



                }
                //table=BIODATA
                if (IndexOfEx(sBuffer, "Stamp", 1) > 0
                    && IndexOfEx(sBuffer, "BIODATA", 1) > 0)
                {
                    try
                    {
                        await BioData(sBuffer);
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("Log Added Successfully : " + sBuffer);
                    }
                    catch (Exception ex)
                    {

                        ReplyCode = "401 Unauthorized";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Console.WriteLine("[SendToClient Error] " + ex.Message);
                        throw;
                    }
                }
                ////table=ERRORLOG
                //if (sBuffer.IndexOfEx("Stamp", 1) > 0
                //    && sBuffer.IndexOfEx("ERRORLOG", 1) > 0
                //    && sBuffer.IndexOfEx("ATTLOG", 1) < 0)
                //{
                //    Errorlog(sBuffer);
                //}

                ////table=ATTPHOTO
                if (IndexOfEx(sBuffer, "Stamp", 1) > 0
                    && IndexOfEx(sBuffer, "ATTPHOTO", 1) > 0)  /* upload attphoto */
                {
                    if (IndexOfEx(sBuffer, "Expect: 100-continue") > 0
                        && IndexOfEx(sBuffer, "PIN") < 0)
                    {
                        ReplyCode = "100 Continue";
                        strReply = "Continue";
                        SendDataToDevice(ReplyCode, strReply, tcp);
                        Thread.Sleep(5000);

                        return;
                    }
                    await AttPhoto(bReceive);
                    ReplyCode = "200 OK";
                    strReply = "OK";
                    SendDataToDevice(ReplyCode, strReply, tcp);
                }

                ////table=USERPIC
                //if (sBuffer.IndexOfEx("Stamp", 1) > 0 && sBuffer.IndexOfEx("USERPIC", 1) > 0) // Upload user Info
                //{
                //    UserPicLog(sBuffer);
                //}

                //options 推送配置信息
                //if (sBuffer.IndexOfEx("table=options", 1) > 0) // Upload options Info
                //{
                //    Options(sBuffer);
                //}

                /*
                //customer
                if (sBuffer.IndexOfEx("WORKCODE", 1) > 0) // Upload workcode Info
                {
                    WorkcodeLog(sBuffer);
                }*/

                SendDataToDevice(ReplyCode, strReply, tcp);


            }
        }


        private string GetValueByNameInPushHeader(string buffer, string Name)
        {
            string[] splitStr = buffer.Split('&', '?', ' ');
            if (splitStr.Length <= 0)
            {
                return null;
            }

            foreach (string tmpStr in splitStr)
            {
                if (IndexOfEx(tmpStr, Name + "=") >= 0)
                {
                    return tmpStr.Substring(IndexOfEx(tmpStr, Name + "=") + Name.Length + 1);
                }
            }
            return null;
        }

        private async Task<string> InitDeviceConnected(string DevSN, string RepString)
        {

            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

            DeviceMasterEntity machine = await _deviceservice.GetDeviceSn(DevSN);

            if (null == machine)
            {
                await GlobalLogger.LogToFileAsync("null");
                //OnNewMachine(DevSN);
                RepString = "OK\r\n";
                string newstring = RepString + "~200 OK";
                return newstring;

            }
            else
            {


                //device.LastConnected = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");
                machine.IsConnected = "1";


                await _deviceservice.HandleDeviceStatus(machine);
                //cmdstring = cmdstring + "\r\n";



                RepString = GetDeviceInitInfo(machine);
                string newstring = RepString + "~200 OK";
                return newstring;
            }
        }

        //private string GetDeviceInitInfo(DeviceMasterEntity device)
        //{
        //    StringBuilder retDeviceInfo = new StringBuilder();

        //    // First line must always contain SN
        //    retDeviceInfo.AppendFormat("GET OPTION FROM:{0}\n", device.DeviceSerialNumber);

        //    // --- Timestamps (set 0 to force re-upload or None if not applicable) ---
        //    retDeviceInfo.AppendFormat("ATTLOGStamp={0}\n", 0);
        //    retDeviceInfo.AppendFormat("OPERLOGStamp={0}\n", 0);
        //    retDeviceInfo.AppendFormat("ATTPHOTOStamp={0}\n", 0);
        //    // Optional: ERRORLOGStamp, BIODATAStamp if needed
        //    // retDeviceInfo.AppendFormat("ERRORLOGStamp={0}\n", 0);
        //    // retDeviceInfo.AppendFormat("BIODATAStamp={0}\n", 0);

        //    // --- Transmission flags (Format II is recommended) ---
        //    retDeviceInfo.AppendFormat("TransFlag={0}\n", "TransData    AttLog   OpLog   AttPhoto   EnrollUser   ChgUser   EnrollFP   ChgFP   UserPic   BioPhoto");

        //    // --- Timing controls ---
        //    retDeviceInfo.AppendFormat("ErrorDelay={0}\n", 130);   // Retry after error (30–300s recommended)
        //    retDeviceInfo.AppendFormat("Delay={0}\n", 10);         // Normal polling interval (2–60s)
        //    retDeviceInfo.AppendFormat("TimeZone={0}\n", 330);     // IST = GMT+5:30 → 330 minutes
        //    retDeviceInfo.AppendFormat("TransTimes={0}\n", "00:00;12:00"); // Example daily sync times
        //    retDeviceInfo.AppendFormat("TransInterval={0}\n", 1);          // Interval in minutes (0 = off)

        //    // --- Realtime flag ---
        //    retDeviceInfo.AppendFormat("Realtime={0}\n", 1); // 1 = send immediately, 0 = only scheduled

        //    // --- Encryption flag ---
        //    retDeviceInfo.AppendFormat("Encrypt={0}\n", 0); // 0 = no encryption, 1 = RC4 encrypt attendance logs

        //    // --- Server info ---
        //    retDeviceInfo.AppendFormat("ServerVer={0}\n", "2.2.14");
        //    retDeviceInfo.AppendFormat("PushProtVer={0}\n", "2.4.2"); // Match latest supported protocol
        //    retDeviceInfo.AppendFormat("PushOptionsFlag={0}\n", 0);
        //    retDeviceInfo.AppendFormat("ServerName={0}\n", "Logtime Server");

        //    // --- MultiBio support (set according to your device capabilities) ---
        //    // Format: bitwise colon-separated, e.g., "0:1:1:0:0:0:0:0:0:0"
        //    // Example below = supports fingerprint + face
        //    retDeviceInfo.AppendFormat("MultiBioDataSupport={0}\n", "1:1:0:0:0:0:0:0:0:0");
        //    retDeviceInfo.AppendFormat("MultiBioPhotoSupport={0}\n", "0:1:0:0:0:0:0:0:0:0");

        //    return retDeviceInfo.ToString();
        //}

        private string GetDeviceInitInfo(DeviceMasterEntity device)
        {
            StringBuilder retDeviceInfo = new StringBuilder();

            // First line
            retDeviceInfo.AppendFormat("GET OPTION FROM: {0}\n", device.DeviceSerialNumber);

            // Stamps shown in the screenshot
            retDeviceInfo.AppendLine("ATTLOGStamp=0");
            retDeviceInfo.AppendLine("OPERLOGStamp=0");
            retDeviceInfo.AppendLine("ATTPHOTOStamp=0");
            retDeviceInfo.AppendLine("BIODATAStamp=0");
            retDeviceInfo.AppendLine("IDCARDStamp=0");
            retDeviceInfo.AppendLine("ERRORLOGStamp=0");

            // Other parameters exactly as in screenshot
            retDeviceInfo.AppendLine("ErrorDelay=60");
            retDeviceInfo.AppendLine("Delay=30");
            retDeviceInfo.AppendLine("TransTimes=00: 00;14: 05");   // matching screenshot spacing
            retDeviceInfo.AppendLine("TransInterval=1");

            // TAB-based TransFlag EXACTLY as you requested
            retDeviceInfo.AppendLine(
                "TransFlag=TransData\tAttLog\tOpLog\tAttPhoto\tEnrollUser\tChgUser\tEnrollFP\tEnrollFACE\tChgFP\tFPImag\tFACE\tUserPic\tBioPhoto\tBioData"
            );
            retDeviceInfo.AppendLine("Realtime=1");
            retDeviceInfo.AppendLine("Encrypt=0");
            retDeviceInfo.AppendLine("ServerVer=2.2.14");
            retDeviceInfo.AppendLine("TimeZone=330");

            return retDeviceInfo.ToString();
        }



        private async Task AttLog(string sBuffer, IDeviceDataDownloadService voucher)

        {
            string machineSN = sBuffer.Substring(IndexOfEx(sBuffer, "SN=") + 3);
            string SN = machineSN.Split('&')[0];

            string machinestamp = sBuffer.Substring(IndexOfEx(sBuffer, "Stamp=") + 6);
            string Stamp = "";
            // this.GetTimeNumber(machinestamp, ref Stamp); // Get TimeStamp 
            //update machine attlogStamp
            // DeviceBll.UpdateAttLogStamp(Stamp, SN);

            int attindex = IndexOfEx(sBuffer, "\r\n\r\n", 1);
            string attstr = sBuffer.Substring(attindex + 4);
            await AttLogProcess(attstr, SN, voucher);
        }

        private async Task AttLogProcess(string attstr, string machineSN, IDeviceDataDownloadService voucher)
        {
            try
            {
                string[] strlist = attstr.Split('\n');
                foreach (string i in strlist)
                {
                    if (string.IsNullOrEmpty(i))
                        continue;
                    await CreateAttlog(i.ToString(), machineSN, voucher);
                }
            }
            catch (Exception ex)
            {
                if (OnError != null)
                {
                    Console.WriteLine("Error in saving " + ex.Message);
                }
            }
        }


        private async Task CreateAttlog(string attlog, string machineSN, IDeviceDataDownloadService voucher)
        {
            string[] attlogstr = attlog.Split('\t');


            PunchLogEntity record = new PunchLogEntity
            {
                DeviceSerialNumber = machineSN,
                DeviceEmployeeCode = attlogstr[0],
                PunchDateTime = DateTime.ParseExact(attlogstr[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CreatedDate = DateTime.Now.ToString(),
                //PunchInoutFlag = inOut.ToString(),
                IsManual = "0",
                IsDeleted = "0",

            };
            await voucher.SetAttendance(record);

        }

        private async Task OperLog(string sBuffer)
        {
            string machineSN = sBuffer.Substring(IndexOfEx(sBuffer, "SN=") + 3);
            string SN = machineSN.Split('&')[0]; ;
            if (string.IsNullOrEmpty(SN))
                return;

            //string machinestamp = sBuffer.Substring(sBuffer.IndexOfEx("Stamp=") + 6);
            //string Stamp = "";
            //this.GetTimeNumber(machinestamp, ref Stamp); // Get TimeStamp 

            ////update machine oplogStamp
            //DeviceBll.UpdateOperLogStamp(Stamp, SN);

            int operindex = IndexOfEx(sBuffer, "\r\n\r\n", 1);
            string operstr = sBuffer.Substring(operindex + 4);

            await SeparateOPERLOGData(operstr, SN);
        }

        private async Task SeparateOPERLOGData(string datastr, string SN)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var _service = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
               // var _masterDDLservice = scope.ServiceProvider.GetRequiredService<IMasterDDLService>();
                string[] strlist = datastr.Split('\n');
                foreach (string i in strlist)
                {
                    string tmpstr = i.ToString();

                    if (IndexOfEx(tmpstr, "OPLOG ") >= 0)//处理操作记录
                    {
                        // SaveOperLog(tmpstr, SN);
                    }
                    else if (tmpstr.Split(' ')[0] == "USER")//处理用户信息
                    {
                        await SetUserInfo(tmpstr, SN, _service);
                    }
                    else if (tmpstr.Split(' ')[0] == "FP")//处理用户指纹
                    {
                        // SaveFP(tmpstr, SN, false);
                    }
                    else if (tmpstr.Split(' ')[0] == "FACE")//处理用户面部模板
                    {
                        // SaveFace(tmpstr, false);
                    }
                    else if (tmpstr.Split(' ')[0] == "BIOPHOTO") //处理用户photoID
                    {
                        await SaveBioPhoto(tmpstr, SN, _service);
                    }
                }
            }
            catch (Exception ex)
            {
                if (OnError != null)
                {
                    if (OnError != null)
                    {
                        Console.WriteLine("Error in saving " + ex.Message);
                    }
                }
            }
        }


        public async Task SetUserInfo(string str, string sn, IDeviceDataDownloadService _service)
        {
            try
            {
                if (str != null)
                {
                    var newstr = str.Split("\t");
                    Dictionary<string, string> EmployeeData = new Dictionary<string, string>();
                    for (int i = 0; i < newstr.Length; i++)
                    {
                        string part = newstr[i];
                        int idx = part.IndexOf("=");
                        if (idx <= 0)
                            continue;
                        string Key = part.Substring(0, idx);
                        if (EmployeeData.ContainsKey(Key) || string.IsNullOrEmpty(Key))
                            continue;
                        EmployeeData.Add(Key, string.IsNullOrEmpty(part.Substring(idx + 1)) ? " " : part.Substring(idx + 1));
                    }

                    var enroll_no = EmployeeData["USER PIN"];


                    var entity = new EmployeeEntity
                    {
                        //   CompanyId = compId,
                        EmployeeCode = enroll_no,
                        DeviceEmployeeCode = enroll_no,
                        EmployeeName = EmployeeData["Name"],
                        JoiningDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        //   MappedDeviceGroup = devicegroup.ToString(),

                        DevSerialNo = sn
                    };

                    await _service.SetUserInfo(entity);


                    string msg = $"Employee add Successfully{enroll_no}";
                    Console.WriteLine(msg);
                    await GlobalLogger.LogToFileAsync(msg);


                }
            }
            catch (JsonReaderException ex)
            {
                string msg = ex + "Fail: Error occured while adding Employee";
                Console.WriteLine(msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
            catch (Exception ex)
            {
                string msge = ex + "Fail";
                Console.WriteLine(msge);
                await GlobalLogger.LogToFileAsync(msge);
            }
        }

        private async Task BioData(string sBuffer)
        {
            string machineSN = sBuffer.Substring(IndexOfEx(sBuffer, "SN=") + 3);
            string SN = machineSN.Split('&')[0];

            //string machinestamp = sBuffer.Substring(sBuffer.IndexOfEx("Stamp=") + 6);
            //string Stamp = "";
            //this.GetTimeNumber(machinestamp, ref Stamp); // Get TimeStamp 

            //update machine BioDataStamp (不使用)

            int bioindex = IndexOfEx(sBuffer, "\r\n\r\n", 1);
            string biostr = sBuffer.Substring(bioindex + 4);

            await SeparateBioData(biostr, SN);
        }

        private async Task SeparateBioData(string datastr, string SN)
        {
            try
            {
                string[] strlist = datastr.Split('\n');
                using var scope = _serviceProvider.CreateScope();
                //var _employeeservice = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
                //var _masterDDLservice = scope.ServiceProvider.GetRequiredService<IMasterDDLService>();
                foreach (string i in strlist)
                {
                    string tmpstr = i.ToString();
                    if (string.IsNullOrEmpty(tmpstr))
                        continue;
                    string bioTypeStr = tmpstr.Split('\t')[5].Split('=')[1];
                    // BioType bioType = (BioType)Enum.Parse(typeof(BioType), bioTypeStr);
                    switch (bioTypeStr)
                    {
                        case "0"://Comm
                            break;
                        case "1"://FingerPrint
                            //SaveFP(tmpstr, SN, true);
                            break;
                        case "2"://Face
                                 // SaveFace(tmpstr, true);
                            break;
                        case "3"://VocalPrint
                            break;
                        case "4"://Iris
                            break;
                        case "5"://Retina
                            break;
                        case "6"://Palm Print
                            break;
                        case "7"://Finger Vein
                            break;
                        case "8"://Palm
                                 // SavePalm(tmpstr);
                            break;
                        case "9"://VisilightFace
                            //await SaveVisilightFace(tmpstr, SN, _employeeservice, _masterDDLservice);
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                if (OnError != null)
                {
                    _logger.LogError(ex, "Fail");
                }
            }
        }

        //private async Task SaveVisilightFace(string str, string SN, IEmployeeService employeeService, IMasterDDLService masterDDLService)
        //{
        //    try
        //    {
        //        string[] newstr = str.Split("\t");
        //        string base64Image = newstr[9].Split('=')[1];
        //        string enroll_no = newstr[0].Split('=')[1];
        //        string rootPath = Directory.GetCurrentDirectory();
        //        string parentPath = Directory.GetParent(rootPath)!.FullName; // goes up one level
        //        string documentsPath = Path.Combine(parentPath, "Documents");
        //        string dbImagePath;
        //        var compId = await masterDDLService.GetCompanyIdbyDeviceSerialNumber(SN);
        //        var empId = await masterDDLService.GetEmployeeIdByCardNumber(enroll_no);
        //        var entity = await employeeService.GetEmployeeById(empId);
        //        if (base64Image != "0")
        //        {
        //            if (base64Image.StartsWith("record"))
        //            {
        //                var commaIndex = base64Image.IndexOf(',');
        //                base64Image = base64Image[(commaIndex + 1)..];
        //            }
        //            var newbase64img = "/9j/4AAQSkZJRgABAQAAAQAB/" + base64Image;
        //            byte[] imageBytes = Convert.FromBase64String(newbase64img);

        //            // Step 2: Create the directory path
        //            string baseFolder = Path.Combine(compId, "DeviceFace");
        //            string directoryPath = Path.Combine(documentsPath, baseFolder);
        //            Directory.CreateDirectory(directoryPath);
        //            string fileName = $"{entity.EmployeeCode}.jpeg";
        //            string fullImagePath = Path.Combine(directoryPath, fileName);

        //            // Step 4: Save image
        //            await File.WriteAllBytesAsync(fullImagePath, imageBytes);
        //            dbImagePath = Path.Combine(baseFolder, fileName).Replace("\\", "/");
        //            entity.FaceImagePath = dbImagePath;
        //            await employeeService.EditEmployee(entity, 0);
        //            _logger.LogInformation($"Face added Successfully {enroll_no}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Fail");

        //    }




        //}



        private async Task HandleGetRequest(TcpClient client, string request)
        {
            // Extract device serial number (SN=....)
            string sn = GetValueByNameInPushHeader(request, "SN");

            // Build your command (auto-send when device connects)
            string command = $"SET OPTION SystemTime={DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            string body = $"C:{sn}:001:{command}\r\n";

            string headers =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/plain\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            string response = headers + body;

            byte[] buffer = Encoding.ASCII.GetBytes(response);
            NetworkStream stream = client.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            SendDataToDevice("200 OK", "OK", client);

            //client.Close();
        }

        private async Task SaveBioPhoto(string bioPhoto, string Sn, IDeviceDataDownloadService _service)
        {
            try
            {
                if (IndexOfEx(bioPhoto, "PIN", 0, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var newstr = bioPhoto.Split("\t");
                    Dictionary<string, string> EmployeeData = new Dictionary<string, string>();
                    for (int i = 0; i < newstr.Length; i++)
                    {
                        string part = newstr[i];
                        int idx = part.IndexOf("=");
                        if (idx <= 0)
                            continue;
                        string Key = part.Substring(0, idx);
                        if (EmployeeData.ContainsKey(Key) || string.IsNullOrEmpty(Key))
                            continue;
                        EmployeeData.Add(Key, string.IsNullOrEmpty(part.Substring(idx + 1)) ? " " : part.Substring(idx + 1));
                    }
                    var deviceId = EmployeeData["BIOPHOTO PIN"];
                    var entity = new EmployeeEntity
                    {
                        DeviceEmployeeCode = deviceId,
                        EmpBase64Img = EmployeeData["Content"],
                        DevSerialNo = Sn,

                    };
                    await _service.SetUserInfo(entity);
                    string msg = $"Face added Successfully {deviceId}";
                    Console.WriteLine(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                }
                else
                {
                    string msge = $"Employee not present ";
                    Console.WriteLine(msge);
                    await GlobalLogger.LogToFileAsync(msge);
                }
            }

            catch (Exception)
            {

                throw;
            }

        }

        private async Task<bool> CheckPendingCmd(string strReceive, TcpClient client)
        {
            try
            {
                string SN = GetValueByNameInPushHeader(strReceive, "SN");
                using var scope = _serviceProvider.CreateScope();
                var _deviceDataService = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
                DeviceCommandEntity Command = new DeviceCommandEntity() { DeviceSerialNumber = SN };

                var commands = await _deviceDataService.CheckCommandZkt(SN.ToString());

                if (commands.Count > 0)
                {
                    var deviceLock = _deviceLocks.GetOrAdd(SN, _ => new SemaphoreSlim(1, 1));

                    await deviceLock.WaitAsync();
                    try
                    {
                        foreach (var command in commands)
                        {
                            var cmd = await _deviceDataService.GetDeviceCommand(command.DeviceCommandId);

                            // send ONE command
                            await HandleBitch(cmd, client);

                            // WAIT until device responds (devicecmd?)
                            //await WaitForCommandResultAsync(command.DeviceCommandId);
                        }
                    }
                    finally
                    {
                        deviceLock.Release();
                    }


                    return true;
                }
                else
                {
                    string msg = $"[ZKT] No Pending command exist";
                    Console.WriteLine(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    await GetRequestProcess(strReceive, client);
                    return false;
                }
            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync("<<<<<<<<<<Error>>>>>>>>>" + ex);
                throw ex;
            }

        }

        private string GetAddUserCommand(long cmdId, string pin, string name,
                                     int privilege = 0, string password = "", string card = "",
                                     int group = 1, string tz = "0000000000000000", int verify = -1)
        {
            // USER record format: USER PIN=xxx Name=xxx Pri=xxx Card=xxx
            return $"C:{cmdId}:DATA UPDATE USERINFO " + $"PIN={pin}\tName={name}\tPri={privilege}\tPasswd={password}\t" + $"Card={card}\tGrp={group}\tTZ={tz}\tVerify={verify}"; ;
        }

        private string GetDownloadUserCommand(long cmdId, string pin)
        {
            // USER record format: USER PIN=xxx Name=xxx Pri=xxx Card=xxx
            return $"C:{cmdId}:DATA QUERY USERINFO " + $"PIN={pin}"; ;
        }
        private string GetDeleteUserCommand(string pin)
        {
            // Delete user by PIN
            return $"DATA DELETE USER PIN={pin}\n";
        }
        private string GetAddBioPhotoCommand(
     long cmdId,
     string pin,
     int type,
     int no,
     int index,
     int size,
     string content,
     string format,
     string url = "",
     int postBackTmpFlag = 0)
        {
            // BIOPHOTO record format:
            // C:${CmdID}:DATA UPDATE BIOPHOTO PIN=${XXX} Type=${XXX} No=${XXX} Index=${XXX} Size=${XXX} Content=${XXX} Format=${XXX} Url=${XXX} PostBackTmpFlag=${XXX}

            return $"C:{cmdId}:DATA UPDATE BIOPHOTO " +
                   $"PIN={pin}\t" +
                   $"Type={type}\t" +
                   $"No={no}\t" +
                   $"Index={index}\t" +
                   $"Size={size}\t" +
                   $"Content={content}\t" +
                   $"Format={format}\t" +
                   $"Url={url}\t" +
                   $"PostBackTmpFlag={postBackTmpFlag}";
        }




        private string GetRestartCommand(long cmd)
        {
            return $"C:{cmd}:REBOOT\n";
        }
        private string GetAllLogsCommand(long cmd, string st, string et)
        {
            // Equivalent to telling the device to re-upload from start
            return $"C:{cmd}:DATA QUERY ATTLOG StartTime={st} EndTime={et}\n";
        }
        private string GetAllUsersCommand(long cmd)
        {
            // Query all users
            return $"C:{cmd}:DATA QUERY USERINFO\n";
        }
        private string DeleteAllUsersCommand(long cmd)
        {
            // Delete all users
            return $"C:{cmd}:CLEAR DATA\n";
        }

        // Example usage inside your TCP request handler
        private async Task HandleBitch(DeviceCommandEntity deviceCommand, TcpClient tcp)
        {
            try
            {
                await GlobalLogger.LogToFileAsync("Handle Request ENTERED");
                string cmd;


                await GlobalLogger.LogToFileAsync("Incoming Command: " + deviceCommand.DeviceCommand);
                switch (deviceCommand.DeviceCommand)
                {
                    case "ADD_USER":
                        // case "UPDATE_USER":

                        cmd = GetAddUserCommand(deviceCommand.DeviceCommandId,
                             deviceCommand.DeviceUserId, deviceCommand.EmployeeName
                            );

                        break;

                    case "DELETE_USER":

                        cmd = GetDeleteUserCommand(deviceCommand.DeviceUserId);
                        break;

                    case "UPDATE_USER":
                        try
                        {
                            string base64String = " ";
                            if (!string.IsNullOrEmpty(deviceCommand.FaceImagePath))
                            {
                                string rootPath = Directory.GetCurrentDirectory();
                                string basePath = Directory.GetParent(rootPath)!.FullName;
                                var imagePath = System.IO.Path.Combine(basePath, "Documents", deviceCommand.FaceImagePath);
                                //Converting Image to JPEG
                                using var image = SixLabors.ImageSharp.Image.Load(imagePath);
                                using var ms = new MemoryStream();

                                image.SaveAsJpeg(ms);
                                base64String = Convert.ToBase64String(ms.ToArray());

                            }

                            cmd = GetAddBioPhotoCommand(deviceCommand.DeviceCommandId,
                                 deviceCommand.DeviceUserId, 9, 0, 0, base64String.Length, base64String, "JPEG"
                                );
                            break;
                        }
                        catch (Exception ex)
                        {

                            throw ex;
                        }


                    case "RESTART_DEVICE":
                        cmd = GetRestartCommand(deviceCommand.DeviceCommandId);
                        break;

                    case "DOWNLOAD_USER":
                        cmd = GetDownloadUserCommand(deviceCommand.DeviceCommandId, deviceCommand.DeviceUserId);
                        await GlobalLogger.LogToFileAsync("Download User +++");
                        break;

                    case "DEVICE_LOG":
                        cmd = GetAllLogsCommand(deviceCommand.DeviceCommandId, deviceCommand.FromDate, deviceCommand.ToDate);
                        await GlobalLogger.LogToFileAsync("Device log");
                        break;

                    case "GET_USER":
                        cmd = GetAllUsersCommand(deviceCommand.DeviceCommandId);
                        await GlobalLogger.LogToFileAsync("Get User");
                        break;

                    case "DELETE_ALL_USER":
                        cmd = DeleteAllUsersCommand(deviceCommand.DeviceCommandId);
                        await GlobalLogger.LogToFileAsync("Delete All Users");
                        break;

                    default:
                        string msg = $"[ZKT] Unknown command type: {deviceCommand.DeviceCommand}";
                        Console.WriteLine(msg);
                        await GlobalLogger.LogToFileAsync(msg);
                        return;
                }

                if (!string.IsNullOrEmpty(cmd))
                {
                    await GlobalLogger.LogToFileAsync("CMD SENDING:" + cmd);
                    //var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    //_cmdWaiters[deviceCommand.DeviceCommandId] = tcs;
                    //SendCmdToDevice(deviceCommand.DeviceSerialNumber, cmd, tcp);
                    await SendHttpResponseToDevice(tcp, cmd);
                    //try
                    //{
                    //    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                    //    await tcs.Task.WaitAsync(cts.Token); // waits ONLY for real response
                    //}
                    //catch (OperationCanceledException)
                    //{
                    //    await UpdateCommand(deviceCommand.DeviceCommandId, -2);
                    //}
                    //finally
                    //{
                    //    _cmdWaiters.TryRemove(deviceCommand.DeviceCommandId, out _);
                    //}


                    //deviceCommand.CommondStatusId = 2; // Mark as sent
                    //deviceCommand.UpdatedDate = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");
                    //deviceCommand.CommondResponce = "Pending";
                    //easyAccess.Update(deviceCommand);
                    //await easyAccess.SaveChangesAsync();
                    //await GlobalLogger.LogToFileAsync("CMD PENDING:" + cmd);


                    return;
                }
            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync("<<<<<<<<<<Error>>>>>>>>>" + ex);
            }

        }
       

        public async Task UpdateCommand(long id, int result)
        {
            using var scope = _serviceProvider.CreateScope();
            var easyAccess = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            DeviceCommandEntity deviceCommand = new DeviceCommandEntity { DeviceCommandId = id };
            if (result == 0)
            {
                deviceCommand.CommondStatusId = 1; // Mark as sent
                deviceCommand.CommondResponce = "Successful";
            }
            else if (result == -2)
            {
                deviceCommand.CommondStatusId = -1; // Mark as sent
                deviceCommand.CommondResponce = "Timeout";
            }
            else
            {
                deviceCommand.CommondStatusId = -1; // Mark as sent
                deviceCommand.CommondResponce = "Error";
            }
            deviceCommand.UpdatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

           await easyAccess.UpdateCommandAsync(deviceCommand);
           
        }

        private void SendCmdToDevice(string serialNumber, string dataRecord, TcpClient tcp)
        {
            try
            {
                // Convert body to bytes (ZKTeco expects GB2312 encoding)
                byte[] bData = Encoding.GetEncoding("gb2312").GetBytes(dataRecord);

                // Build HTTP request header
                string sHeader = $"POST /iclock/cdata?SN={serialNumber}&table=OPERLOG HTTP/1.1\r\n";
                sHeader += $"Host: {_clientIp}:{_clientPort}\r\n";
                sHeader += "Content-Type: text/plain\r\n";
                sHeader += "User-Agent: iClock Proxy/1.09\r\n";
                sHeader += "Connection: close\r\n";
                sHeader += $"Content-Length: {bData.Length}\r\n\r\n";

                // Get stream for writing
                NetworkStream stream = tcp.GetStream();

                // Send header
                byte[] headerBytes = Encoding.GetEncoding("gb2312").GetBytes(sHeader);
                SendToClient(stream, headerBytes);

                // Send body (actual DATA RECORD)
                SendToClient(stream, bData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SendDataToDevice Error] " + ex.Message);
            }
        }

        private async Task SendHttpResponseToDevice(TcpClient tcp, string command)
        {
            try
            {
                NetworkStream stream = tcp.GetStream();

                // ZKTeco expects GB2312
                byte[] body = Encoding.GetEncoding("gb2312").GetBytes(command);

                string header =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/plain\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n" +
                    "\r\n";

                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                // Send HTTP response header
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                // Send command
                await stream.WriteAsync(body, 0, body.Length);

                await stream.FlushAsync();

                await GlobalLogger.LogToFileAsync("[HTTP RESPONSE]");
                await GlobalLogger.LogToFileAsync(header);
                await GlobalLogger.LogToFileAsync(command);
            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync("SendHttpResponseToDevice Error: " + ex);
            }
        }
        private async Task AttPhoto(byte[] bReceive)
        {
            try
            {
                string strReceive = Encoding.ASCII.GetString(bReceive);
                byte[] imgReceive = new byte[bReceive.Length];
                string[] tmpstr = strReceive.Split('\n');
                string strImageNumber = "";
                foreach (string str in tmpstr)
                {
                    if (IndexOfEx(str, "PIN=") >= 0)
                    {
                        strImageNumber = str;
                        break;
                    }
                }
                string machineSN = strReceive.Substring(IndexOfEx(strReceive, "SN=") + 3);
                string SN = machineSN.Split('&')[0];
                string pin = strImageNumber.Substring(IndexOfEx(strImageNumber, "PIN=") + 4);


                string machinestamp = strReceive.Substring(IndexOfEx(strReceive, "Stamp=") + 6);
                string Stamp = "";
                //this.GetTimeNumber(machinestamp, ref Stamp); // Get TimeStamp 

                //update machine AttPhotoStamp
                //DeviceBll.UpdateAttPhotoStamp(Stamp, SN);

                int imgrecindex = IndexOfEx(strReceive, "uploadphoto") + 12;
                Array.Copy(bReceive, imgrecindex, imgReceive, 0, bReceive.Length - imgrecindex);
                string rootPath = Directory.GetCurrentDirectory();
                string parentPath = Directory.GetParent(rootPath)!.FullName;
                string documentsPath = System.IO.Path.Combine(parentPath, "Documents");

                string baseFolder = System.IO.Path.Combine("logImage", SN);
                string directoryPath = System.IO.Path.Combine(documentsPath, baseFolder);
                Directory.CreateDirectory(directoryPath);

                string fileName = $"{pin}";
                string fullImagePath = System.IO.Path.Combine(directoryPath, fileName);

                await File.WriteAllBytesAsync(fullImagePath, imgReceive);
                string dbImagePath = System.IO.Path.Combine(baseFolder, fileName).Replace("\\", "/");

            }
            catch (Exception ex)
            {

                throw ex;
            }

            //string msg = $"Image saved for SN {deviceSerialNumber}, Path={dbImagePath}";

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
    }
}
