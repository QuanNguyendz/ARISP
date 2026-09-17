using ARI.Domain.Constants;
using Xunit;

namespace ARI.Domain.UnitTests.Constants;

/// <summary>
/// <see cref="SalaryCurrencies"/> — mọi con số lương chỉ mang VND hoặc USD, lưu dạng viết hoa.
/// Bộ lọc lương của Job Board so khớp đúng chuỗi "USD" để quy đổi, nên "usd" lọt xuống DB là lọc sai.
/// </summary>
public class SalaryCurrenciesTests
{
    [Theory]
    [InlineData("VND")]
    [InlineData("USD")]
    [InlineData("usd")]
    [InlineData("  Vnd ")]
    [InlineData(null)]  // trống: nơi gọi tự dùng mặc định
    [InlineData("")]
    [InlineData("   ")]
    public void IsAllowed_true_for_vnd_usd_or_blank(string? value)
        => Assert.True(SalaryCurrencies.IsAllowed(value));

    [Theory]
    [InlineData("EUR")]
    [InlineData("VNĐ")]
    [InlineData("$")]
    [InlineData("VND USD")]
    public void IsAllowed_false_for_anything_else(string value)
        => Assert.False(SalaryCurrencies.IsAllowed(value));

    [Theory]
    [InlineData(" usd ", "USD")]
    [InlineData("vnd", "VND")]
    [InlineData(null, "VND")]
    [InlineData("", "VND")]
    [InlineData("EUR", "VND")]
    public void Normalize_returns_upper_case_or_default(string? value, string expected)
        => Assert.Equal(expected, SalaryCurrencies.Normalize(value));
}
