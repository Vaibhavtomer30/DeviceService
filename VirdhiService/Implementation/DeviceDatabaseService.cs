using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using UCBioBSPCOMLib;
using Microsoft.Data.SqlClient;

using Microsoft.EntityFrameworkCore;
using DeviceServices.Models;
using DeviceServices.Services;

namespace DeviceServices.Implementation
{
    public class DeviceDatabaseService : IDeviceDatabaseService
    {
        private readonly AppDbContext _easyAccessDbContext;
        
        private readonly IServiceProvider _serviceProvider;
        
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache
            = new();
        int flag = 0;

        public DeviceDatabaseService(AppDbContext easyAccessDbContext,  IServiceProvider serviceProvider) 
        {
            _easyAccessDbContext = easyAccessDbContext;
            
            _serviceProvider = serviceProvider;
        }

      
public async Task AddUpdateDevEmp(EmployeeEntity emp)
    {
        try
        {
            var connection = _easyAccessDbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = "[Bio].[uspInsertUpdateEmployee]";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@DeviceEmployeeID", 0));
            command.Parameters.Add(new SqlParameter("@EmployeeID", emp.EmployeeId));
            command.Parameters.Add(new SqlParameter("@EmployeeName", emp.EmployeeName ?? ""));
            command.Parameters.Add(new SqlParameter("@CardNumber", (object?)emp.CardNo ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@EmployeeCode", emp.EmployeeCode));

            command.Parameters.Add(new SqlParameter("@DateOfJoining",
                string.IsNullOrWhiteSpace(emp.JoiningDate)
                    ? DBNull.Value
                    : DateTime.Parse(emp.JoiningDate)));

            command.Parameters.Add(new SqlParameter("@DateOfLeaving",
                emp.LeavingDate ?? (object)DBNull.Value));

            command.Parameters.Add(new SqlParameter("@EmployeeStatusID",
                emp.IsActive == 1 ? 2 : 1));

            command.Parameters.Add(new SqlParameter("@EmployeeTypeID", emp.EmployeeTypeId));

            command.Parameters.Add(new SqlParameter("@GenderID", emp.GenderId));
            command.Parameters.Add(new SqlParameter("@CompanyID", 1));               // Set your CompanyId
            command.Parameters.Add(new SqlParameter("@DepartmentID", emp.DepartmentId));
            command.Parameters.Add(new SqlParameter("@DesignationID", emp.DesignationId));
            command.Parameters.Add(new SqlParameter("@BranchID", emp.BranchId));
            command.Parameters.Add(new SqlParameter("@GradeID", emp.CategoryId));

            command.Parameters.Add(new SqlParameter("@EmailIdOfficial", DBNull.Value));
            command.Parameters.Add(new SqlParameter("@ContactNumberOfficial", DBNull.Value));
            command.Parameters.Add(new SqlParameter("@CurrentAddress", DBNull.Value));
            command.Parameters.Add(new SqlParameter("@AadharNumber", DBNull.Value));

            command.Parameters.Add(new SqlParameter("@EnrollmentDeviceID",
                emp.EnrollmentDeviceId ?? 0));

            command.Parameters.Add(new SqlParameter("@UserType", "D"));
            command.Parameters.Add(new SqlParameter("@UserAuthTypeID", emp.UserauthTypeId));

            command.Parameters.Add(new SqlParameter("@ValidityStart",
                string.IsNullOrWhiteSpace(emp.ValidityStart)
                    ? DBNull.Value
                    : DateTime.Parse(emp.ValidityStart)));

            command.Parameters.Add(new SqlParameter("@ValidityEnd",
                string.IsNullOrWhiteSpace(emp.ValidityEnd)
                    ? DBNull.Value
                    : DateTime.Parse(emp.ValidityEnd)));

            command.Parameters.Add(new SqlParameter("@MaxOpenDoorTime", emp.MaxAccessCount));

            command.Parameters.Add(new SqlParameter("@FaceImagePath",
                (object?)emp.FaceImagePath ?? DBNull.Value));

            command.Parameters.Add(new SqlParameter("@AuthTypeDataBase64",
                (object?)emp.EmpBase64Img ?? DBNull.Value));

            command.Parameters.Add(new SqlParameter("@ReSyncData", false));

            command.Parameters.Add(new SqlParameter("@FaceDownloadFromDevice",
                emp.FaceDownloadFromDevice == "Y"));

            command.Parameters.Add(new SqlParameter("@ActionSourceID", 8));
            command.Parameters.Add(new SqlParameter("@DataChangeStageID", 0));

            command.Parameters.Add(new SqlParameter("@MappedDeviceGroup",
                (object?)emp.MappedDeviceGroup ?? DBNull.Value));

            command.Parameters.Add(new SqlParameter("@FaceImageUpdated", false));

            command.Parameters.Add(new SqlParameter("@Flag", "I")); // I=Insert/U=Update as per SP

            command.Parameters.Add(new SqlParameter("@LoggedUser", 99999));

            await command.ExecuteNonQueryAsync();

            await GlobalLogger.LogToFileAsync(
                $"Employee {emp.EmployeeCode} synchronized successfully.");
        }
        catch (Exception ex)
        {
            await GlobalLogger.LogToFileAsync(ex.ToString());
            throw;
        }
    }
    public async Task AddUpdateDevlog(PunchLogEntity punch)
        {

            await _easyAccessDbContext.Database.ExecuteSqlRawAsync(
 @"
EXEC [Punch].[uspInsertDeviceRawPunch]
    @DeviceCode,
    @DeviceUserCode,
    @PunchTime,
    @PunchTemperature,
    @PunchFlag,
    @LogImage,
    @VerifyMode
",
 new SqlParameter("@DeviceCode", punch.DeviceSerialNumber),
 new SqlParameter("@DeviceUserCode", punch.DeviceEmployeeCode),
 new SqlParameter("@PunchTime", punch.PunchDateTime.ToString("yyyy-MM-dd HH:mm:ss")),
 new SqlParameter("@PunchTemperature", DBNull.Value), // or punch.Temperature
 new SqlParameter("@PunchFlag", "I"),                 // or punch.InOut
 new SqlParameter("@LogImage", DBNull.Value),
 new SqlParameter("@VerifyMode", "1")
 );

        }
        public async Task AddUpdateDevMaster(DeviceMasterEntity dev)
        {
            
           


           
                    var parameters = new[]
                    {
        new SqlParameter("@DeviceID", 0),

        new SqlParameter("@DeviceCode",
            (object?)dev.DeviceSerialNumber ?? DBNull.Value),

        new SqlParameter("@DeviceName",
            (object?)dev.DeviceName ?? DBNull.Value),

        new SqlParameter("@DeviceInfo",
            (object?)dev.DeviceInfo ?? DBNull.Value),

        new SqlParameter("@DeviceType",
            (object?)dev.DeviceType ?? "A"),

        new SqlParameter("@DeviceModel",
            (object?)dev.DeviceModelId ?? "ABCD"),

        new SqlParameter("@InOut",
            (object?)dev.InoutFlag ?? "I"),

        new SqlParameter("@DeviceModelID",
            dev.DeviceModelId ?? 1),

        new SqlParameter("@LastConnected",
            (object?)dev.LastConnected ?? DBNull.Value),

        new SqlParameter("@DeviceIP",
            (object?)dev.DeviceIp ?? DBNull.Value),

        new SqlParameter("@DevicePort",
            (object?)dev.DevicePort ?? DBNull.Value),

        new SqlParameter("@XMLDeviceStatus",
            DBNull.Value), // not using XML here

        new SqlParameter("@Connected",
            dev.IsConnected == "1"),

        new SqlParameter("@LoggedUser",
            dev.CreatedBy)
    };

                    await _easyAccessDbContext.Database.ExecuteSqlRawAsync(
                        @"EXEC [Bio].[uspGetDeviceStatus]
            @DeviceID,
            @DeviceCode,
            @DeviceName,
            @DeviceInfo,
            @DeviceType,
            @DeviceModel,
            @InOut,
            @DeviceModelID,
            @LastConnected,
            @DeviceIP,
            @DevicePort,
            @XMLDeviceStatus,
            @Connected,
            @LoggedUser",
                        parameters
                    );
                
            

                dev.IsRegistered = "0";
                dev.DeviceType = "A";
                dev.InoutFlag = "I";
                dev.IsActive = "1";
                dev.IsConnected = "1";
                dev.LastConnected = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                dev.CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                     parameters = new[]
   {
        new SqlParameter("@DeviceID",  0),
        new SqlParameter("@DeviceCode", dev.DeviceSerialNumber ?? (object)DBNull.Value),
        new SqlParameter("@DeviceName", dev.DeviceName ?? (object)DBNull.Value),
        new SqlParameter("@DeviceIP", dev.DeviceIp ?? (object)DBNull.Value),
        new SqlParameter("@DeviceType", dev.DeviceType ?? "A"),
        new SqlParameter("@DeviceModel", dev.DeviceModelId ?? (object)DBNull.Value),
        new SqlParameter("@InOut", dev.InoutFlag ?? "I"),
        new SqlParameter("@LastConnected", dev.LastConnected ?? (object)DBNull.Value),
        new SqlParameter("@DeviceModelID", dev.DeviceModelId ?? 1),
        new SqlParameter("@Connected", dev.IsConnected == "1"),
        new SqlParameter("@DeviceKey", dev.DeviceKey ?? (object)DBNull.Value),
        new SqlParameter("@DevicePassword",  (object)DBNull.Value),
        new SqlParameter("@DeviceUserID",  (object)DBNull.Value),
        new SqlParameter("@ProtocolType", dev.ProtocolType ?? (object)DBNull.Value),
        new SqlParameter("@DevicePort", dev.DevicePort ?? (object)DBNull.Value),
        new SqlParameter("@Registered", dev.IsRegistered == "1"),



        new SqlParameter("@DeviceInfo", dev.DeviceInfo ?? (object)DBNull.Value),
        new SqlParameter("@LastDownloadTime", dev.LastDownloadTime ?? (object)DBNull.Value),
        new SqlParameter("@DeviceGroupID", dev.DeviceGroupId ?? (object)DBNull.Value),

        new SqlParameter("@EnrollmentDevice", dev.EnrollmentDevice),
        new SqlParameter("@DeviceUsers", dev.DeviceUser ?? (object)DBNull.Value),
        new SqlParameter("@DeviceFaces", dev.FaceUser ?? (object)DBNull.Value),
        new SqlParameter("@DeviceFingers", dev.FingerUser ?? (object)DBNull.Value),
        new SqlParameter("@DeviceCards", dev.CardUser ?? (object)DBNull.Value),
        new SqlParameter("@UserLogs",  (object)DBNull.Value),

        new SqlParameter("@firmware", dev.DeviceInfo ?? (object)DBNull.Value),
        new SqlParameter("@BranchID", (object)DBNull.Value),

        new SqlParameter("@LoggedUser", dev.CreatedBy)
    };

                    await _easyAccessDbContext.Database.ExecuteSqlRawAsync(
                       "EXEC [Bio].[uspInsertDeviceMaster] " +
                       "@DeviceID, @DeviceCode, @DeviceName, @DeviceIP, @DeviceType, @DeviceModel, @InOut, " +
                       "@LastConnected, @DeviceModelID, @Connected, @DeviceKey, @DevicePassword, @DeviceUserID, " +
                       "@ProtocolType, @DevicePort, @Registered, @CompanyList, @SubDepartmentList, @SubBranchList, " +
                       "@UpdateMapping, @DeviceInfo, @LastDownloadTime, @DeviceGroupID, @EnrollmentDevice, " +
                       "@DeviceUsers, @DeviceFaces, @DeviceFingers, @DeviceCards, @UserLogs, @firmware, " +
                       "@BranchID, @LoggedUser",
                       parameters
                   );
               
        }

        public async Task AddUpdateDevCommand(DeviceCommandEntity Cmd)
        {

            // Process command responses if present
            
            
                Cmd.CommondStatusId = (Cmd.CommondResponce.Equals("True", StringComparison.OrdinalIgnoreCase)) ? 1 : -1;
                Cmd.CommondResponce = (Cmd.CommondResponce.Equals("True", StringComparison.OrdinalIgnoreCase)) ? "Success" : $"Failed ({Cmd.DeviceCommand})";
               Cmd.UpdatedDate = DateTime.Now.ToString();
                await _easyAccessDbContext.Database.ExecuteSqlRawAsync(
        @"EXEC [Bio].[uspUpdateDeviceCommand]
            @DeviceCommandID,
            @DeviceCommandEmployeeID,
            @DeviceCode,
            @CommondStatusID,
            @CommondResponce,
            @LastExecutionTime,
            @CardNumber,
            @DeviceCommand,
            @LoggedUser",
        
        new SqlParameter("@CommondStatusID", Cmd.CommondStatusId),
        new SqlParameter("@CommondResponce", Cmd.CommondResponce),
        
        new SqlParameter("@LoggedUser", Cmd.CreatedBy)
    );
           
            
        }

        public async Task<List<DeviceCommandEntity>> CheckCommand()
        {
            var command = await _easyAccessDbContext.DeviceCommands
        .FromSqlRaw(
            "EXEC [Bio].[uspGetDeviceCommand] @ActionSourceID",
            new SqlParameter("@ActionSourceID", 8))
        .ToListAsync();

            List<DeviceCommandEntity> cmds = Mapper.ToEntityList(command);
                return cmds;
            }

        public async Task<DeviceCommandEntity> GetCommandFromDatabase(long id)
        {
            var command = await _easyAccessDbContext.DeviceCommands
        .FromSqlRaw(
            "EXEC [Bio].[uspGetDeviceCommand] @DeviceCommandID, @ActionSourceID",
            new SqlParameter("@DeviceCommandID", id),
            new SqlParameter("@ActionSourceID", 0))
        .ToListAsync();

            DeviceCommandEntity devicecmd = Mapper.ToEntity(command.FirstOrDefault());
            
            return devicecmd;
        }

        public async Task<EmployeeEntity?> GetEmpFromDatabase(DeviceCommandEntity cmd)
        {
            var result = await _easyAccessDbContext.EmployeeMasters
                .FromSqlRaw(
                    @"EXEC Bio.uspGetDeviceEmployee
                @EmployeeCode,
                @ActionSourceID,
                @LoggedUser",
                    new SqlParameter("@EmployeeCode",
                        (object?)cmd.CardSnrNumber ?? DBNull.Value),
                    new SqlParameter("@ActionSourceID", 101),
                    new SqlParameter("@LoggedUser", 99999))
                .ToListAsync();

            return result.FirstOrDefault()?.ToEntity();
        }



        public async Task<DeviceMasterEntity> GetDevSnFromDatabase(string? sn)
        {
            try
            {
                var device = await _easyAccessDbContext.DeviceMasters
        .FromSqlRaw(
            @"EXEC Bio.uspGetDeviceMaster
                @DeviceCode,
                @ActionSourceID,
                @Flag",
            new SqlParameter("@DeviceCode", sn),
            new SqlParameter("@ActionSourceID", 8),
            new SqlParameter("@Flag", "L"))
        .ToListAsync();

                if(device.Any())
                {
                    return Mapper.ToEntity(device.First());
                }
                return null;
            }
            catch (Exception er)
            {
                await GlobalLogger.LogToFileAsync("<<<<Errror>>>>"+er);
                throw;
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

        public async Task AddUpdateDevSendCommand(DeviceCommandEntity cmd)
        {
            await _easyAccessDbContext.Database.ExecuteSqlRawAsync(
                @"EXEC Bio.uspUpdateDeviceCommand
            @DeviceCommandID,
            @DeviceCommandEmployeeID,
            @DeviceCode,
            @CommondStatusID,
            @CommondResponce,
            @LastExecutionTime,
            @CardNumber,
            @DeviceCommand,
            @LoggedUser",

                new SqlParameter("@DeviceCommandID", cmd.DeviceCommandId),

                new SqlParameter("@DeviceCommandEmployeeID", 0),

                new SqlParameter("@DeviceCode",
                    (object?)cmd.DeviceSerialNumber ?? DBNull.Value),

                new SqlParameter("@CommondStatusID",
                    (object?)cmd.CommondStatusId ?? DBNull.Value),

                new SqlParameter("@CommondResponce",
                    (object?)cmd.CommondResponce ?? DBNull.Value),

                // The procedure itself sets LastExecutionTime = dbo.fn_GetDate(),
                // so this can safely be NULL.
                new SqlParameter("@LastExecutionTime", DBNull.Value),

                new SqlParameter("@CardNumber",
                    (object?)cmd.CardSnrNumber ?? DBNull.Value),

                new SqlParameter("@DeviceCommand",
                    (object?)cmd.DeviceCommand ?? DBNull.Value),

                new SqlParameter("@LoggedUser", 3003)
            );
        }
    }




}

//namespace DeviceServices
//{
//    internal static class DataTableExtensions
//    {
//        internal static List<T> ToList<T>(this DataTable table) where T : new()
//        {
//            if (table == null || table.Rows.Count == 0)
//                return new List<T>();

//            var columnNames = new HashSet<string>(
//                table.Columns.Cast<DataColumn>().Select(c => c.ColumnName),
//                StringComparer.OrdinalIgnoreCase
//            );

//            var type = typeof(T);
//            var properties = DeviceServices.Implementation.DeviceDatabaseService
//                .PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

//            var list = new List<T>(table.Rows.Count);

//            foreach (DataRow row in table.Rows)
//            {
//                var obj = new T();

//                foreach (var prop in properties)
//                {
//                    if (!columnNames.Contains(prop.Name))
//                        continue;

//                    var value = row[prop.Name];
//                    if (value == DBNull.Value)
//                        continue;

//                    try
//                    {
//                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType)
//                                         ?? prop.PropertyType;

//                        var safeValue = Convert.ChangeType(value, targetType);
//                        prop.SetValue(obj, safeValue);
//                    }
//                    catch (Exception ex)
//                    {
//                        // Use the same logger call as original code; keep it asynchronous fire-and-forget
//                        _ = GlobalLogger.LogToFileAsync(
//                            ex +
//                            "12" +
//                            $"(DataTableExtensions.ToList) Property: {prop.Name}"
//                        );
//                    }
//                }

//                list.Add(obj);
//            }

//            return list;
//        }
//    }
//}

