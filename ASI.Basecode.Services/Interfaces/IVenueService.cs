using ASI.Basecode.Services.ServiceModels;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IVenueService
    {
        Task<PagedResult<VenueViewModel>> GetPageAsync(string status, int? pageNumber, CancellationToken cancellationToken = default);
        VenueViewModel GetVenue(int id);
        Task AddVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default);
        Task UpdateVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default);
        Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
    }
}
