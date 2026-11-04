using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        public UserService(IUserRepository repository) => _repository = repository;

        public User AuthenticateUser(string email, string password)
        {
            var normalizedEmail = email?.Trim().ToLowerInvariant();
            var user = _repository.GetUsers().FirstOrDefault(u => u.Email == normalizedEmail && u.IsActive);
            return user != null && PasswordManager.VerifyPassword(password, user.PasswordHash) ? user : null;
        }

        public bool IsCurrentAccountEligible(int userId, string role)
        {
            return _repository.GetUsers().Any(u => u.UserId == userId && u.IsActive && u.Role == role);
        }

    }
}
