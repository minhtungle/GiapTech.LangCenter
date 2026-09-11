using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// LOP_HOC — một lớp học của trung tâm (FR-07).
///
/// Vòng đời: tạo qua wizard 3 bước, lưu <see cref="TrangThaiLopHoc.Nhap"/> giữa chừng, chuyển
/// sang trạng thái thật khi hoàn tất.
/// </summary>
public class LopHoc : TenantEntity
{
    public string Ten { get; set; } = null!;

    /// <summary>
    /// Giáo viên chính — đúng MỘT người, nên là cột thẳng chứ không phải bảng trung gian.
    /// Ép "đúng một" bằng cột NOT NULL rẻ và chắc hơn ép bằng UNIQUE trên bảng phụ.
    /// </summary>
    public Guid GiaoVienChinhId { get; set; }
    public NguoiDung GiaoVienChinh { get; set; } = null!;

    public HinhThucHoc HinhThuc { get; set; }

    /// <summary>Phòng học (Offline/Kết hợp). Giữ nguyên khi đổi sang Online — đổi lại thì còn.</summary>
    public string? PhongHoc { get; set; }

    /// <summary>Link học trực tuyến (Online/Kết hợp).</summary>
    public string? LinkHoc { get; set; }

    /// <summary>
    /// Học phí chuẩn của lớp. Nullable vì bước 1 của wizard cho lưu nháp khi chưa nhập.
    ///
    /// Phân biệt rõ: <c>null</c> = chưa nhập, <c>0</c> = lớp MIỄN PHÍ (lớp thử, học bổng) —
    /// hai thứ khác nhau, đừng dùng 0 làm "chưa có".
    /// </summary>
    public decimal? HocPhi { get; set; }

    /// <summary>Sức chứa tối đa. null = không giới hạn.</summary>
    public int? SucChuaToiDa { get; set; }

    /// <summary>
    /// Ngày khai giảng. Nullable vì lưu nháp ở bước 1 chưa có ngày (ô này ở bước 2).
    /// Bắt buộc khi rời khỏi trạng thái <see cref="TrangThaiLopHoc.Nhap"/> — canh ở validator
    /// của lệnh hoàn tất.
    /// </summary>
    public DateTimeOffset? NgayKhaiGiang { get; set; }

    /// <summary>Suy từ buổi học cuối, cập nhật khi sinh lịch. Không cho sửa tay.</summary>
    public DateTimeOffset? NgayKetThuc { get; set; }

    /// <summary>
    /// CHỈ lưu ba giá trị do người quyết định: Nhap / DaHuy / DaKetThuc.
    /// SapKhaiGiang và DangHoc suy từ ngày lúc đọc — xem <see cref="TrangThaiHienThi"/>.
    /// </summary>
    public TrangThaiLopHoc TrangThai { get; set; } = TrangThaiLopHoc.Nhap;

    public string? GhiChu { get; set; }

    /// <summary>
    /// Lớp gốc nếu đây là bản nhân bản — chỉ để truy vết, không ràng buộc gì về nghiệp vụ.
    /// </summary>
    public Guid? NhanBanTuLopId { get; set; }
    public LopHoc? NhanBanTuLop { get; set; }

    /// <summary>
    /// Người tạo — cần để trả lời "lớp nháp này của ai" (nháp chỉ hiện cho người tạo và
    /// người có quyền xem toàn trung tâm).
    /// </summary>
    public Guid? NguoiTaoId { get; set; }
    public NguoiDung? NguoiTao { get; set; }

    public ICollection<LopHocHocVien> HocViens { get; set; } = [];
    public ICollection<LopHocTroGiang> TroGiangs { get; set; } = [];

    /// <summary>
    /// Khoá học mà lớp này dạy — **tối đa 3** (FR-07, chốt 12/09/2026).
    ///
    /// Bảng trung gian chứ không 3 cột `KhoaHoc1Id/2/3`: ba cột thì mọi truy vấn "lớp nào dạy
    /// khoá X" phải `OR` ba lần và quên một cột là lọt, còn thêm khoá thứ tư phải đổi schema.
    /// Giới hạn 3 ép ở validator, không ở schema — con số này do nghiệp vụ đặt và có thể đổi.
    ///
    /// Rỗng = lớp chưa gán khoá (lớp nháp, hoặc lớp cũ tạo trước 12/09). Không ràng buộc
    /// NOT NULL để không phá dữ liệu đang có (quy tắc #1).
    /// </summary>
    public ICollection<LopHocKhoaHoc> KhoaHocs { get; set; } = [];

    /// <summary>
    /// Trạng thái hiển thị cho người dùng, suy từ ngày với các lớp đang chạy.
    ///
    /// Không lưu vào DB: lưu thì phải có job đổi lúc nửa đêm, job chết là lớp kẹt sai trạng
    /// thái mà không ai biết. Tính lúc đọc thì luôn đúng, đổi giá không đáng kể.
    /// </summary>
    public TrangThaiLopHoc TrangThaiTaiThoiDiem(DateTimeOffset bayGio)
    {
        // Ba trạng thái do người quyết định thì giữ nguyên, không suy diễn.
        if (TrangThai is TrangThaiLopHoc.Nhap or TrangThaiLopHoc.DaHuy or TrangThaiLopHoc.DaKetThuc)
            return TrangThai;

        if (NgayKhaiGiang is { } kg && bayGio < kg) return TrangThaiLopHoc.SapKhaiGiang;
        if (NgayKetThuc is { } kt && bayGio > kt) return TrangThaiLopHoc.DaKetThuc;

        return TrangThaiLopHoc.DangHoc;
    }
}

/// <summary>
/// LOP_HOC_HOC_VIEN — học viên trong một lớp.
///
/// Bảng riêng chứ không gộp với trợ giảng vào một bảng có cột "vai trò": học viên có ngày vào
/// lớp, trạng thái và mức học phí riêng; trợ giảng không có gì trong số đó. Gộp thì nửa số cột
/// luôn null, và mọi truy vấn học viên phải nhớ lọc thêm vai trò — quên một lần là trợ giảng
/// lọt vào bảng điểm danh.
/// </summary>
public class LopHocHocVien : TenantEntity
{
    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    /// <summary>
    /// Học viên vào giữa khoá là chuyện thường. Không có trường này thì báo cáo chuyên cần
    /// chia mẫu số sai — tính cả những buổi diễn ra trước khi họ vào lớp.
    /// </summary>
    public DateTimeOffset NgayVaoLop { get; set; }

    public DateTimeOffset? NgayRoiLop { get; set; }

    public TrangThaiHocVienTrongLop TrangThai { get; set; } = TrangThaiHocVienTrongLop.DangHoc;

    /// <summary>
    /// Mức học phí CHỐT cho riêng học viên này, snapshot lúc vào lớp.
    ///
    /// Cho phép miễn giảm cá nhân (học lại, anh chị em, học bổng), và quan trọng hơn: sửa học
    /// phí lớp KHÔNG làm đổi hồi tố công nợ của người đã đóng theo giá cũ.
    /// </summary>
    public decimal HocPhiApDung { get; set; }

    public string? GhiChu { get; set; }
}

/// <summary>LOP_HOC_TRO_GIANG — trợ giảng của lớp (nhiều người).</summary>
public class LopHocTroGiang : TenantEntity
{
    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    public Guid TroGiangId { get; set; }
    public NguoiDung TroGiang { get; set; } = null!;

    public string? GhiChu { get; set; }
}

/// <summary>
/// LOP_HOC_KHOA_HOC — lớp này dạy khoá học nào (FR-07, 12/09/2026). Tối đa 3 khoá mỗi lớp.
///
/// Đóng nợ N19: trước đây `LOP_HOC` không nối `KHOA_HOC` nên khi duyệt học viên vào lớp,
/// hệ thống không biết đơn CRM của họ có khớp lớp không — người điều phối phải tự đối chiếu
/// tên khoá bằng mắt.
/// </summary>
public class LopHocKhoaHoc : TenantEntity
{
    public Guid LopHocId { get; set; }
    public LopHoc LopHoc { get; set; } = null!;

    public Guid KhoaHocId { get; set; }
    public KhoaHoc KhoaHoc { get; set; } = null!;
}
