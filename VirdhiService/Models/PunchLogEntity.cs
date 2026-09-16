using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public class PunchLogEntity: BaseEntity
    {
        public long PunchlogId { get; set; }
        public string DeviceEmployeeCode { get; set; } = null!;
        public string? DeviceSerialNumber { get; set; }
        public string? PunchDate { get; set; }
        public string? PunchTime { get; set; }
        public string? Reason { get; set; }
        public string IsAuthorized { get; set; } = null!;
        public string PunchInoutFlag { get; set; } = null!;
        public string DeviceInoutFlag { get; set; } = null!;
        public DateTime PunchDateTime { get; set; }
        public string IsManual { get; set; } = null!;
        public string IsDeleted { get; set; } = null!;
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? DepartmentName { get; set; }
        public string? DesignationNmae { get; set; }
        public string? BranchName { get; set; }
        public string? DeviceGroup { get; set; }
        public string? DeviceName { get; set; }
        public string? Timing { get; set; }

        public string? logimagepath { get; set; }
        public string? logImagePathDisplay { get; set; }

        public DateTime SortTime { get; set; }

        public string? ActualCardNumber { get; set; }
        public string? PhtBase64Img {  get; set; }

    }
}
