using System;

namespace ASI.Basecode.Data.Models
{
    public partial class EventType
    {
        public int TypeId { get; set; }

        public string TypeName { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
