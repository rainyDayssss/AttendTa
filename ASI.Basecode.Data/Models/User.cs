using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models
{
    public partial class User
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public RoleCode RoleId { get; set; }
        public bool IsActive { get; set; }
        public int? CreatedById { get; set; }
        public int? UpdatedById { get; set; }
    }
}
