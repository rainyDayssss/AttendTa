using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class EventTypeViewModel
    {
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type name is required.")]
        [StringLength(100, ErrorMessage = "Type name cannot exceed 100 characters.")]
        public string TypeName { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
