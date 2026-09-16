using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public static class Mapper
    {
        public static DeviceCommandEntity ToEntity(this tblDeviceCommand source)
        {
            if (source == null)
                return null!;

            return new DeviceCommandEntity
            {
                DeviceCommandId = source.DeviceCommandID,
                DeviceSerialNumber = source.DeviceCode,
                DeviceCommand = source.DeviceCommand,
                CommondStatusId = source.CommondStatusID,
                CommondResponce = source.CommondResponce,
                Priority = source.Priority,
                UserType = source.UserType,
                EmployeeCode = source.EmployeeCode,
                EmployeeName = source.EmployeeName,
                UserAuthType = source.UserAuthTypeID,
                FromDate = source.FromDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                ToDate = source.ToDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                MaxOpenDoorTime = source.MaxOpenDoorTime,
                FaceImagePath = source.FaceImagePath,
                AuthtypeDataBase64 = source.AuthTypeDataBase64,
                CardSnrNumber = source.CardSNRNumber,
                Extra = source.Extra
            };
        }

        public static List<DeviceCommandEntity> ToEntityList(this IEnumerable<tblDeviceCommand> source)
        {
            return source.Select(x => x.ToEntity()).ToList();
        }

        public static DeviceMasterEntity ToEntity(this tblDeviceMaster source)
        {
            return new DeviceMasterEntity
            {
                DeviceSerialNumber = source.DeviceCode ?? string.Empty,
                DeviceName = source.DeviceName ?? string.Empty,
                DeviceModelId = source.DeviceModelID,
                DeviceType = source.DeviceType ?? string.Empty,
                IsConnected = source.Connected?.ToString(),
                DeviceKey = source.DeviceKey,
                ProtocolType = source.ProtocolType,
                IsRegistered = source.Registered?.ToString(),
                EnrollmentDevice = source.EnrollmentDevice,
                DeviceUser = source.DeviceUserID
            };
        }

        public static List<DeviceMasterEntity> ToEntityList(this IEnumerable<tblDeviceMaster> source)
        {
            return source.Select(x => x.ToEntity()).ToList();
        }

        public static EmployeeEntity ToEntity(this tblEmployeeMaster source)
        {
            if (source == null)
                return null!;

            return new EmployeeEntity
            {
                EmployeeId = source.EmployeeID,
                EmployeeCode = source.EmployeeCode ?? string.Empty,
                DeviceEmployeeCode = source.CardNumber ?? string.Empty,
                CardNo = source.CardNumber,
                EmployeeName = source.EmployeeName,

                UserauthTypeId = source.UserAuthTypeID ?? 0,
                DevSerialNo = source.DeviceCode,

                ValidityStart = source.ValidityStart?.ToString("yyyy-MM-dd HH:mm:ss"),
                ValidityEnd = source.ValidityEnd?.ToString("yyyy-MM-dd HH:mm:ss"),

                MaxAccessCount = source.MaxOpenDoorTime ?? 0,

                FaceImagePath = source.FaceImagePath,
                FaceImagePathDisplay = source.FaceImageURL,
                ProfileImagePath = source.ProfileImagePath,

                EmpBase64Img = source.AuthTypeDataBase64,

                FaceDownloadFromDevice = source.FaceDownloadFromDevice,

                IsRegisterd = source.Registerd == true ? 1 : 0
            };
        }

        public static List<EmployeeEntity> ToEntityList(this IEnumerable<tblEmployeeMaster> source)
        {
            return source.Select(x => x.ToEntity()).ToList();
        }
    }
}
