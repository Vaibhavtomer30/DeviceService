using DeviceServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Services
{
    public interface IDeviceDatabaseService
    {
        Task AddUpdateDevEmp(EmployeeEntity emp);
        Task AddUpdateDevlog(PunchLogEntity punch);
        Task AddUpdateDevMaster(DeviceMasterEntity dev);
        Task AddUpdateDevCommand(DeviceCommandEntity Cmd);
        Task<List<DeviceCommandEntity>> CheckCommand();
        Task<DeviceCommandEntity> GetCommandFromDatabase(long id);
        Task<EmployeeEntity> GetEmpFromDatabase(DeviceCommandEntity cmd);
        Task<DeviceMasterEntity> GetDevSnFromDatabase(string? sn);
        Task AddUpdateDevSendCommand(DeviceCommandEntity cmd);


    }
}
