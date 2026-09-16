using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public class tblDeviceCommand
    {
        public long DeviceCommandID { get; set; }

        public string? DeviceCode { get; set; }

        public string? DeviceCommand { get; set; }

        public int? CommondStatusID { get; set; }

        public string? CommondResponce { get; set; }

        public int? Priority { get; set; }

        public string? CardNumber { get; set; }

        public string? UserType { get; set; }

        public string? EmployeeCode { get; set; }

        public string? EmployeeName { get; set; }

        public int? UserAuthTypeID { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int? MaxOpenDoorTime { get; set; }

        public string? FaceImagePath { get; set; }

        public string? AuthTypeDataBase64 { get; set; }

        public string? CardSNRNumber { get; set; }

        public string? Extra { get; set; }
    }
}
