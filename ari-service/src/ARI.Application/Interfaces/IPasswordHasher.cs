namespace ARI.Application.Interfaces
{
    /// <summary>Hash/verify mật khẩu — che BCrypt khỏi Application/API layer.</summary>
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }
}
