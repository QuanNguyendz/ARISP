using ARI.Application.Applications;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Vòng hiện tại của hồ sơ (<see cref="ApplicationCurrentRound"/>) — quyết định hồ sơ nằm ở cột
/// "Vòng N" nào và vòng nào được xếp lịch tiếp.
///
/// Lỗi mà bộ test này khoá lại: vòng TRẮC NGHIỆM không có bước HM chốt kết quả nên không bao giờ sinh
/// lời mời vòng kế; Recruiter xếp thẳng lịch vòng 2, nhưng vì phép tính chỉ đọc lời mời + phiên phỏng
/// vấn nên hồ sơ đã có lịch vòng 2 vẫn nằm ở cột "Vòng 1".
/// </summary>
public class ApplicationCurrentRoundTests
{
    [Theory]
    [InlineData(ApplicationStatuses.CvSubmitted)]
    [InlineData(ApplicationStatuses.Invited)]
    [InlineData(ApplicationStatuses.CvRejected)]
    public void Chua_qua_CV_thi_chua_o_vong_nao(string status)
    {
        Assert.Null(ApplicationCurrentRound.Resolve(status, 2, 2, 2));
    }

    [Fact]
    public void Cho_xep_lich_la_vong_1()
    {
        Assert.Equal(1, ApplicationCurrentRound.Resolve(ApplicationStatuses.Screening, 0, 0, 0));
    }

    [Fact]
    public void Lich_dang_giu_cho_vong_ke_dua_ho_so_sang_vong_do()
    {
        // Lời mời vòng 1, chưa có phiên nào (vòng trắc nghiệm không có phiên), lịch vòng 2 đã xếp.
        Assert.Equal(2, ApplicationCurrentRound.Resolve(ApplicationStatuses.Interview, 1, 0, 2));
    }

    [Fact]
    public void Lay_nguon_lon_nhat_trong_ba()
    {
        // HM chốt đạt vòng 2 → lời mời vòng 3, trong khi lịch còn giữ chỗ chỉ tới vòng 2.
        Assert.Equal(3, ApplicationCurrentRound.Resolve(ApplicationStatuses.Interview, 3, 2, 2));
        Assert.Equal(2, ApplicationCurrentRound.Resolve(ApplicationStatuses.Interview, 1, 2, 0));
    }

    [Fact]
    public void Khong_co_du_lieu_nao_thi_mac_dinh_vong_1()
    {
        Assert.Equal(1, ApplicationCurrentRound.Resolve(ApplicationStatuses.Interview, 0, 0, 0));
    }
}
