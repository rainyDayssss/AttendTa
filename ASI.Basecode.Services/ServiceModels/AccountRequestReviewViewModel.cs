using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels
{
    public class AccountRequestReviewViewModel
    {
        public int RequestId { get; set; }
        [StringLength(1000)] public string ReviewNotes { get; set; }
    }
}
