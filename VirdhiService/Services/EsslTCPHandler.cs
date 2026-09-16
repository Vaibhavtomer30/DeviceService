using DeviceServices.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Services
{
    public class EsslTCPHandler
    {
        private ILogger<EsslTCPHandler> _logger;
        private IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        string msg = "No Message";
        private string _clientIp;
        private int _clientPort;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks
     = new();

        public EsslTCPHandler(ILogger<EsslTCPHandler> Logger, IServiceProvider service, IConfiguration configuration)
        {
            _logger = Logger;
            _serviceProvider = service;
            _configuration = configuration;
        }

        public static void Start(EsslTCPHandler handlerInstance, byte[] bReceive, TcpClient client)
        {
            handlerInstance.HandleTcpDeviceDataAsync(bReceive, client).GetAwaiter().GetResult();
        }
        public async Task HandleTcpDeviceDataAsync(byte[] bReceive, TcpClient client)
        {
            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();


            // disconnected
            //string hexDump = BitConverter.ToString(buffer, 0, bytesRead);
            //_logger.LogInformation($"[ZKT] Raw Hex: {hexDump}");
            string strReceive = Encoding.ASCII.GetString(bReceive).TrimEnd().TrimEnd('\0');


            // await UpdateDeviceStatus(strReceive, _deviceservice);
            await GlobalLogger.LogToFileAsync("USING");
            using (client)
            {
                await GlobalLogger.LogToFileAsync("using client");
                if (IndexOfEx(strReceive, "cdata.aspx") > 0 || IndexOfEx(strReceive, "cdata.aspx?") > 0)
                {
                    // cdataProcess(bytesRead,client);
                    //string response = "OK\n";
                    //byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    //await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await cdataProcess(bReceive, client, _deviceservice);
                }
                else if (IndexOfEx(strReceive, "getrequest.aspx?") > 0)
                {
                    await CheckPendingCmd(strReceive, client);


                }
                else if (IndexOfEx(strReceive, "devicecmd.aspx?") > 0)
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
                //UpdateLastSeen(SN);

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
            //UpdateLastSeen(SN);


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
                            LastConnected = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"),
                            CreatedDate = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss"),
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
                        //await BioData(sBuffer);
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

                Console.WriteLine("Error in saving " + ex.Message);

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

                Console.WriteLine("Error in saving " + ex.Message);

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
                    SendCmdToDevice(deviceCommand.DeviceSerialNumber, cmd, tcp);
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
        //private async Task HandleDeviceRequest(DeviceCommandEntity deviceCommand, TcpClient tcp)
        //{
        //    string cmd;
        //    using var scope = _serviceProvider.CreateScope();
        //    var easyAccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();

        //    await GlobalLogger.LogToFileAsync("Handle Request ENTERED");
        //    await GlobalLogger.LogToFileAsync("Incoming Command: " + deviceCommand.DeviceCommand);
        //    switch (deviceCommand.DeviceCommand)
        //    {
        //        case "ADD_USER":
        //            // case "UPDATE_USER":

        //            cmd = GetAddUserCommand(deviceCommand.DeviceCommandId,
        //                 deviceCommand.DeviceUserId, deviceCommand.EmployeeName
        //                );

        //            break;

        //        case "DELETE_USER":

        //            cmd = GetDeleteUserCommand(deviceCommand.DeviceUserId);
        //            break;

        //        case "UPDATE_USER":
        //            try
        //            {
        //                string base64String = " ";
        //                if (!string.IsNullOrEmpty(deviceCommand.FaceImagePath))
        //                {
        //                    string rootPath = Directory.GetCurrentDirectory();
        //                    string basePath = Directory.GetParent(rootPath)!.FullName;
        //                    var imagePath = System.IO.Path.Combine(basePath, "Documents", deviceCommand.FaceImagePath);

        //                    using (var image = System.Drawing.Image.FromFile(imagePath))
        //                    using (var ms = new MemoryStream())
        //                    {
        //                        // Always convert to JPEG format
        //                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

        //                        // Convert to Base64 string
        //                        byte[] jpgBytes = ms.ToArray();
        //                        base64String = Convert.ToBase64String(jpgBytes);
        //                    }
        //                }

        //                cmd = GetAddBioPhotoCommand(deviceCommand.DeviceCommandId,
        //                     deviceCommand.DeviceUserId, 9, 0, 0, base64String.Length, base64String, "JPEG"
        //                    );
        //                break;
        //            }
        //            catch (Exception ex)
        //            {

        //                throw ex;
        //            }


        //        case "RESTART_DEVICE":
        //            cmd = GetRestartCommand(deviceCommand.DeviceCommandId);
        //            break;

        //        case "DOWNLOAD_USER":
        //            cmd = GetDownloadUserCommand(deviceCommand.DeviceCommandId, deviceCommand.DeviceUserId);
        //            await GlobalLogger.LogToFileAsync("Download User +++");
        //            break;

        //        case "DEVICE_LOG":
        //            cmd = GetAllLogsCommand(deviceCommand.DeviceCommandId, deviceCommand.FromDate, deviceCommand.ToDate);
        //            await GlobalLogger.LogToFileAsync("Device log");
        //            break;

        //        case "GET_USER":
        //            cmd = GetAllUsersCommand(deviceCommand.DeviceCommandId);
        //            await GlobalLogger.LogToFileAsync("Get User");
        //            break;

        //        default:
        //            string msg = $"[ZKT] Unknown command type: {deviceCommand.DeviceCommand}";
        //            Console.WriteLine(msg);
        //            await GlobalLogger.LogToFileAsync(msg);
        //            return;
        //    }

        //    if (!string.IsNullOrEmpty(cmd))
        //    {
        //        await GlobalLogger.LogToFileAsync("CMD SENDING:" + cmd);
        //        SendCmdToDevice(deviceCommand.DeviceSerialNumber, cmd, tcp);
        //        deviceCommand.CommondStatusId = 2; // Mark as sent
        //        deviceCommand.UpdatedDate = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");
        //        deviceCommand.CommondResponce = "Pending";
        //        easyAccess.Update(deviceCommand);
        //        await easyAccess.SaveChangesAsync();
        //        await GlobalLogger.LogToFileAsync("CMD PENDING:" + cmd);


        //        return;
        //    }


        //}

        public async Task UpdateCommand(long id, int result)
        {
            using var scope = _serviceProvider.CreateScope();
            var easyAccess = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

            DeviceCommandEntity deviceCommand = await easyAccess.GetDeviceCommand(id);
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
            deviceCommand.UpdatedDate = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");

            easyAccess.UpdateCommandAsync(deviceCommand);
        }

        private void SendCmdToDevice(string serialNumber, string dataRecord, TcpClient tcp)
        {
            try
            {
                // Convert body to bytes (ZKTeco expects GB2312 encoding)
                byte[] bData = Encoding.GetEncoding("gb2312").GetBytes(dataRecord);

                // Build HTTP request header
                string sHeader = $"POST /iclock/cdata.aspx?SN={serialNumber}&table=OPERLOG HTTP/1.1\r\n";
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
    }
    }
