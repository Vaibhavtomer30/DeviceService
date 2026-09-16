using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceServices.Models
{
    [Table(name: "TblEmployee")]
    public class EmployeeEntity :BaseEntity
    {
        [Key]
        public int EmployeeId { get; set; }
        [Required]
        [Column (TypeName ="VARCHAR(20)")]
        public string EmployeeCode { get; set; } = null!;
        [Required]
        [Column(TypeName = "VARCHAR(20)")]
        public string DeviceEmployeeCode { get; set; } = null!;
        [Required]
        [Column(TypeName = "VARCHAR(50)")]
        public string? EmployeeName { get; set; }
        [Required]
        [Column(TypeName = "INT")]
        public int DepartmentId { get; set; }
        [Required]
        [Column(TypeName = "INT")]
        public int BranchId { get; set; }
        [Required]
        [Column(TypeName = "INT")]
        public int DesignationId { get; set; }
        [Required]
        [Column(TypeName = "INT")]
        public int CategoryId { get; set; }
        [Required]
        [Column(TypeName = "INT")]
        public int GenderId { get; set; }

        public int? EnrollmentDeviceId { get; set; }
        
        [Column(TypeName = "VARCHAR(50)")]
        public string? MappedDeviceGroup { get; set; }
        public List<int> deviceGroupMappedArr { get; set; } = new List<int>();
        [Column(TypeName = "INT")]
        public int EmployeeTypeId { get; set; }
        [Column(TypeName = "INT")]
        public int UserauthTypeId { get; set; }
        [Column(TypeName = "INT")]
        public int AccessTypeId { get; set; }
        
        [Column(TypeName = "VARCHAR(23)")]
        public string? ValidityStart { get; set; }
        
        [Column(TypeName = "VARCHAR(23)")]
        public string? ValidityEnd { get; set; }
        [Column(TypeName = "INT")]
        public int MaxAccessCount { get; set; }
        [Column(TypeName = "VARCHAR(200)")]
        public string? ProfileImagePath { get; set; }
        [Column(TypeName = "VARCHAR(200)")]
        public string? FaceImagePath { get; set; }
        [Column(TypeName = "CHAR(1)")]
        public string? FaceDownloadFromDevice { get; set; }
        [Column(TypeName = "INT")]
        public int? FaceUpdateSourceId { get; set; }
        [Column(TypeName = "INT")]
        public int IsRegisterd { get; set; }
        [Column(TypeName = "INT")]
        public int IsActive { get; set; }
        [Column(TypeName = "INT")]
        public int IsDeleted { get; set; }

        public int UpdateDeviceSetting { get; set; }
        //public ICollection<DdlMaster>? Branchs { get; set; }
        //public ICollection<DdlMaster>? Departments { get; set; }
        //public ICollection<DdlMaster>? Designations { get; set; }
        //public ICollection<DdlMaster>? Categories { get; set; }
        ///public ICollection<DdlMaster>? Genders { get; set; }
        //public ICollection<DdlMaster>? EmployeeTypes { get; set; }
        //public ICollection<DdlMaster>? UserAuthTypes { get; set; }
        //public ICollection<DdlMaster>? AccessTypes { get; set; }
        //public ICollection<DdlMaster>? deviceGroup { get; set; }
        //public ICollection<DdlMaster>? DeviceMasters { get; set; }
        //public EmployeeBasicEntity? EmployeeBasicEntity { get; set; }
        public string? Companyname { get; set; }
        public string? JoiningDate { get; set; }
        public string? FaceImagePathDisplay { get; set; }
       // public List<EmployeeSalaryStructureEntity>?  SalaryStructure {  get; set; }
        public DateTime? LeavingDate { get; set; }
        public string? LeavingReason { get; set; }
        public decimal? Ctc { get; set; }
        public char? CtcType { get; set; }
        public decimal? GrossSalary { get; set; }
        public string? PfApplicable { get; set; }
        public string? VpfApplicable { get; set; }
        public string? EsiApplicable { get; set; }
        public string? LwfApplicable { get; set; }
        public string? PtApplicable { get; set; }
        public decimal? PfGrossLimit { get; set; }
        public string? EmployeeSalaryType { get; set; }
        public string? UanNumber { get; set; }
        public string? EsiNumber { get; set; }
        public string? PaymentType { get; set; }
        public string? PanNumber { get; set; }
        public string? BankAccNumber { get; set; }
        public string? Ifsc { get; set; }
         public int Flag { get; set; }

       // public EmployeePayrollComponent PayrollComponent { get; set; } = null!;
        public string? CardNo { get; set; }
        public string? EmpBase64Img { get; set; }
        public string? DevSerialNo { get; set; }
        public int IsDepartmentAdmin { get; set; }
        public string? DepartmentAdminList { get; set; } 
        public string? Password { get; set; }
        }
}
