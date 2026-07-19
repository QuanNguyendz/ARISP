using System;
using System.Security.Cryptography;
using System.Text;

namespace ARI.Application.Common.Security
{
    /// <summary>Hash token bằng SHA256 để lưu an toàn trong DB (refresh token, invite token...).</summary>
    public static class TokenHashing
    {
        public static string Sha256Base64(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}
