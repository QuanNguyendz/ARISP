using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Identity
{
    /// <summary>BCrypt (work factor mặc định) — thay các call BCrypt.Net trực tiếp trong controllers cũ.</summary>
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
