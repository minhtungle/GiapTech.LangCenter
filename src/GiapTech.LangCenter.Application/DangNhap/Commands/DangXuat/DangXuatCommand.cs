using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.DangXuat;

/// <summary>
/// FR-01 — **ĐĂNG XUẤT THẬT**, thu hồi phiên ở phía server (22/09/2026).
///
/// ## Vấn đề nó chữa
///
/// Trước thay đổi này, `POST /auth/dang-xuat` **không tồn tại**: nút "Đăng xuất" ở frontend chỉ
/// xoá `localStorage`. Hệ quả là bấm đăng xuất xong,
///
/// - access token vẫn hợp lệ **tới 60 phút**,
/// - refresh token vẫn hợp lệ **tới 30 ngày**,
/// - `TAI_KHOAN.phien_hien_tai` không đổi nên `PhienDuyNhatMiddleware` vẫn cho đi qua.
///
/// Ai đọc được `localStorage` sau đó — máy dùng chung ở quầy lễ tân, extension trình duyệt,
/// bản sao lưu profile — dùng lại được trọn vẹn. Người dùng thì tin rằng mình đã đăng xuất.
/// Phát hiện trong đợt rà soát bảo mật 22/09/2026.
///
/// ## Ba việc phải làm cùng nhau
///
/// Thiếu bất kỳ việc nào thì "đăng xuất" vẫn là nói dối:
///
/// 1. **Thu hồi refresh token** — không thì người cầm token cũ tự làm mới phiên được.
/// 2. **Đặt <see cref="TaiKhoan.DaDangXuat"/> vào `PhienHienTai`** — access token còn hạn tới
///    60 phút; chỉ có cột này mới chặn được nó ngay.
/// 3. **Xoá cache phiên** — middleware cache 10 giây, không xoá thì token vừa đăng xuất còn
///    sống thêm chừng ấy.
///
/// ## Vì sao KHÔNG đặt `PhienHienTai = null`
///
/// Đây là cái bẫy chính. `null` mang nghĩa *"chưa từng đăng nhập kể từ 20/09/2026"* và
/// middleware **cố ý cho qua** — nếu đăng xuất cũng ghi `null` thì middleware sẽ cho qua đúng
/// những token vừa bị đăng xuất, tức thêm endpoint này vào mà **không chặn được gì**.
///
/// Nên dùng <see cref="TaiKhoan.DaDangXuat"/>: một `Guid` hằng, không `jti` nào trùng được
/// (`jti` sinh ngẫu nhiên), nên mọi token của tài khoản đều lệch ⇒ bị chặn. Không cần thêm cột,
/// không cần migration, và token đang lưu hành không bị ảnh hưởng (quy tắc #1).
///
/// ## Đăng xuất rồi đăng nhập lại
///
/// `DangNhapCommand` ghi đè `PhienHienTai` bằng `jti` mới nên tự khôi phục, không cần xử lý gì
/// thêm. Canh bởi test `Dang_xuat_roi_dang_nhap_lai_van_vao_duoc_binh_thuong`.
/// </summary>
public record DangXuatCommand : IRequest;

public class DangXuatHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPhienService phienService)
    : IRequestHandler<DangXuatCommand>
{
    public async Task Handle(DangXuatCommand request, CancellationToken ct)
    {
        // `TaiKhoanId` chứ không phải `UserId`: đăng xuất là thao tác trên TÀI KHOẢN.
        if (currentUser.TaiKhoanId is not { } taiKhoanId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var taiKhoan = await db.TaiKhoans.FirstOrDefaultAsync(t => t.Id == taiKhoanId, ct)
            ?? throw new KhongTimThayException($"Không thấy tài khoản {taiKhoanId}");

        // (1) Chặn access token còn hạn — xem chú thích đầu lớp về việc KHÔNG dùng `null`.
        taiKhoan.PhienHienTai = TaiKhoan.DaDangXuat;

        // (2) Thu hồi refresh token đang mở, để không tự làm mới phiên được nữa.
        var bayGio = DateTimeOffset.UtcNow;
        var dangMo = await db.RefreshTokens
            .Where(r => r.TaiKhoanId == taiKhoanId && r.ThuHoiLuc == null)
            .ToListAsync(ct);

        foreach (var r in dangMo)
            r.ThuHoiLuc = bayGio;

        await db.SaveChangesAsync(ct);

        /*
          (3) Xoá cache SAU khi lưu.

          Thứ tự quan trọng: xoá TRƯỚC `SaveChangesAsync` thì một request chen vào giữa sẽ nạp
          lại đúng giá trị CŨ vào cache, và token vừa đăng xuất sống thêm 10 giây nữa.

          ⚠️ KHÔNG có test nào canh riêng thứ tự này — đã thử và ghi lại để người sau khỏi mất
          công: mutation "đảo thứ tự" SỐNG. Test tích hợp chạy DB in-memory, `SaveChangesAsync`
          trả về đồng bộ nên không có request nào chen vào giữa được; cửa sổ đua chỉ tồn tại
          với PostgreSQL thật. Viết test cho nó sẽ phải giả lập đua, mà test như vậy thường
          xanh/đỏ thất thường và bị vô hiệu hoá sau vài lần.

          Mutation "bỏ hẳn XoaCache" thì CHẾT (test `Dang_xuat_roi_thi_access_token_...`), nên
          bản thân việc xoá cache có canh — chỉ riêng thứ tự là không.
        */
        phienService.XoaCache(taiKhoanId);
    }
}
