import { useTranslation } from 'react-i18next'
import { GraduationCap } from 'lucide-react'
import { vietTat } from '@/lib/nhanDienTrungTam'
import type { NhanDienTrungTam } from '@/lib/traTenTrungTam'
import anhMacDinh from '@/assets/banner-hoc-tap.jpg'

/**
 * Banner bên trái màn đăng nhập (22/09/2026) — *"bên trái để hiển thị 1 khung banner được
 * setting trong thiết lập như logo, nếu chưa có hãy để mặc định"*.
 *
 * ## Ba trạng thái, không phải hai
 *
 * | Khi nào | Hiện gì |
 * |---|---|
 * | Chưa gõ mã (hoặc mã sai) | **Ảnh học tập mặc định** + tên phần mềm — không để trống |
 * | Gõ đúng mã, chưa có ảnh bìa | Ảnh mặc định + logo + tên + mô tả + địa chỉ |
 * | Gõ đúng mã, đã có ảnh bìa | **Ảnh bìa của trung tâm** thay ảnh mặc định |
 *
 * Ảnh mặc định là ảnh THẬT về học tập (22/09/2026, yêu cầu chủ sản phẩm) — xem
 * `assets/banner-hoc-tap.NGUON.md` cho nguồn, giấy phép và lý do chọn. Trước đó chỗ này chỉ
 * có nền gradient; gradient nay lùi xuống làm lớp dưới cùng, chỉ lộ ra khi **cả hai** ảnh
 * hỏng.
 *
 * Trạng thái đầu hay bị quên: người dùng mở trang là thấy ngay, mà lúc đó chưa gõ gì nên chưa
 * biết trung tâm nào. Để trống thì nửa màn hình trắng — đúng cái *"hơi trống"* cần chữa.
 *
 * ## Responsive
 *
 * Màn hẹp (< lg) **ẩn hẳn banner**: nhồi cả banner lẫn form vào màn điện thoại thì form bị đẩy
 * xuống dưới màn hình, người dùng phải cuộn mới đăng nhập được — tệ hơn hẳn so với không có
 * banner. Nhận diện trung tâm vẫn thấy ở thẻ dưới ô mã.
 */
export function BannerTrungTam({
  trungTam,
  duongDanLogo,
  duongDanAnhBia,
}: {
  /** `null`/`undefined` = chưa gõ mã hoặc mã sai → banner mặc định. */
  trungTam: NhanDienTrungTam | null | undefined
  duongDanLogo?: string
  duongDanAnhBia?: string
}) {
  const { t } = useTranslation()

  return (
    <div className="relative hidden overflow-hidden bg-primary lg:flex lg:w-[45%] xl:w-1/2">
      {/*
        Ba lớp chồng lên nhau, dưới cùng lộ ra khi lớp trên hỏng:

          1. gradient thương hiệu — lưới an toàn cuối
          2. ảnh học tập mặc định (đóng gói trong bundle, không đi mạng)
          3. ảnh bìa của trung tâm nếu có

        Ảnh dùng `<img>` phủ kín thay vì `background-image`: `onError` bắt được ảnh hỏng (bị
        xoá khỏi kho, MinIO chết) và ẩn đi, để lộ lớp dưới — `background-image` hỏng thì chỉ
        còn khoảng trống, không có cách nào biết.
      */}
      <div className="absolute inset-0 bg-gradient-to-br from-primary via-primary to-primary/70" />

      <img
        src={anhMacDinh}
        alt=""
        className="absolute inset-0 h-full w-full object-cover"
        onError={(e) => {
          e.currentTarget.style.display = 'none'
        }}
      />

      {duongDanAnhBia && (
        <img
          src={duongDanAnhBia}
          alt=""
          className="absolute inset-0 h-full w-full object-cover"
          onError={(e) => {
            e.currentTarget.style.display = 'none'
          }}
        />
      )}

      {/*
        Lớp phủ tối — LUÔN có, không chỉ khi trung tâm tải ảnh bìa.

        Giờ nền mặc định cũng là ảnh thật, mà ảnh nào cũng có vùng sáng nuốt mất chữ trắng.
        Dùng gradient CHÉO thay vì phủ đều: đậm nhất ở góc dưới-trái (nơi có logo, tên, mô tả,
        địa chỉ), nhạt dần lên góc trên-phải để ảnh vẫn nhìn ra được.

        Đổi từ gradient dọc sang chéo sau khi soi ảnh chụp: chữ "Hà Nội, Việt Nam" nằm đúng
        vùng laptop sáng ở giữa-trái, gradient dọc chưa đủ đậm ở đó.
      */}
      <div className="absolute inset-0 bg-gradient-to-tr from-black/85 via-black/55 to-black/20" />

      {/*
        Tên phần mềm neo TUYỆT ĐỐI ở góc trên, khối nội dung căn giữa theo chiều dọc.

        Bản đầu dùng `justify-between` với một `<div/>` giữ chỗ ở cuối — nội dung bị đẩy xuống
        dưới giữa màn, nhìn lệch. Neo tuyệt đối thì khối giữa căn giữa thật, không phụ thuộc
        chiều cao của hai khối kia.
      */}
      <div className="absolute left-10 top-10 z-10 flex items-center gap-2.5 text-sm font-medium text-primary-foreground opacity-90">
        <GraduationCap className="h-5 w-5" />
        {t('dangNhap.tenPhanMem')}
      </div>

      <div className="relative flex w-full flex-col justify-center p-10 text-primary-foreground">

        {trungTam ? (
          <div className="grid gap-4">
            {duongDanLogo ? (
              <img
                src={duongDanLogo}
                alt=""
                /* Nền trắng cho logo: logo thường là ảnh tối trên nền trong suốt, đặt thẳng
                   lên nền xanh đậm thì gần như biến mất. */
                className="h-20 w-20 rounded-xl bg-white/95 object-contain p-2"
                onError={(e) => {
                  e.currentTarget.style.display = 'none'
                }}
              />
            ) : (
              <span className="flex h-20 w-20 items-center justify-center rounded-xl bg-white/15 text-2xl font-semibold backdrop-blur">
                {vietTat(trungTam.tenVietTat ?? trungTam.tenTrungTam)}
              </span>
            )}

            <div className="grid gap-1.5">
              <h2 className="text-3xl font-semibold leading-tight">{trungTam.tenTrungTam}</h2>

              {trungTam.moTa && (
                <p className="max-w-md text-sm leading-relaxed opacity-90">{trungTam.moTa}</p>
              )}

              {trungTam.diaChi && (
                <p className="text-sm opacity-75">{trungTam.diaChi}</p>
              )}
            </div>
          </div>
        ) : (
          /* Chưa gõ mã: vẫn phải có nội dung, không để nửa màn hình trống. */
          <div className="grid gap-2">
            <h2 className="max-w-md text-3xl font-semibold leading-tight">
              {t('dangNhap.bannerTieuDe')}
            </h2>
            <p className="max-w-md text-sm leading-relaxed opacity-85">
              {t('dangNhap.bannerMoTa')}
            </p>
          </div>
        )}

      </div>
    </div>
  )
}
