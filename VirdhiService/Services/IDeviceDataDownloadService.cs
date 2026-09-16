using DeviceServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Services
{
    public interface IDeviceDataDownloadService
    {
        Task<DeviceCommandEntity> GetDeviceCommand(long CMD);
        Task<EmployeeEntity> GetEmployee(DeviceCommandEntity CMD);
        Task UpdateCommandAsync(DeviceCommandEntity Cmd);
        Task HandleDeviceStatus(DeviceMasterEntity dev, string clientIp=" ", int clientPort=0);
        Task<List<DeviceCommandEntity>> CheckCommandZkt(string? DeviceSerialNo);
        Task SetDeviceInfo(DeviceMasterEntity dev);
        Task SetAttendance(PunchLogEntity record);
        Task SetUserInfo(EmployeeEntity emp);
        Task<DeviceMasterEntity> GetDeviceSn(string sn);
        Task SendCommand();
        Task UpdateSendingCommandAsync(DeviceCommandEntity Cmd);
    }
}
