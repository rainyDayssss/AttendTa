using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class EventTypeViewModel
    {
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type name is required.")]
        public string TypeName { get; set; }

        public string Description { get; set; }
    }
}
