using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Cấu hình gốc URL hai cổng frontend cho test.
///
/// Dùng địa chỉ KHÔNG PHẢI localhost là cố ý: một lá thư còn ghi cứng <c>localhost:3001</c> vẫn
/// chạy qua test nếu test cũng dùng localhost, mà đó đúng là lỗi cần bắt — trên production nút bấm
/// trong thư dẫn về máy người nhận.
/// </summary>
internal static class TestConfig
{
    public const string StaffBaseUrl = "https://staff.arisp.test";
    public const string CandidateBaseUrl = "https://arisp.test";

    public static IConfiguration Frontend() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ARI.Application.Common.FrontendUrls.StaffKey] = StaffBaseUrl,
                [ARI.Application.Common.FrontendUrls.CandidateKey] = CandidateBaseUrl,
            })
            .Build();
}
