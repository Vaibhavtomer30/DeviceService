using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public class tblEmployeeMaster
    {
        public int EmployeeID { get; set; }

        public string? EmployeeCode { get; set; }

        public string? CardNumber { get; set; }

        public string? EmployeeName { get; set; }

        public int? UserAuthTypeID { get; set; }

        public string? DeviceCode { get; set; }

        public string? UserType { get; set; }

        public DateTime? ValidityStart { get; set; }

        public DateTime? ValidityEnd { get; set; }

        public int? MaxOpenDoorTime { get; set; }

        public string? FaceImageURL { get; set; }

        public string? FaceImagePath { get; set; }

        public string? ProfileImagePath { get; set; }

        public string? AuthTypeDataBase64 { get; set; }

        public bool? Registerd { get; set; }

        public string? FaceDownloadFromDevice { get; set; }
    }
}