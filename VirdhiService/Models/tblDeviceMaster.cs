using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public class tblDeviceMaster
    {
        public string? DeviceCode { get; set; }

        public string? DeviceName { get; set; }

        public string? DeviceType { get; set; }

        public int? DeviceModelID { get; set; }

        public bool? Connected { get; set; }

        public string? DevicePassword { get; set; }

        public string? DeviceUserID { get; set; }

        public string? DeviceKey { get; set; }

        public bool? Registered { get; set; }

        public string? ProtocolType { get; set; }

        public string? EnrollmentDevice { get; set; }
    }
}
