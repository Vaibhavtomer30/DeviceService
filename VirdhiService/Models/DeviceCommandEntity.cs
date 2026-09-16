namespace DeviceServices.Models
{
    public class DeviceCommandEntity:BaseEntity
    {
        public long DeviceCommandId { get; set; }

        public string? DeviceSerialNumber { get; set; }

        public string? DeviceCommand { get; set; }

        public int? CommondStatusId { get; set; }

        //public string? Detail { get; set; }

        public string? CommondResponce { get; set; }

        //public string? StartTime { get; set; }

        //public string? EndTime { get; set; }

        public string? LastExecutionTime { get; set; }

        //public string? Extra1 { get; set; }

        //public string? Extra2 { get; set; }

        //public string? Extra3 { get; set; }

        //public string? Extra4 { get; set; }


        public string? EmployeeCode { get; set; }
        public string? DeviceUserId { get; set; }
        public int? UserAuthType { get; set; }
        public int? RetryCount { get; set; }
        public string? UserType { get; set; } = "E";
        public int? Priority { get; set; }
        public string? EmployeeName { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? MaxOpenDoorTime { get; set; }
        public string? FaceImagePath { get; set; }
        public string? AuthtypeDataBase64 { get; set; }
        public string? CardSnrNumber { get; set; }
        public string? Extra { get; set; }
        public DateTime? SentDate { get; set; }
        public string? FaceImagePathDisplay { get; set; }
        public int? DeviceModelId { get; set; }
        public string IsConnected { get; set; }
    }
}