using DeviceServices.Models;
using DeviceServices.Services;

namespace AVTech.Device.BusinessServices.Implementation
{
    public class DeviceDataDownloadService : IDeviceDataDownloadService
    {
        private ILogger<DeviceDataDownloadService> _logger;
        private IServiceProvider _serviceProvider;
        private readonly IDeviceDatabaseService _deviceDatabase;
        public DeviceDataDownloadService(ILogger<DeviceDataDownloadService> logger, IDeviceDatabaseService deviceDatabase, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _deviceDatabase = deviceDatabase;
            _serviceProvider = serviceProvider;
        }

        public async Task SetDeviceInfo(DeviceMasterEntity dev)
        {
            try
            {
                if (string.IsNullOrEmpty(dev.DeviceSerialNumber))
                {
                    await GlobalLogger.LogToFileAsync("Error<<<<<No Sn Passed>>>>>");
                    return;
                }



                dev.LastConnected = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                dev.CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await _deviceDatabase.AddUpdateDevMaster(dev);


            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task UpdateCommandAsync(DeviceCommandEntity Cmd)
        {

            if (string.IsNullOrEmpty(Cmd.DeviceSerialNumber))
            {
                string msg = $"Device SN missing in status check";
                Console.WriteLine(msg);
                await GlobalLogger.LogToFileAsync(msg);
                return;
            }

            await _deviceDatabase.AddUpdateDevCommand(Cmd);
        }

        public async Task UpdateSendingCommandAsync(DeviceCommandEntity Cmd)
        {

            if (string.IsNullOrEmpty(Cmd.DeviceSerialNumber))
            {
                string msg = $"Device SN missing in status check";
                Console.WriteLine(msg);
                await GlobalLogger.LogToFileAsync(msg);
                return;
            }

            await _deviceDatabase.AddUpdateDevSendCommand(Cmd);
        }



        public async Task HandleDeviceStatus(DeviceMasterEntity dev, string clientIp = " ", int clientPort = 0)
        {

            if (string.IsNullOrEmpty(dev.DeviceSerialNumber))
            {
                string msg = $"Device SN missing in status check from {clientIp}:{clientPort}";
                Console.WriteLine(msg);
                _logger.LogWarning(msg);
                await GlobalLogger.LogToFileAsync(msg);
                return;
            }
            dev.LastConnected =DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            dev.DeviceUser = dev.UsedUser == null ? dev.DeviceUser : $"{dev.UsedUser ?? "0"}/{dev.TotalUser ?? "0"}";
            dev.FaceUser = dev.UsedFaceUser == null ? dev.FaceUser : $"{dev.UsedFaceUser ?? "0"}/{dev.TotalFaceUser ?? "0"}";
            dev.CardUser = dev.UsedCardUser == null ? dev.CardUser : $"{dev.UsedCardUser ?? "0"}/{dev.TotalCardUser ?? "0"}";
            dev.UpdatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


            await _deviceDatabase.AddUpdateDevMaster(dev);
            //using var scope = _serviceProvider.CreateScope();
            //var _deviceservice = scope.ServiceProvider.GetRequiredService<IDeviceMasterService>();
            //var db = scope.ServiceProvider.GetRequiredService<EasyAccessDbContext>();

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
        public async Task SetAttendance(PunchLogEntity punch)
        {
            if (string.IsNullOrEmpty(punch.DeviceSerialNumber))
            {
                await GlobalLogger.LogToFileAsync("Error<<<<<No Sn Passed>>>>>");
                return;
            }
            await GlobalLogger.LogToFileAsync("SetAttendence --1");
            if (string.IsNullOrEmpty(punch.DeviceEmployeeCode))
            {
                await GlobalLogger.LogToFileAsync("Error<<<<<No EmpCode Passed>>>>>");
                return;
            }
            if (punch.PunchDateTime == null)
            {
                await GlobalLogger.LogToFileAsync("Error<<<<<No Punch Passed>>>>>");
                return;
            }
            punch.CreatedDate = DateTime.Now.ToString();
            punch.IsManual = "0";
            punch.IsDeleted = "0";
            if (punch.PhtBase64Img != null)
            {
                string safeTimestamp = punch.PunchDateTime.ToString("yyyy-MM-dd_HH-mm-ss");
                var image = await SaveDeviceLogImageAsync(punch.PhtBase64Img, punch.DeviceSerialNumber, safeTimestamp, punch.DeviceEmployeeCode);
                punch.logimagepath = image;
            }
            await GlobalLogger.LogToFileAsync($"SetAttendence --2");
            await _deviceDatabase.AddUpdateDevlog(punch);
            string msg = $"Attendance logs processed for SN {punch.DeviceSerialNumber}, Count={0}";
            Console.WriteLine(msg);
            _logger.LogInformation(msg);

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

        public async Task SetUserInfo(EmployeeEntity emp)
        {
            try
            {

                string compId = " ";
                string dbImagePath = " ";
                //if (!String.IsNullOrEmpty(emp.EmpBase64Img))
                //{
                //    dbImagePath = await SaveEmployeeImageAsync(emp.EmpBase64Img, compId, emp.EmployeeCode);
                //    emp.FaceImagePath = dbImagePath;

                //}
                //else
                //{
                //    emp.FaceImagePath = null;
                //}


                emp.DepartmentId = 1;
                emp.EmployeeName = emp.EmployeeCode;
                emp.BranchId = 1;
                emp.DesignationId = 1;
                emp.CategoryId = 1;
                emp.JoiningDate = DateTime.Now.ToString("yyyy-MM-dd");

                //emp.FaceImagePath = dbImagePath;
                emp.CardNo = emp.CardNo;
                emp.EmployeeTypeId = 1;
                emp.AccessTypeId = 2;
                emp.ValidityStart = DateTime.Now.ToString("yyyy-MM-dd");
                emp.ValidityEnd = DateTime.Now.AddYears(5).ToString("yyyy-MM-dd");
                emp.IsActive = 1;
                emp.GenderId = 1;
                emp.UserauthTypeId = 4;
                await _deviceDatabase.AddUpdateDevEmp(emp);
            }

            catch (Exception ex)
            {
                string msg = $"Set user Info --  Unhandled error in SetUserInfo: {ex.Message}";
                Console.WriteLine(msg);
                _logger.LogError(ex, msg);
                await GlobalLogger.LogToFileAsync(msg);
            }
        }

        private async Task<string> SaveEmployeeImageAsync(string base64Image, string compId, string EmployeeCode)
        {
            string rootPath = Directory.GetCurrentDirectory();
            string parentPath = Directory.GetParent(rootPath)!.FullName; // goes up one level
            string documentsPath = System.IO.Path.Combine(parentPath, "Documents");
            string dbImagePath;
            if (base64Image != "0")
            {
                if (base64Image.StartsWith("record"))
                {
                    var commaIndex = base64Image.IndexOf(',');
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

        public async Task SendCommand()
        {
            try
            {

                var commands = await _deviceDatabase.CheckCommand();

                //int userID = Convert.ToInt32(txtUserID.Text);
                //int terminalID = Convert.ToInt32(txtTerminalID.Text);
                //int logType = cmbLogType.SelectedIndex;
                await GlobalLogger.LogToFileAsync("Get Command");
                if (commands.Count < 1)
                {
                    await GlobalLogger.LogToFileAsync("Command Less Than 1");
                    return;
                }
                for (int i = 0; i < commands.Count; i++)
                {
                    await GlobalLogger.LogToFileAsync("Entered Loop");

                    var kvp = commands.ElementAt(i);
                    long commandId = kvp.DeviceCommandId;
                    var command = await GetDeviceCommand(commandId);
                    var DevModelId = kvp.DeviceModelId;
                    await GlobalLogger.LogToFileAsync("Entering switch");
                    switch (DevModelId)
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

                        case 1:
                            {
                                break;
                            }
                        case 2:
                            {


                                break;
                            }
                        case 3:
                            {
                                var handlerInstance = _serviceProvider.GetRequiredService<VridhiDeviceHandler>();
                                await Task.Run(() => VridhiDeviceHandler.CheckCommand(handlerInstance, command));
                                break;
                            }
                        case 4:
                            {
                                var handlerInstance = _serviceProvider.GetRequiredService<WebSocketServerManage>();
                                await Task.Run(() => WebSocketServerManage.CheckCommand(handlerInstance, command));

                                break;

                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendCommand | Message={Message}", ex.Message);
            }
        }

        public async Task<DeviceCommandEntity> GetDeviceCommand(long CMD)
        {
            var cmd = await _deviceDatabase.GetCommandFromDatabase(CMD);
            return cmd;
        }

        public async Task<EmployeeEntity> GetEmployee(DeviceCommandEntity CMD)
        {
            var cmd = await _deviceDatabase.GetEmpFromDatabase(CMD);
            return cmd;
        }
        public async Task<DeviceMasterEntity> GetDeviceSn(string sn)
        {

            var cmd = await _deviceDatabase.GetDevSnFromDatabase(sn);
            return cmd;
        }

        public async Task<List<DeviceCommandEntity>> CheckCommandZkt(string? DeviceSerialNo)
        {
            var commands = await _deviceDatabase.CheckCommand();
            commands = commands.Where(x => x.DeviceSerialNumber == DeviceSerialNo).ToList();
            return commands ?? new List<DeviceCommandEntity>();
        }

       
    }
}
