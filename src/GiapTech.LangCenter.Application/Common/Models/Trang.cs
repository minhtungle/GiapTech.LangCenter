namespace GiapTech.LangCenter.Application.Common.Models;

/// <summary>
/// Tham số phân trang cho mọi danh sách.
///
/// Phân trang ở SERVER chứ không cắt ở trình duyệt: bảng dữ liệu của một trung tâm hoạt
/// động vài năm sẽ lên hàng nghìn dòng, tải hết về rồi mới cắt sẽ chậm dần và không ai để ý
/// cho tới khi quá muộn.
/// </summary>
public record ThamSoTrang(int Trang = 1, int SoDong = 20)
{
    /// <summary>Chặn trên để một request lỗi không kéo cả bảng về.</summary>
    public const int SoDongToiDa = 200;

    public int TrangHopLe => Trang < 1 ? 1 : Trang;

    public int SoDongHopLe => SoDong switch
    {
        < 1 => 20,
        > SoDongToiDa => SoDongToiDa,
        _ => SoDong,
    };

    public int BoQua => (TrangHopLe - 1) * SoDongHopLe;
}

/// <summary>Một trang kết quả kèm tổng số dòng để client dựng thanh phân trang.</summary>
public record KetQuaTrang<T>(List<T> DuLieu, int TongSoDong, int Trang, int SoDong)
{
    public int TongSoTrang => TongSoDong == 0 ? 1 : (int)Math.Ceiling((double)TongSoDong / SoDong);
}
