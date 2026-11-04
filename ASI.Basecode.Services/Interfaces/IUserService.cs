using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IUserService
    {
        User AuthenticateUser(string email, string password);
        bool IsCurrentAccountEligible(int userId, string role);
    }
}
