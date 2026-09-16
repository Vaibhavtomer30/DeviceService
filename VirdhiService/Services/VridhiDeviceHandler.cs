using DeviceServices.Models;
using DeviceServices.Services;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using UCSAPICOMLib;

namespace DeviceServices.Services

{
    public class VridhiDeviceHandler
    {
        public UCSAPICOMLib.UCSAPI ucsAPI;
        private ILogger<VridhiDeviceHandler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private IServerAuthentication serverAuthentication;
        private IServerUserData serveruserData;
        private IAccessLogData accessLogData;
        private ITerminalUserData terminalUserData;
        public string terminalId;
        public int if_enrollcmd = 0;
        public WALKTHROUGH_DATA WalkThrough;

        public VridhiDeviceHandler(ILogger<VridhiDeviceHandler> zktLogger, IServiceProvider service, IConfiguration configuration)
        {
            _logger = zktLogger;
            _serviceProvider = service;
            _configuration = configuration;
            try
            {
                ucsAPI = new UCSAPIClass();
                GlobalLogger.LogToFileAsync("COM object initialization Succesfully");
            }
            catch (COMException ex)
            {
                GlobalLogger.LogToFileAsync("COM object initialization failed: {Message}", ex.Message);
                throw;
            }
            serveruserData = ucsAPI.ServerUserData as IServerUserData;
            terminalUserData = ucsAPI.TerminalUserData as ITerminalUserData;
            accessLogData = ucsAPI.AccessLogData as IAccessLogData;
            serverAuthentication = ucsAPI.ServerAuthentication as IServerAuthentication;
            //terminalOption = ucsAPI.TerminalOption as ITerminalOption;
            //smartCardLayout = ucsAPI.SmartCardLayout as ISmartCardLayout;
            //accessControlData = ucsAPI.AccessControlData as IAccessControlData;

            ucsAPI.EventTerminalConnected += new _DIUCSAPIEvents_EventTerminalConnectedEventHandler(UCSCOMObj_EventTerminalConnected);
            ucsAPI.EventTerminalDisconnected += new _DIUCSAPIEvents_EventTerminalDisconnectedEventHandler(UCSCOMObj_EventTerminalDisconnected);
            ucsAPI.EventFirmwareVersion += new _DIUCSAPIEvents_EventFirmwareVersionEventHandler(ucsAPI_EventFirmwareVersion);
            ucsAPI.EventAntipassback += new _DIUCSAPIEvents_EventAntipassbackEventHandler(ucsAPI_EventAntipassback);
            ucsAPI.EventAddUser += new _DIUCSAPIEvents_EventAddUserEventHandler(ucsAPI_EventAddUser);
            ucsAPI.EventRealTimeAccessLog += new _DIUCSAPIEvents_EventRealTimeAccessLogEventHandler(ucsAPI_EventRealTimeAccessLog);
            ucsAPI.EventDeleteAllUser += new _DIUCSAPIEvents_EventDeleteAllUserEventHandler(ucsAPI_EventDeleteAllUser);
            ucsAPI.EventDeleteUser += new _DIUCSAPIEvents_EventDeleteUserEventHandler(ucsAPI_EventDeleteUser);
            ucsAPI.EventGetAccessLog += new _DIUCSAPIEvents_EventGetAccessLogEventHandler(ucsAPI_EventGetAccessLog);
            ucsAPI.EventGetAccessLogCount += new _DIUCSAPIEvents_EventGetAccessLogCountEventHandler(ucsAPI_EventGetAccessLogCount);
            ucsAPI.EventGetUserCount += new _DIUCSAPIEvents_EventGetUserCountEventHandler(ucsAPI_EventGetUserCount);
            //ucsAPI.EventGetUserData += new _DIUCSAPIEvents_EventGetUserDataEventHandler(ucsAPI_EventGetUserData);
            ucsAPI.EventGetUserInfoList += new _DIUCSAPIEvents_EventGetUserInfoListEventHandler(ucsAPI_EventGetUserInfoList);
            ucsAPI.EventGetUserData += new _DIUCSAPIEvents_EventGetUserDataEventHandler(ucsAPI_EventGetUserData);
            ucsAPI.EventWalkThroughData += new _DIUCSAPIEvents_EventWalkThroughDataEventHandler(ucsAPI_EventWalkThroughData);


        }

        public VridhiDeviceHandler()
        {
        }


        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    await ExecuteTaskAsync(stoppingToken).ConfigureAwait(false);
        //}
        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var msg = "Terminal trying to connect at " + DateTime.Now;
            _logger.LogInformation(msg);
            GlobalLogger.LogToFileAsync(msg);
            ucsAPI.ServerStart(999, 8179);
            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //ucsAPI.ServerStart(_deviceServerConfig.MaxAllowClient, _deviceServerConfig.Port);
                    ucsAPI.ServerStop();
                    msg = "Device Server Started at {Time}" + DateTime.Now;
                    _logger.LogInformation(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    await Task.Delay(Convert.ToInt32(TimeSpan.FromSeconds(5).TotalMilliseconds), stoppingToken);
                    ucsAPI.ServerStart(999, 8179);
                }
                catch (Exception ex)
                {
                    ucsAPI.ServerStop();
                    msg = "Device Server listening; Err=0x{Error}" + ucsAPI.ErrorCode.ToString("X4");
                    _logger.LogInformation(msg);
                    await GlobalLogger.LogToFileAsync(msg);
                    await Task.Delay(Convert.ToInt32(TimeSpan.FromSeconds(5).TotalMilliseconds), stoppingToken);
                    ucsAPI.ServerStart(999, 8179);
                }
            }
        }

        async void UCSCOMObj_EventTerminalConnected(int TerminalID, string TerminalIP)
        {
            await GlobalLogger.LogToFileAsync("<--EventTerminalConnected");
            await GlobalLogger.LogToFileAsync("   +TerminalID: {TerminalID}" + TerminalID);
            await GlobalLogger.LogToFileAsync("   +TerminalIP: {TerminalIP}" + TerminalIP);
            await GlobalLogger.LogToFileAsync("   +ErrorCode: {ErrorCode}" + this.ucsAPI.EventError);
            var info = new UCSAPINative.UCSAPI_TERMINAL_INFO();
            UCSAPINative.UCSAPI_GetTerminalInfo((uint)TerminalID, out info);
            await UpdateDevStatus(TerminalID, TerminalIP, info);
            this.terminalId = TerminalID.ToString();
            // txtTerminalID.Text = this.terminalID;


            /*
            accessControlData.InitData();

            accessControlData.SetTimeZone("0001", 0, 0, 0, 23, 59);
            accessControlData.SetAuthProperty("0001", 1, 1, 0, 0, 1, 0, 0, 0);
            accessControlData.SetTimeZoneToAuthProperty("0001", 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            //accessControlData.SetHoliday("1000", 0, 1, 1);
            accessControlData.SetAccessTime("2000", "0001", "0001", "0001", "0001", "0001", "0001", "0001", "0001", "");
            accessControlData.SetAccessGroup("3000", 0, "2000");

            accessControlData.SetAccessControlDataToTerminal(0, TerminalID, 0);
            //accessControlData.SetAccessControlDataToTerminal(0, TerminalID, 1);
            accessControlData.SetAccessControlDataToTerminal(0, TerminalID, 2);
            accessControlData.SetAccessControlDataToTerminal(0, TerminalID, 3);
            */
        }

        async void UCSCOMObj_EventTerminalDisconnected(int TerminalID)
        {
            await GlobalLogger.LogToFileAsync(
                "<--Complete TerminalDisconnected\n   +TerminalID: {TerminalID}\n   +ErrorCode: {ErrorCode}" +
                TerminalID + "-" +
                this.ucsAPI.EventError
            );
        }

        async void ucsAPI_EventFirmwareVersion(int ClientID, int TerminalID, string Version)
        {
            await GlobalLogger.LogToFileAsync("<--EventFirmwareVersion\n" +
                       "   +ErrorCode: {ErrorCode}\n" +
                       "   +ClientID: {ClientID}\n" +
                       "   +TerminalID: {TerminalID}\n" +
                       "   +Version: {Version}" +
                       this.ucsAPI.EventError +
                       ClientID +
                       TerminalID +
                       Version);
            //string devName = GetDeviceName(Version);
            //await UpdateDevStatus(ClientID, devName, Version);


        }


        //public static string GetDeviceName(string raw)
        //    {
        //        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        //        // Trim outer slashes/spaces first
        //        var s = raw.Trim().Trim('/');

        //        // Prefer: "<name> <version...>"
        //        var m = Regex.Match(s, @"^(.*?)\s+\d+(?:\.\d+)*(?:[^\s]*)?(?:.*)$");
        //        if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
        //            return m.Groups[1].Value.Trim();

        //        // Fallbacks for odd formats
        //        int slashSlash = s.IndexOf("//", StringComparison.Ordinal);
        //        if (slashSlash > 0) s = s[..slashSlash];

        //        int firstDigit = s.IndexOfAny("0123456789".ToCharArray());
        //        if (firstDigit > 0) return s[..firstDigit].Trim();

        //        return s.Trim();
        //    }

        public async Task UpdateDevStatus(int Id, string IP, UCSAPINative.UCSAPI_TERMINAL_INFO INFO)
        {
            using var scope = _serviceProvider.CreateScope();
            var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            var Mac = Encoding.ASCII.GetString(INFO.MacAddr).TrimEnd().TrimEnd('\0');

            var newdevice = new DeviceMasterEntity
            {
                DeviceSerialNumber = Id.ToString(),
                DeviceName = "Vridhi",
                DeviceIp = IP,
                DevicePort = "0",
                IsRegistered = "0",
                DeviceType = "A",
                InoutFlag = "I",
                IsActive = "1",
                IsConnected = "1",
                DeviceGroupId = 1,
                LastConnected = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DeviceInfo = " ",
                MacAddress = Mac,
                DeviceUser = "0/0",
                FaceUser = "0/0",
                CardUser = "0/0",
                FingerUser = "0/0",
            };
            await _deviceservice.SetDeviceInfo(newdevice);
            Console.WriteLine("Device Added Successfully : " + Id);

        }

        async void ucsAPI_EventAntipassback(int TerminalID, int UserID)
        {
            await GlobalLogger.LogToFileAsync("<-- EventAntipassback\n" +
    "   +TerminalID: {TerminalID}" + TerminalID + "\n" +
    "   +UserID: {UserID}\n" + UserID + "\n" +
    "   +Result: {Result}" + 1 + "\n"


   ); // 0 = Fail, 1 = Success

            this.serverAuthentication.SendAntipassbackResultToTerminal(TerminalID, UserID, 1);

            await GlobalLogger.LogToFileAsync("--> SendAntipassbackResultToTerminal\n" +
                "   +TerminalID: {TerminalID}" + TerminalID + "\n" +
                "   +UserID: {UserID}" + UserID + "\n" +
                "   +Result: {Result}" + 1 + "\n"


                );

        }

        async void ucsAPI_EventRealTimeAccessLog(int TerminalID)
        {
            await GlobalLogger.LogToFileAsync(
     "<-- EventRealTimeAccessLog\n" +
     "   +TerminalID: " + TerminalID + "\n" +
     "   +ErrorCode: " + this.ucsAPI.EventError + "\n" +
     "   +TerminalID(Internal): " + this.terminalId + "\n" +
     "   +UserID: " + this.accessLogData.UserID + "\n" +
     "   +DateTime: " + this.accessLogData.DateTime + "\n" +
     "   +AuthMode: " + this.accessLogData.AuthMode + "\n" +
     "   +AuthType: " + this.accessLogData.AuthType + "\n" +
     "   +IsAuthorized: " + this.accessLogData.IsAuthorized + "\n" +
     "   +DeviceID: " + this.accessLogData.DeviceID + "\n" +
     "   +Result: " + this.accessLogData.AuthResult + "\n" +
     "   +RFID: " + this.accessLogData.RFID + "\n" +
     "   +PictureDataLength: " + this.accessLogData.PictureDataLength + "\n" +
     "   +ThermalBurnType: " + this.accessLogData.ThermalBurnType + "\n" +
     "   +ThermalBurn: " + this.accessLogData.ThermalBurn + "\n" +
     "   +Progress: " + this.accessLogData.CurrentIndex + "/" + this.accessLogData.TotalNumber
 );


            await UploadLog(this.accessLogData);

        }


        public async Task UploadLog(IAccessLogData accessLog)
        {

            PunchLogEntity record = new PunchLogEntity
            {
                DeviceSerialNumber = this.terminalId,
                DeviceEmployeeCode = accessLog.UserID.ToString(),
                PunchDateTime = DateTime.ParseExact(accessLog.DateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CreatedDate = DateTime.Now.ToString(),
                //PunchInoutFlag = inOut.ToString(),
                IsManual = "0",
                IsDeleted = "0",

            };
            using var scope = _serviceProvider.CreateScope();
            var _service = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            await _service.SetAttendance(record);
        }

        public static void CheckCommand(VridhiDeviceHandler handlerInstance, DeviceCommandEntity cmd)
        {
            handlerInstance.SendCommand(cmd).GetAwaiter().GetResult();
        }


        public async Task SendCommand(DeviceCommandEntity kvp)
        {
            try
            {
                await GlobalLogger.LogToFileAsync("Sending txt2");
                using var scope = _serviceProvider.CreateScope();
                var _device = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

                await GlobalLogger.LogToFileAsync("Easy");
                int terminalID = String.IsNullOrEmpty(terminalId) ? 100 : int.Parse(terminalId);
                await GlobalLogger.LogToFileAsync("Get Terminal Id");

                //int userID = Convert.ToInt32(txtUserID.Text);
                //int terminalID = Convert.ToInt32(txtTerminalID.Text);
                //int logType = cmbLogType.SelectedIndex;


                long commandId = kvp.DeviceCommandId;
                string commandText = kvp.DeviceCommand;
                await GlobalLogger.LogToFileAsync("Entering switch");
                switch (kvp.DeviceCommand)
                {
                    //case cmdServerStart:
                    //    ucsAPI.ServerStart(9999, 8179);
                    //    _logger.LogInformation("--> ServerStart | Server listening; Err=0x{ErrorCode}",
                    //        ucsAPI.ErrorCode.ToString("X4"));
                    //    break;

                    //case cmdServerStop:
                    //    ucsAPI.ServerStop();
                    //    _logger.LogInformation("--> ServerStop | Closed; Err=0x{ErrorCode}",
                    //        ucsAPI.ErrorCode.ToString("X4"));
                    //    break;

                    //case cmdLoadFastSearchDB:
                    //    OpenFileDialog openFileDlg = new OpenFileDialog
                    //    {
                    //        Filter = "UnionFastSearch DB(*.UFS)|*.UFS|All file(*.*)|*.*"
                    //    };
                    //    if (openFileDlg.ShowDialog() == DialogResult.OK)
                    //    {
                    //        fastSearch.ClearDB();
                    //        fastSearch.LoadDBFromFile(openFileDlg.FileName);
                    //        _logger.LogInformation("--> Load Fast Search DB OK | Fp Count={FpCount}", fastSearch.FpCount);
                    //    }
                    //    else
                    //    {
                    //        _logger.LogWarning("--> Load Fast Search DB FAIL");
                    //    }
                    //    break;

                    case "ADD_USER":
                    case "UPDATE_USER":
                        await GlobalLogger.LogToFileAsync("Insert add" + terminalID);
                        try
                        {
                            terminalID = await AddUserToTerminal(terminalID, kvp, _device);
                        }
                        catch (Exception ex)
                        {
                            await GlobalLogger.LogToFileAsync("Error" + ex);
                            throw;
                        }

                        await GlobalLogger.LogToFileAsync("--> AddUserToTerminal executed | TerminalID={TerminalID}" + terminalID);
                        await GlobalLogger.LogToFileAsync("--> AddUserToTerminal executed | TerminalID={TerminalID}" + terminalID);
                        // await _device.UpdateCommandAsync(kvp);
                        break;

                    case "DELETE_USER":
                        await DeleteUserFromTerminal(terminalID, kvp);


                        break;

                    case "DELETE_ALL_USER":
                        terminalUserData.DeleteAllUserFromTerminal(0, terminalID);
                        await GlobalLogger.LogToFileAsync("--> DeleteAllUserFromTerminal | TerminalID={TerminalID}, Err=0x{ErrorCode}" +
                                terminalID, ucsAPI.ErrorCode.ToString("X4"));
                        //await _device.UpdateCommandAsync(kvp);
                        break;

                    case "GET_USER_COUNT":
                        terminalUserData.GetUserCountFromTerminal(0, terminalID);
                        await GlobalLogger.LogToFileAsync("--> GetUserCountFromTerminal | TerminalID={TerminalID}, Err=0x{ErrorCode}" +
                                terminalID, ucsAPI.ErrorCode.ToString("X4"));
                        //await _device.UpdateCommandAsync(kvp);
                        break;

                    case "GET_USER":
                        terminalUserData.GetUserInfoListFromTerminal(0, terminalID);
                        await GlobalLogger.LogToFileAsync("--> GetUserInfoListFromTerminal | TerminalID={TerminalID}, Err=0x{ErrorCode}" +
                                terminalID, ucsAPI.ErrorCode.ToString("X4"));
                        //await _device.UpdateCommandAsync(kvp);
                        break;

                    case "DOWNLOAD_USER":
                        await DownloadUserFromTerminal(terminalID, kvp);
                        //await _device.UpdateCommandAsync(kvp);

                        break;
                    case "ENROLL_ID":
                        await EnrollUserToTerminal(terminalID, kvp);

                        //await _device.UpdateCommandAsync(kvp);

                        break;
                    case "RESTART":

                        break;

                    // … Continue same pattern for other cases …

                    default:
                        _logger.LogWarning("Unknown command: {Command}", commandText);
                        await GlobalLogger.LogToFileAsync("Unknown command: {Command}", commandText);
                        break;
                }

            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync(ex + "Error in SendCommand | Message={Message}" + ex.Message);
            }
        }

        private async Task<int> AddUserToTerminal(
    int terminalID,
   DeviceCommandEntity device, IDeviceDataDownloadService _deviceService)
        {
            try
            {
                await GlobalLogger.LogToFileAsync("Insert add" + terminalID);


                if (device == null)
                    return terminalID;

                var employee = await _deviceService.GetEmployee(device);

                if (employee == null)
                    return terminalID;

                // --- Initialize User Data ---
                serveruserData.InitUserData();
                serveruserData.IsBlacklist = 0;

                serveruserData.UserID = int.Parse(employee.DeviceEmployeeCode);
                serveruserData.UniqueID = employee.EmployeeCode;
                serveruserData.UserName = employee.EmployeeName;

                // Admin flag (only if employee type is admin)
                serveruserData.IsAdmin = 0;
                serveruserData.IsIdentify = 1;   // enable 1:N identification
                serveruserData.IsFace1toN = 1;   // enable face recognition
                serveruserData.IsIris1toN = 1;   // disable iris by default
                serveruserData.AuthType = 0;

                // --- Validity Period ---
                try
                {
                    if (!string.IsNullOrEmpty(employee.ValidityStart) &&
                        !string.IsNullOrEmpty(employee.ValidityEnd))
                    {
                        DateTime startDate = Convert.ToDateTime(employee.ValidityStart);
                        DateTime endDate = Convert.ToDateTime(employee.ValidityEnd);

                        if (startDate < endDate)
                        {
                            serveruserData.SetAccessDate(
                                1,
                                startDate.Year, startDate.Month, startDate.Day,
                                endDate.Year, endDate.Month, endDate.Day
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    await GlobalLogger.LogToFileAsync(ex + "Invalid access date for employee {emp}" + employee.EmployeeCode);
                }

                // --- Set Authentication Type (Face Only here) ---
                serveruserData.SetAuthType(0, 0, 0, 0, 0, 0);     // clear
                serveruserData.SetAuthTypeEx(1, 1, 0, 0, 0, 0, 0, 0); // Face and card only 
                if (employee.UserauthTypeId == 1 || employee.UserauthTypeId == 4) // 1/2 Card Auth
                {

                    if (employee.CardNo != null)
                    {
                        int decimalValue = int.Parse(employee.CardNo);

                        // decimal → hex (big-endian text)
                        string hex = decimalValue.ToString("X8");   // 1F690836

                        // convert to bytes and reverse to little-endian
                        byte[] byte1 = Convert.FromHexString(hex);
                        Array.Reverse(byte1);

                        // little-endian hex
                        string Hex = Convert.ToHexString(byte1);
                        serveruserData.SetCardData(1, Hex);
                    }
                }
                // --- Attach Face Data ---
                if (employee.UserauthTypeId == 3 || employee.UserauthTypeId == 4) // 3/4 = Face auth
                {
                    await GlobalLogger.LogToFileAsync("Employee Settings are right");
                    if (!string.IsNullOrEmpty(employee.FaceImagePath))
                    {
                        string rootPath = Directory.GetCurrentDirectory();
                        string basePath = Directory.GetParent(rootPath)!.FullName;
                        var facePath = System.IO.Path.Combine(basePath, "Documents", employee.FaceImagePath);

                        // var facePath = System.IO.Path.Combine(basePath, employee.FaceImagePath);
                        await GlobalLogger.LogToFileAsync("Employee has image path");
                        if (File.Exists(facePath))
                        {
                            await GlobalLogger.LogToFileAsync("Employee image path exist");
                            byte[] data = await ResizeImage(facePath, 340, 340);
                            using var fs = new FileStream(facePath, FileMode.Open, FileAccess.Read);
                            using var br = new BinaryReader(fs);
                            await GlobalLogger.LogToFileAsync("Employee Image Resized");
                            int nFaceCount = br.ReadInt32(); // template count
                            byte[] biFaceData = br.ReadBytes((int)(fs.Length - 4));

                            //if (nFaceCount < 1) nFaceCount = 1;   // fallback
                            if (nFaceCount > 10) nFaceCount = 10; // max safe

                            serveruserData.FaceNumber = 0;
                            serveruserData.FaceData = null;

                            // WalkThrough packet for UBIO-XPro2 and similar
                            WalkThrough = new WALKTHROUGH_DATA
                            {
                                Type = 12,
                                Length = data.Length,
                                Data = new byte[data.Length]
                            };
                            Buffer.BlockCopy(data, 0, WalkThrough.Data, 0, data.Length);

                            serveruserData.SetWalkThroughData(WalkThrough.Type, WalkThrough.Length, WalkThrough.Data);
                            await GlobalLogger.LogToFileAsync("Picture Updated");
                        }
                    }
                }

                // --- Send to Terminal ---
                try
                {
                    await GlobalLogger.LogToFileAsync("\"--> Add user to terminal {term}");
                    serveruserData.AddUserToTerminal(1, terminalID, 1);
                }
                catch (COMException ex)
                {
                    await GlobalLogger.LogToFileAsync(ex + "COM error while adding user {emp} to terminal {term}" + employee.EmployeeCode + terminalID);
                    return terminalID;
                }

                // Log result
                await GlobalLogger.LogToFileAsync("--> Add user to terminal {term}, result Err=0x{err:X4}" + terminalID + serveruserData.ErrorCode);
                // await GlobalLogger.LogToFileAsync("--> Add user to terminal {term}, result Err=0x{err:X4}"+ terminalID+serveruserData.ErrorCode);
                return terminalID;
            }
            catch (Exception ex)
            {
                await GlobalLogger.LogToFileAsync(ex + "Error while adding user to terminal {term}" + terminalID);
                throw;
            }
        }


        private async Task DeleteUserFromTerminal(int deviceID, DeviceCommandEntity device)
        {


            if (device != null)
            {

                terminalUserData.DeleteUserFromTerminal(0, deviceID, Convert.ToInt32(device.DeviceUserId));

                await GlobalLogger.LogToFileAsync($"--> DeleteUserFromTerminal | TerminalID={deviceID}, UserID={device.DeviceUserId}, Err=0x{ucsAPI.ErrorCode.ToString("X4")}" +
                                deviceID + device.DeviceUserId + ucsAPI.ErrorCode.ToString("X4"));

            }
        }

        private async Task DownloadUserFromTerminal(int deviceID, DeviceCommandEntity device)
        {



            if (device != null)
            {

                terminalUserData.GetUserDataFromTerminal(0, deviceID, Convert.ToInt32(device.DeviceUserId));
                await GlobalLogger.LogToFileAsync("--> DownloadUserFromTerminal | TerminalID={TerminalID}, UserID={UserID}, Err=0x{ErrorCode}" +
                                deviceID + device.DeviceUserId + ucsAPI.ErrorCode.ToString("X4"));

            }
        }

        async void ucsAPI_EventGetUserCount(int ClientID, int TerminalID, int AdminCount, int UserCount)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventGetUserCount
   +CID, TID : {ClientID}, {TerminalID}
   +Admin, User : {AdminCount}, {UserCount}
   +MaxUser : {terminalUserData.MaxUserNumber}
   +RegFinger : {terminalUserData.RegFingerNumber}
   +MaxFinger : {terminalUserData.MaxFingerNumber}
   +RegFace : {terminalUserData.RegFaceNumber}
   +MaxFace : {terminalUserData.MaxFaceNumber}
");
            await UpdateCommandAsync(ucsAPI.EventError, "GET_USER_COUNT", TerminalID.ToString());
        }


        async void ucsAPI_EventGetAccessLogCount(int ClientID, int TerminalID, int LogCount)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventGetAccessLogCount
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +LogCount: {LogCount}
   +ErrorCode: {this.ucsAPI.EventError}
");
        }

        async void ucsAPI_EventGetAccessLog(int ClientID, int TerminalID)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventGetAccessLog
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +ErrorCode: {this.ucsAPI.EventError}
   +UserID: {this.accessLogData.UserID}
   +DateTime: {this.accessLogData.DateTime}
   +AuthMode: {this.accessLogData.AuthMode}
   +AuthType: {this.accessLogData.AuthType}
   +IsAuthorized: {this.accessLogData.IsAuthorized}
   +Result: {this.accessLogData.AuthResult}
   +RFID: {this.accessLogData.RFID}
   +ThermalBurnType: {this.accessLogData.ThermalBurnType}
   +ThermalBurn: {this.accessLogData.ThermalBurn}
   +PictureDataLength: {this.accessLogData.PictureDataLength}
   +Progress: {this.accessLogData.CurrentIndex}/{this.accessLogData.TotalNumber}
");
        }

        async void ucsAPI_EventDeleteUser(int ClientID, int TerminalID, int UserID)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventDeleteUser
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +UserID: {UserID}
   +ErrorCode: {this.ucsAPI.EventError}
");
            await UpdateCommandAsync(ucsAPI.EventError, "DELETE_USER", TerminalID.ToString());
        }

        async void ucsAPI_EventDeleteAllUser(int ClientID, int TerminalID)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventDeleteAllUser
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +ErrorCode: {this.ucsAPI.EventError}
");
            await UpdateCommandAsync(ucsAPI.EventError, "DELETE_ALL_USER", TerminalID.ToString());
        }

        async void ucsAPI_EventAddUser(int ClientID, int TerminalID, int UserID)
        {
            await GlobalLogger.LogToFileAsync($@"
<-- EventAddUser
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +UserID: {UserID}
   +ErrorCode: {this.ucsAPI.EventError}
");

            int errorcode = this.ucsAPI.EventError;

            await GlobalLogger.LogToFileAsync($"--> Add user to terminal {TerminalID}");



            updatecmd(errorcode, "ADD_USER", TerminalID.ToString());
        }

        async void ucsAPI_EventGetUserInfoList(int ClientID, int TerminalID)
        {
            _logger.LogInformation("<--EventGetUserInfoList");
            _logger.LogInformation("   +ClientID:{ClientID}", ClientID);
            _logger.LogInformation("   +TerminalID:{TerminalID}", TerminalID);
            _logger.LogInformation("   +ErrorCode:{ErrorCode}", this.ucsAPI.EventError);
            _logger.LogInformation("   +UserID:{UserID}", this.terminalUserData.UserID);
            _logger.LogInformation("   +Admin:{IsAdmin}", this.terminalUserData.IsAdmin);
            _logger.LogInformation("   +AuthType:{AuthType}", this.terminalUserData.AuthType);
            _logger.LogInformation("   +Blacklist:{IsBlacklist}", this.terminalUserData.IsBlacklist);
            _logger.LogInformation("   +Progress:{CurrentIndex}/{TotalNumber}",
                this.terminalUserData.CurrentIndex,
                this.terminalUserData.TotalNumber);
            string ImagePath = "";
            //string file = "";
            string userId = terminalUserData.UserID.ToString();
            if (this.terminalUserData.PictureDataLength > 0)
            {
                byte[] biPictureData = (byte[])this.terminalUserData.PictureData;
                ImagePath = await SaveEmployeeImageAsync(biPictureData, userId);
                _logger.LogInformation("   +PictureData Length:{Length}", this.terminalUserData.PictureDataLength);
            }
            await SetUserInServer(this.terminalId, userId, ImagePath);
        }

        async void ucsAPI_EventGetUserData(int ClientID, int TerminalID)
        {
            byte[] biFPData1 = null;
            byte[] biFPData2 = null;
            long nFPDataSize1;
            long nFPDataSize2;
            long nFPDataCount;
            long nCardNumber;
            string file = "";
            string userId = terminalUserData.UserID.ToString();
            await GlobalLogger.LogToFileAsync($@"
<-- EventGetUserData
   +ClientID: {ClientID}
   +TerminalID: {TerminalID}
   +ErrorCode: {this.ucsAPI.EventError}
   +UserID: {this.terminalUserData.UserID}
   +UserName: {this.terminalUserData.UserName}
   +UniqueID: {this.terminalUserData.UniqueID}
   +Password: {this.terminalUserData.Password}
   +Property:
      +Finger: {this.terminalUserData.IsFinger}, FPCard: {this.terminalUserData.IsFPCard}, Password: {this.terminalUserData.IsPassword}, Card: {this.terminalUserData.IsCard}, CardID: {this.terminalUserData.IsCardID}, Op: {this.terminalUserData.IsAndOperation}, Identify: {this.terminalUserData.IsIdentify}, Admin: {this.terminalUserData.IsAdmin}
   +PropertyEx:
      +Face: {this.terminalUserData.IsFace}, Iris: {this.terminalUserData.IsIris}, MobileKey: {this.terminalUserData.IsMobileKey}
   +AuthType: {this.terminalUserData.AuthType}
   +AccessGroup: {this.terminalUserData.AccessGroup}
   +AccessDateType: {this.terminalUserData.AccessDateType}
   +AccessDate: {this.terminalUserData.StartAccessDate} ~ {this.terminalUserData.EndAccessDate}
   +Card Count: {this.terminalUserData.CardNumber}
");

            // Card list
            for (int i = 0; i < this.terminalUserData.CardNumber; i++)
            {
                await GlobalLogger.LogToFileAsync(
                    $"   +RFID: {this.terminalUserData.get_RFID(i)}"
                );
            }

            // Total finger count
            nFPDataCount = this.terminalUserData.TotalFingerCount;

            await GlobalLogger.LogToFileAsync(
                $"   +TotalFingerCount: {nFPDataCount}"
            );

            for (int i = 0; i < nFPDataCount; i++)
            {
                int nFingerID = this.terminalUserData.get_FingerID(i);
                nFPDataSize1 = this.terminalUserData.get_FPSampleDataLength(nFingerID, 0);
                biFPData1 = this.terminalUserData.get_FPSampleData(nFingerID, 0) as byte[];
                if (biFPData1 != null)
                {
                    file = string.Format(".\\Fingerprint_{0:d8}-{1:d2}-1.uct", terminalUserData.UserID, nFingerID);
                    using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                    {
                        binaryWriter.Write(biFPData1);
                    }
                }

                nFPDataSize2 = this.terminalUserData.get_FPSampleDataLength(nFingerID, 1);
                biFPData2 = this.terminalUserData.get_FPSampleData(nFingerID, 1) as byte[];
                if (biFPData2 != null)
                {
                    file = string.Format(".\\Fingerprint_{0:d8}-{1:d2}-2.uct", terminalUserData.UserID, nFingerID);
                    using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                    {
                        binaryWriter.Write(biFPData2);
                    }
                }
            }

            int nFaceCount = this.terminalUserData.FaceNumber;
            if (nFaceCount > 0)
            {
                byte[] biFaceData = (byte[])this.terminalUserData.FaceData;
                await GlobalLogger.LogToFileAsync("   +FaceData(Number, DataLength): {Count}, {Length}" + nFaceCount + biFaceData.Length);

                file = string.Format(".\\FaceData_{0:d8}.dat", terminalUserData.UserID);
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(nFaceCount);
                    binaryWriter.Write(biFaceData);
                }
            }

            if (this.terminalUserData.IrisDataLength > 0)
            {
                file = string.Format(".\\IrisData_{0:d8}.dat", terminalUserData.UserID);
                byte[] biIrisData = (byte[])this.terminalUserData.IrisData;
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(biIrisData);
                }
                await GlobalLogger.LogToFileAsync("   +IrisLength:{Length}" + this.terminalUserData.IrisDataLength);
            }

            if (this.terminalUserData.WalkThroughLength > 0)
            {
                file = string.Format(".\\WalkThroughData_{0:d8}.dat", terminalUserData.UserID);
                byte[] biWalkThroughData = (byte[])this.terminalUserData.WalkThroughData;
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(biWalkThroughData);
                }
                await GlobalLogger.LogToFileAsync("   +WalkThroughType:{Type}" + this.terminalUserData.WalkThroughType);
                await GlobalLogger.LogToFileAsync("   +WalkThroughLength:{Length}" + this.terminalUserData.WalkThroughLength);
            }

            if (this.terminalUserData.WalkThroughLength_2 > 0)
            {
                file = string.Format(".\\WalkThroughData_2_{0:d8}.jpg", terminalUserData.UserID);
                byte[] biWalkThroughData = (byte[])this.terminalUserData.WalkThroughData_2;
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(biWalkThroughData);
                }
                await GlobalLogger.LogToFileAsync("   +WalkThroughType_2:{Type}" + this.terminalUserData.WalkThroughType_2);
                await GlobalLogger.LogToFileAsync("   +WalkThroughLength_2:{Length}" + this.terminalUserData.WalkThroughLength_2);
            }
            string ImagePath = "";
            if (this.terminalUserData.PictureDataLength > 0)
            {
                byte[] biPictureData = (byte[])this.terminalUserData.PictureData;
                file = string.Format(".\\PictureData_{0:d8}.jpg", terminalUserData.UserID);
                ImagePath = await SaveEmployeeImageAsync(biPictureData, userId);
                using (FileStream fileStream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(biPictureData);
                }
                await GlobalLogger.LogToFileAsync("   +PictureData Length:{Length}" + this.terminalUserData.PictureDataLength);

            }
            await SetUserInServer(this.terminalId, userId, ImagePath);
            await UpdateCommandAsync(ucsAPI.EventError, "DOWNLOAD_USER", TerminalID.ToString());
        }

        private async Task<string> SaveEmployeeImageAsync(byte[] biPictureData, string userId)
        {
            string rootPath = Directory.GetCurrentDirectory();
            string parentPath = Directory.GetParent(rootPath)!.FullName; // goes up one level
            string documentsPath = System.IO.Path.Combine(parentPath, "Documents");
            string dbImagePath;
            using var scope = _serviceProvider.CreateScope();
            var easyAccess = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            DeviceCommandEntity deviceCommand = new DeviceCommandEntity() { DeviceUserId = userId };
            var Emp = await easyAccess.GetEmployee(deviceCommand);
            // Step 2: Create the directory path
            string baseFolder = System.IO.Path.Combine(Emp.CompanyId, "DeviceFace");
            string directoryPath = System.IO.Path.Combine(documentsPath, baseFolder);
            Directory.CreateDirectory(directoryPath);
            string fileName = $"{Emp.EmployeeId.ToString()}.jpg";
            string fullImagePath = System.IO.Path.Combine(directoryPath, fileName); // Step 4: Save image
            await File.WriteAllBytesAsync(fullImagePath, biPictureData);
            dbImagePath = System.IO.Path.Combine(baseFolder, fileName).Replace("\\", "/");
            return dbImagePath;
        }

        private async Task SetUserInServer(string deviceSerialNumber, string userId, string imagePath)
        {
            using var scope = _serviceProvider.CreateScope();
            //var easyAccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
            var _service = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();


            {
               // var cmpId = await easyAccess.TblDeviceMasters.Where(x => x.DeviceSerialNumber == deviceSerialNumber).Select(x => x.CompanyId).FirstOrDefaultAsync() ?? "0001";
                var newemployee = new EmployeeEntity
                {
                    EmployeeCode = userId,
                    EmployeeName = "Unknown",
                    DeviceEmployeeCode = userId,
                    FaceImagePath = imagePath,
                    CreatedDate = DateTime.Now.ToString(),
                    UpdatedDate = DateTime.Now.ToString(),
                    IsActive = 1,
                    IsDeleted = 0,
                    //CompanyId = cmpId,
                    DepartmentId = 1,
                    BranchId = 1,
                    DesignationId = 1,
                    CategoryId = 1,
                    JoiningDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    //MappedDeviceGroup = devicegroup.ToString(),
                    //CardNo = cardno,
                    EmployeeTypeId = 1,
                    AccessTypeId = 2,
                    ValidityStart = DateTime.Now.ToString("yyyy-MM-dd"),
                    ValidityEnd = DateTime.Now.AddYears(5).ToString("yyyy-MM-dd"),
                    GenderId = 1,
                    UserauthTypeId = 4,
                };
                await _service.SetUserInfo(newemployee);
                _logger.LogInformation("Employee added with the UserID: {UserID}", userId);
            }
        }

        private async Task UpdateCommandToPending(long commandid, int result = 1)
        {
            DeviceCommandEntity device = new DeviceCommandEntity() { DeviceCommandId = commandid };
            using var scope = _serviceProvider.CreateScope();
            var _service = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();

           


                device.CommondStatusId = result == 1 ? 2 : -1; // 2 = Pending, -1 = Error while senduing
                device.CommondResponce = result == 1 ? "Pending" : "Send Error while sending";

                device.CreatedDate = DateTime.Now.ToString();

                await _service.UpdateCommandAsync(device);
            
        }

        public static bool IsValidFaceTemplate(string filePath, out int faceCount)
        {
            faceCount = 0;

            if (!File.Exists(filePath))
                return false;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Step 1: if file starts with JPG magic numbers, it's NOT a template
                byte[] header = br.ReadBytes(2);
                if (header[0] == 0xFF && header[1] == 0xD8)
                {
                    // JPEG file
                    return false;
                }

                // Reset stream
                fs.Seek(0, SeekOrigin.Begin);

                try
                {
                    // Step 2: UnionCommunity templates start with an Int32 (FaceCount)
                    faceCount = br.ReadInt32();

                    // sanity check: must be between 3 and 10
                    if (faceCount < 1 || faceCount > 10)
                        return false;

                    long expectedDataSize = fs.Length - 4;
                    if (expectedDataSize <= 0)
                        return false;

                    // Step 3: Read data bytes
                    byte[] data = br.ReadBytes((int)expectedDataSize);

                    // UnionCommunity face templates are usually binary blobs > 1KB
                    if (data.Length < 1000)
                        return false;

                    return true; // looks like a valid template
                }
                catch
                {
                    return false; // failed parsing as template
                }
            }
        }

        async void ucsAPI_EventWalkThroughData(int ClientID, int TerminalID,
                                        int UserID, int DataType,
                                        int DataLength, object EventData)
        {

            byte[] data = (byte[])EventData;

            await GlobalLogger.LogToFileAsync($@"
<-- EventWalkThroughData
 CID,TID : {ClientID}, {TerminalID}
 UserID : {UserID}
 Data Type : {DataType}   // 12 = JPG, 13 = Template
 Data Length : {DataLength}
");

            if (DataType == 12)
            {

                string path = await SaveEmployeeImageAsync(data, ClientID.ToString());
                using var scope = _serviceProvider.CreateScope();
                //var easyAccess = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
                var _device = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
                string DevCode = ClientID.ToString();
                DeviceCommandEntity cmd = new DeviceCommandEntity { DeviceUserId = DevCode };

                EmployeeEntity Emp = await _device.GetEmployee(cmd) ?? new EmployeeEntity { DeviceEmployeeCode = DevCode, FaceImagePath = path };
                Emp.FaceImagePath = path;
                
                await _device.SetUserInfo(Emp);

                await GlobalLogger.LogToFileAsync("--> JPG saved: " + path);
               // DeviceCommandEntity cmd = new DeviceCommandEntity { DeviceUserId = DevCode };
                await AddUserToTerminal(TerminalID, cmd, _device);
                await UpdateCommandAsync(ucsAPI.ErrorCode, "ENROLL_ID", TerminalID.ToString());


            }
            else if (DataType == 13)
            {
                await GlobalLogger.LogToFileAsync("--> Template saved to DB");


                WalkThrough = new WALKTHROUGH_DATA
                {
                    Type = 12,
                    Length = data.Length,
                    Data = new byte[data.Length]
                };
                Buffer.BlockCopy(data, 0, WalkThrough.Data, 0, data.Length);

                serveruserData.SetWalkThroughData(
                    WalkThrough.Type,
                    WalkThrough.Length,
                    WalkThrough.Data);

                await GlobalLogger.LogToFileAsync("--> Template pushed to device");
            }

        }




        public async Task<byte[]> ResizeImage(string imagePath, int IMAGE_WIDTH, int IMAGE_HEIGHT)
        {
            string dir = System.IO.Path.GetDirectoryName(imagePath);
            string fileWithoutExt = System.IO.Path.GetFileNameWithoutExtension(imagePath);
            string resizedFileName = System.IO.Path.Combine(dir, fileWithoutExt + "_resized.jpg");

            // ✅ If resized image exists, return it
            if (File.Exists(resizedFileName))
            {
                return await File.ReadAllBytesAsync(resizedFileName);
            }

            // ✅ Load original image using ImageSharp
            using (SixLabors.ImageSharp.Image image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath))
            {
                // ✅ Resize (high quality)
                image.Mutate(x => x.Resize(IMAGE_WIDTH, IMAGE_HEIGHT));

                using (MemoryStream ms = new MemoryStream())
                {
                    var encoder = new JpegEncoder
                    {
                        Quality = 90
                    };

                    // ✅ Save resized image into memory + new file
                    await image.SaveAsJpegAsync(ms, encoder);
                    byte[] output = ms.ToArray();

                    await File.WriteAllBytesAsync(resizedFileName, output);

                    return output;
                }
            }
        }







        public void updatecmd(int errcode, string command, string sn)
        {
            UpdateCommandAsync(errcode, command, sn).GetAwaiter().GetResult();

        }

        public async Task UpdateCommandAsync(int errcode, string command, string sn)
        {
            using var scope = _serviceProvider.CreateScope();
            var _service = scope.ServiceProvider.GetRequiredService<IDeviceDataDownloadService>();
            //var db = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();
          DeviceCommandEntity deviceCommand = new DeviceCommandEntity() { DeviceCommand = command };

            if (deviceCommand != null)
            {
                if (errcode == 0)
                {
                    deviceCommand.CommondStatusId = 1;
                    deviceCommand.CommondResponce = "Success";
                }
                else if (errcode == 771)
                {
                    deviceCommand.CommondStatusId = -1;
                    deviceCommand.CommondResponce = "Correct Image";
                }
                else
                {
                    deviceCommand.CommondStatusId = -1;
                    deviceCommand.CommondResponce = "Error";
                }
                deviceCommand.UpdatedDate = DateTime.Now.ToString();

                
                await _service.UpdateCommandAsync(deviceCommand);
            }


        }

        private async Task<int> EnrollUserToTerminal(
  int terminalID,
  DeviceCommandEntity device)
        {


            if (device == null)
                return terminalID;


            //if_enrollcmd = 1;

            int UserID = int.Parse(device.DeviceUserId);

            terminalUserData.RegistWalkThroughFaceFromTerminal(UserID, terminalID, 0);



            return terminalID;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class WALKTHROUGH_DATA
        {
            public int Type;
            public int Length;
            public byte[] Data;
        }
    }
}
