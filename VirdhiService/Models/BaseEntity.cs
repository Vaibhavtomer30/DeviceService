using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceServices.Models
{
    public class BaseEntity
    {
        public string? CompanyId { get; set; }
        public int? CreatedBy { get; set; }

        public string? CreatedDate { get; set; }

        public int? UpdatedBy { get; set; }

        public string? UpdatedDate { get; set; }
    }
    public class MasterBaseEntity : BaseEntity
    {
        public string? IsDefault { get; set; }

        public int ParentId { get; set; }
    }
}
