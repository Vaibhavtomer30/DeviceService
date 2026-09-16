namespace DeviceServices.Models
{
    public class DeviceMasterEntity : BaseEntity
    {
        

        public int DeviceId { get; set; }

        public string DeviceSerialNumber { get; set; } = null!;

        public string DeviceName { get; set; } = null!;

        public int? DeviceModelId { get; set; }

        public string DeviceType { get; set; } = null!;

        public string InoutFlag { get; set; } = null!;

        public string? IsConnected { get; set; }

        public string? LastConnected { get; set; }

        public string? DeviceInfo { get; set; }

        public string? DeviceIp { get; set; }

        public string? DevicePort { get; set; }

        public string CompanyId { get; set; } = null!;

        public string? DeviceKey { get; set; }

        public string? ProtocolType { get; set; }

        public string? IsRegistered { get; set; }

        public string? LastDownloadTime { get; set; }

        public int? DeviceGroupId { get; set; }

        public string? EnrollmentDevice { get; set; }

        public string? IsActive { get; set; }
        //public ICollection<DdlMaster> DeviceModels { get; set; }
        //public ICollection<DdlMaster> DeviceGroups { get; set; }
        public string? MacAddress { get; set; } 
        public string? DeviceUser { get; set; }
        public  string? FaceUser { get; set; }
        public string? FingerUser { get; set; }
        public string? CardUser { get; set; }
        public string? DeviceGroupName { get; set; }
        public string? TotalUser {  get; set; }
        public string? TotalFaceUser { get; set; }
        public string? TotalCardUser { get; set; }
        public string? clientIp { get; set; }
        public int clientPort { get; set; }
        public string? TotalFingerUser { get; set; }
        public string? UsedUser { get; set; }
        public string? UsedFaceUser { get; set; }
        public string? UsedCardUser { get; set; }
        public string? UsedFingerUser { get; set; }
    }
}
