using ARI.Application.Common.Security;
using Xunit;

namespace ARI.Application.UnitTests.Common;

/// <summary>
/// Hai format hash CÙNG tồn tại trong DB: Base64 (refresh_tokens.token_hash) và
/// HEX uppercase (interview_invites.token_hash). Test chốt cứng format để refactor
/// sau này không vô tình đổi — đổi là mất hiệu lực toàn bộ token đã phát hành.
/// </summary>
public class TokenHashingTests
{
    [Fact]
    public void Sha256Base64_matches_known_vector()
    {
        // SHA256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        Assert.Equal("ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=", TokenHashing.Sha256Base64("abc"));
    }

    [Fact]
    public void Sha256Hex_matches_known_vector_and_is_uppercase()
    {
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", TokenHashing.Sha256Hex("abc"));
    }

    [Fact]
    public void Formats_differ_for_same_input()
    {
        Assert.NotEqual(TokenHashing.Sha256Base64("token"), TokenHashing.Sha256Hex("token"));
    }
}
