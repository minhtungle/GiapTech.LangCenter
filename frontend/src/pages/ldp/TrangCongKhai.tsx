import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import axios from 'axios'
import { ChevronLeft, ChevronRight, MapPin, Phone } from 'lucide-react'

type LoaiKhoi =
  | 'Hero' | 'GioiThieu' | 'KhoaHoc' | 'GiaoVien' | 'CamNhan' | 'TinTuc' | 'LienHe'
  | 'DoiTac' | 'QuyTrinh' | 'CoSo'

interface Muc {
  tieuDe: string
  phuDe: string | null
  moTa: string | null
  anhUrl: string | null
  giaNiemYet: number | null
  duongDan: string | null
}

interface Khoi {
  loai: LoaiKhoi
  tieuDe: string | null
  moTa: string | null
  anhUrl: string | null
  nhanNut: string | null
  duongDanNut: string | null
  mucs: Muc[]
}

interface Trang {
  tenTrungTam: string
  logoUrl: string | null
  tieuDeSeo: string | null
  moTaSeo: string | null
  khois: Khoi[]
}

/**
 * FR-30 — trang đích CÔNG KHAI. Khách vãng lai, không đăng nhập.
 *
 * ## Dùng axios trần, KHÔNG dùng `api.ts`
 *
 * `api.ts` gắn token và có interceptor làm mới token rồi đá về `/dang-nhap` khi 401 — ở đây
 * chưa ai đăng nhập, nên luồng đó vừa vô nghĩa vừa đẩy khách vãng lai vào màn đăng nhập của
 * một hệ thống họ không liên quan.
 *
 * ## Hai đường vào
 *
 * - `/t/{mã}` — trung tâm chưa trỏ domain riêng. Mã đi qua header `X-Ma-Trung-Tam`.
 * - Domain riêng — không có mã trên URL, server tự giải từ domain (ADR-0008).
 *
 * ## Bố cục
 *
 * Theo khuôn landing giáo dục phổ biến (chủ sản phẩm đưa edulife.com.vn làm tham chiếu):
 * header dính, hero căn giữa, lưới thẻ, băng chuyền ngang cho cảm nhận và giáo viên, dải
 * logo đối tác, các bước đánh số, nút gọi nổi.
 *
 * **Dựng lại bố cục, không sao chép nội dung hay nhận diện của trang tham chiếu** — màu sắc
 * lấy từ design token của hệ thống, nội dung do từng trung tâm tự nhập.
 */
export default function TrangCongKhai() {
  const { ma } = useParams<{ ma?: string }>()
  const [trang, setTrang] = useState<Trang | null>(null)
  const [trangThai, setTrangThai] = useState<'dangTai' | 'xong' | 'khongCo'>('dangTai')

  useEffect(() => {
    const headers = ma ? { 'X-Ma-Trung-Tam': ma.toUpperCase() } : undefined

    axios
      .get<Trang>('/api/v1/ldp/cong-khai', { headers })
      .then((res) => { setTrang(res.data); setTrangThai('xong') })
      .catch(() => setTrangThai('khongCo'))
  }, [ma])

  useEffect(() => {
    if (!trang) return

    const truoc = document.title
    document.title = trang.tieuDeSeo || trang.tenTrungTam

    const meta = document.querySelector('meta[name="description"]')
    const moTaTruoc = meta?.getAttribute('content') ?? null
    if (meta && trang.moTaSeo) meta.setAttribute('content', trang.moTaSeo)

    /*
      Đường `/t/{mã}` gắn `noindex`: một nội dung ở hai địa chỉ thì Google phạt trùng lặp, và
      ta muốn nó lưu domain chính thức của trung tâm chứ không phải đường dự phòng.
    */
    let robots: HTMLMetaElement | null = null
    if (ma) {
      robots = document.createElement('meta')
      robots.name = 'robots'
      robots.content = 'noindex'
      document.head.appendChild(robots)
    }

    return () => {
      document.title = truoc
      if (meta && moTaTruoc !== null) meta.setAttribute('content', moTaTruoc)
      robots?.remove()
    }
  }, [trang, ma])

  if (trangThai === 'dangTai') {
    return (
      <div className="flex min-h-screen items-center justify-center text-muted-foreground">
        Đang tải…
      </div>
    )
  }

  if (trangThai === 'khongCo' || !trang) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-2 p-6 text-center">
        <h1 className="text-2xl font-semibold">Không tìm thấy trang</h1>
        <p className="text-muted-foreground">
          Trung tâm này chưa có trang giới thiệu, hoặc đường dẫn không đúng.
        </p>
      </div>
    )
  }

  const hien = trang.khois
  const coLienHe = hien.some((k) => k.loai === 'LienHe')

  return (
    <div className="min-h-screen bg-background">
      {/*
        Header DÍNH — khuôn landing phổ biến: người dùng cuộn sâu vẫn bấm được nút gọi hành
        động mà không phải cuộn ngược lên đầu.
      */}
      <header className="sticky top-0 z-30 border-b bg-background/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center gap-3 px-4 py-3">
          {trang.logoUrl && (
            <img src={trang.logoUrl} alt="" className="h-9 w-9 shrink-0 rounded object-contain" />
          )}
          <span className="truncate font-semibold">{trang.tenTrungTam}</span>

          {coLienHe && (
            <a
              href="#lien-he"
              className="ml-auto hidden h-9 shrink-0 items-center rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground hover:bg-primary/90 sm:inline-flex"
            >
              Nhận tư vấn
            </a>
          )}
        </div>
      </header>

      <main>
        {hien.map((k) => (
          <KhoiView key={k.loai} khoi={k} maTrungTam={ma} />
        ))}
      </main>

      <footer className="border-t bg-muted/40 py-8 text-center text-sm text-muted-foreground">
        <p className="font-medium text-foreground">{trang.tenTrungTam}</p>
        <p className="mt-1">© {new Date().getFullYear()}</p>
      </footer>

      {/*
        Nút gọi NỔI ở góc — chỉ hiện trên màn hẹp, vì trên desktop đã có nút ở header dính.
        Hai nút cùng lúc trên màn nhỏ sẽ che mất nội dung.
      */}
      {coLienHe && (
        <a
          href="#lien-he"
          className="fixed bottom-4 right-4 z-30 inline-flex h-12 items-center gap-2 rounded-full bg-primary px-5 font-medium text-primary-foreground shadow-lg hover:bg-primary/90 sm:hidden"
        >
          <Phone className="h-4 w-4" />
          Nhận tư vấn
        </a>
      )}
    </div>
  )
}

/** Tiêu đề mục — dùng chung để mọi khối có cùng nhịp chữ và khoảng cách. */
function TieuDeMuc({ tieuDe, moTa }: { tieuDe: string | null; moTa: string | null }) {
  if (!tieuDe && !moTa) return null
  return (
    <div className="mx-auto mb-10 max-w-2xl text-center">
      {tieuDe && <h2 className="text-2xl font-bold sm:text-3xl">{tieuDe}</h2>}
      {moTa && <p className="mt-3 whitespace-pre-line text-muted-foreground">{moTa}</p>}
    </div>
  )
}

function KhoiView({ khoi, maTrungTam }: { khoi: Khoi; maTrungTam?: string }) {
  switch (khoi.loai) {
    case 'Hero':
      return <Hero khoi={khoi} />
    case 'GioiThieu':
      return <GioiThieu khoi={khoi} />
    case 'QuyTrinh':
      return <QuyTrinh khoi={khoi} />
    case 'DoiTac':
      return <DoiTac khoi={khoi} />
    case 'CamNhan':
    case 'GiaoVien':
      return <BangChuyen khoi={khoi} />
    case 'CoSo':
      return <CoSo khoi={khoi} />
    case 'LienHe':
      return <LienHe khoi={khoi} maTrungTam={maTrungTam} />
    default:
      return <LuoiThe khoi={khoi} />
  }
}

/** Hero — chữ căn giữa trên ảnh nền, khuôn landing giáo dục phổ biến. */
function Hero({ khoi }: { khoi: Khoi }) {
  return (
    <section
      className="relative flex min-h-[70vh] items-center justify-center overflow-hidden px-4 py-20 text-center"
      style={
        khoi.anhUrl
          ? { backgroundImage: `url(${khoi.anhUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' }
          : undefined
      }
    >
      {khoi.anhUrl ? (
        // Lớp phủ để chữ đọc được trên ảnh BẤT KỲ — không có nó thì ảnh sáng làm mất chữ.
        <div className="absolute inset-0 bg-background/80 backdrop-blur-[2px]" />
      ) : (
        <div className="absolute inset-0 bg-gradient-to-b from-primary/10 to-background" />
      )}

      <div className="relative mx-auto max-w-3xl">
        {khoi.tieuDe && (
          <h1 className="text-3xl font-bold leading-tight sm:text-5xl">{khoi.tieuDe}</h1>
        )}
        {khoi.moTa && (
          <p className="mt-5 whitespace-pre-line text-lg text-muted-foreground">{khoi.moTa}</p>
        )}
        {khoi.nhanNut && (
          <a
            href={khoi.duongDanNut || '#lien-he'}
            className="mt-8 inline-flex h-12 items-center rounded-md bg-primary px-8 text-base font-medium text-primary-foreground shadow-sm hover:bg-primary/90"
          >
            {khoi.nhanNut}
          </a>
        )}
      </div>
    </section>
  )
}

/** Giới thiệu — hai cột: chữ một bên, ảnh một bên. */
function GioiThieu({ khoi }: { khoi: Khoi }) {
  return (
    <section className="px-4 py-16">
      <div className="mx-auto grid max-w-6xl items-center gap-10 sm:grid-cols-2">
        <div>
          {khoi.tieuDe && <h2 className="text-2xl font-bold sm:text-3xl">{khoi.tieuDe}</h2>}
          {khoi.moTa && (
            <p className="mt-4 whitespace-pre-line leading-relaxed text-muted-foreground">
              {khoi.moTa}
            </p>
          )}
        </div>
        {khoi.anhUrl && (
          <img
            src={khoi.anhUrl}
            alt=""
            className="w-full rounded-xl object-cover shadow-sm"
          />
        )}
      </div>
    </section>
  )
}

/** Lưới thẻ — dùng cho khoá học và tin tức. Ảnh trên, chữ dưới. */
function LuoiThe({ khoi }: { khoi: Khoi }) {
  return (
    <section className="border-t bg-muted/30 px-4 py-16">
      <div className="mx-auto max-w-6xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {khoi.mucs.map((m) => (
            <article
              key={m.tieuDe}
              className="overflow-hidden rounded-xl border bg-background shadow-sm transition-shadow hover:shadow-md"
            >
              {m.anhUrl && <img src={m.anhUrl} alt="" className="h-44 w-full object-cover" />}
              <div className="p-5">
                <h3 className="font-semibold">{m.tieuDe}</h3>
                {m.phuDe && <p className="mt-0.5 text-sm text-muted-foreground">{m.phuDe}</p>}
                {m.moTa && (
                  <p className="mt-3 whitespace-pre-line text-sm leading-relaxed">{m.moTa}</p>
                )}
                {m.giaNiemYet != null && (
                  <p className="mt-4 text-lg font-bold text-primary">
                    {m.giaNiemYet.toLocaleString('vi-VN')} ₫
                  </p>
                )}
                {m.duongDan && (
                  <a
                    href={m.duongDan}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="mt-3 inline-block text-sm font-medium text-primary hover:underline"
                  >
                    Xem thêm →
                  </a>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}

/**
 * Băng chuyền ngang — cảm nhận học viên và đội ngũ giáo viên.
 *
 * Cuộn ngang bằng CSS scroll-snap thay vì thư viện carousel: không thêm phụ thuộc, chạy được
 * bằng ngón tay trên di động, và bàn phím vẫn cuộn được. Hai nút mũi tên chỉ là tiện ích trên
 * desktop nơi không có thao tác vuốt.
 */
function BangChuyen({ khoi }: { khoi: Khoi }) {
  const dai = useRef<HTMLDivElement>(null)
  const laNguoi = khoi.loai === 'GiaoVien'

  const cuon = (huong: number) =>
    dai.current?.scrollBy({ left: huong * 320, behavior: 'smooth' })

  return (
    <section className="px-4 py-16">
      <div className="mx-auto max-w-6xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />

        <div className="relative">
          <div
            ref={dai}
            className="flex snap-x snap-mandatory gap-5 overflow-x-auto pb-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
          >
            {khoi.mucs.map((m) => (
              <article
                key={m.tieuDe}
                className="w-[280px] shrink-0 snap-start rounded-xl border bg-background p-5 shadow-sm"
              >
                {m.anhUrl && (
                  <img
                    src={m.anhUrl}
                    alt=""
                    className={
                      laNguoi
                        ? 'mx-auto mb-4 h-28 w-28 rounded-full object-cover'
                        : 'mb-4 h-40 w-full rounded-lg object-cover'
                    }
                  />
                )}
                <h3 className={laNguoi ? 'text-center font-semibold' : 'font-semibold'}>
                  {m.tieuDe}
                </h3>
                {m.phuDe && (
                  <p className={`text-sm text-muted-foreground ${laNguoi ? 'text-center' : ''}`}>
                    {m.phuDe}
                  </p>
                )}
                {m.moTa && (
                  <p className="mt-3 whitespace-pre-line text-sm leading-relaxed">{m.moTa}</p>
                )}
                {m.duongDan && (
                  <a
                    href={m.duongDan}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="mt-3 inline-block text-sm font-medium text-primary hover:underline"
                  >
                    Xem thêm →
                  </a>
                )}
              </article>
            ))}
          </div>

          {khoi.mucs.length > 2 && (
            <div className="mt-2 hidden justify-center gap-2 sm:flex">
              <button
                type="button"
                aria-label="Xem mục trước"
                onClick={() => cuon(-1)}
                className="inline-flex h-9 w-9 items-center justify-center rounded-full border bg-background hover:bg-muted"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <button
                type="button"
                aria-label="Xem mục sau"
                onClick={() => cuon(1)}
                className="inline-flex h-9 w-9 items-center justify-center rounded-full border bg-background hover:bg-muted"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          )}
        </div>
      </div>
    </section>
  )
}

/** Quy trình — các bước đánh số. Thứ tự mục chính là số bước. */
function QuyTrinh({ khoi }: { khoi: Khoi }) {
  return (
    <section className="border-t bg-muted/30 px-4 py-16">
      <div className="mx-auto max-w-5xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />

        <ol className="grid gap-6 sm:grid-cols-3">
          {khoi.mucs.map((m, i) => (
            <li key={m.tieuDe} className="text-center">
              <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary text-lg font-bold text-primary-foreground">
                {i + 1}
              </span>
              <h3 className="mt-4 font-semibold">{m.tieuDe}</h3>
              {m.moTa && (
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{m.moTa}</p>
              )}
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}

/**
 * Dải logo đối tác.
 *
 * Chỉ ảnh, không chữ — dải logo mà kèm mô tả thì thành lưới thẻ và mất hiệu ứng "một hàng
 * thương hiệu". Tên mục dùng làm `alt` cho trình đọc màn hình.
 */
function DoiTac({ khoi }: { khoi: Khoi }) {
  return (
    <section className="px-4 py-12">
      <div className="mx-auto max-w-6xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />

        <div className="flex flex-wrap items-center justify-center gap-8">
          {khoi.mucs.map((m) =>
            m.anhUrl ? (
              <img
                key={m.tieuDe}
                src={m.anhUrl}
                alt={m.tieuDe}
                title={m.tieuDe}
                // Xám hoá rồi hiện màu khi rê chuột: giữ dải logo không "ồn" hơn nội dung
                // chính, nhưng vẫn nhận ra được.
                className="h-12 w-auto object-contain opacity-70 grayscale transition hover:opacity-100 hover:grayscale-0"
              />
            ) : (
              <span key={m.tieuDe} className="text-sm text-muted-foreground">
                {m.tieuDe}
              </span>
            ),
          )}
        </div>
      </div>
    </section>
  )
}

/** Cơ sở / chi nhánh — thẻ có ảnh, tên, địa chỉ, giờ mở cửa. */
function CoSo({ khoi }: { khoi: Khoi }) {
  return (
    <section className="border-t px-4 py-16">
      <div className="mx-auto max-w-6xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {khoi.mucs.map((m) => (
            <article key={m.tieuDe} className="overflow-hidden rounded-xl border shadow-sm">
              {m.anhUrl && <img src={m.anhUrl} alt="" className="h-36 w-full object-cover" />}
              <div className="p-4">
                <h3 className="font-semibold">{m.tieuDe}</h3>
                {m.moTa && (
                  <p className="mt-2 flex gap-1.5 text-sm text-muted-foreground">
                    <MapPin className="mt-0.5 h-4 w-4 shrink-0" />
                    <span className="whitespace-pre-line">{m.moTa}</span>
                  </p>
                )}
                {m.phuDe && <p className="mt-2 text-sm text-muted-foreground">{m.phuDe}</p>}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}

function LienHe({ khoi, maTrungTam }: { khoi: Khoi; maTrungTam?: string }) {
  return (
    <section id="lien-he" className="border-t bg-muted/30 px-4 py-16">
      <div className="mx-auto max-w-2xl">
        <TieuDeMuc tieuDe={khoi.tieuDe} moTa={khoi.moTa} />
        <FormLienHe maTrungTam={maTrungTam} />
      </div>
    </section>
  )
}

function FormLienHe({ maTrungTam }: { maTrungTam?: string }) {
  const [gui, setGui] = useState<'chua' | 'dangGui' | 'xong' | 'loi'>('chua')
  const [form, setForm] = useState({
    hoTen: '', soDienThoai: '', email: '', quanTam: '', loiNhan: '', website: '',
  })

  const doi =
    (k: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setForm((f) => ({ ...f, [k]: e.target.value }))

  const guiForm = async (e: React.FormEvent) => {
    e.preventDefault()
    setGui('dangGui')
    try {
      await axios.post('/api/v1/ldp/lien-he', form, {
        headers: maTrungTam ? { 'X-Ma-Trung-Tam': maTrungTam.toUpperCase() } : undefined,
      })
      setGui('xong')
    } catch {
      setGui('loi')
    }
  }

  if (gui === 'xong') {
    return (
      <div className="rounded-xl border border-primary/30 bg-primary/5 p-8 text-center">
        <p className="text-lg font-medium">Đã nhận thông tin của bạn.</p>
        <p className="mt-2 text-sm text-muted-foreground">
          Trung tâm sẽ liên hệ lại trong thời gian sớm nhất.
        </p>
      </div>
    )
  }

  const oChung =
    'h-11 rounded-md border bg-background px-3 focus:outline-none focus:ring-2 focus:ring-ring'

  return (
    <form onSubmit={guiForm} className="flex flex-col gap-3 rounded-xl border bg-background p-6 shadow-sm">
      {/*
        HONEYPOT — ẩn bằng CSS, người thật không thấy nên không điền; bot điền mọi trường.
        `tabIndex={-1}` và `autoComplete="off"` để bàn phím và trình quản lý mật khẩu bỏ qua.
      */}
      <div className="absolute left-[-9999px]" aria-hidden>
        <label htmlFor="website">Website</label>
        <input
          id="website" name="website" tabIndex={-1} autoComplete="off"
          value={form.website} onChange={doi('website')}
        />
      </div>

      <input
        required placeholder="Họ và tên" value={form.hoTen} onChange={doi('hoTen')}
        className={oChung}
      />
      <input
        required type="tel" placeholder="Số điện thoại"
        value={form.soDienThoai} onChange={doi('soDienThoai')}
        className={oChung}
      />
      <input
        type="email" placeholder="Email (không bắt buộc)"
        value={form.email} onChange={doi('email')}
        className={oChung}
      />
      <input
        placeholder="Quan tâm khoá nào?" value={form.quanTam} onChange={doi('quanTam')}
        className={oChung}
      />
      <textarea
        rows={3} placeholder="Lời nhắn" value={form.loiNhan} onChange={doi('loiNhan')}
        className="rounded-md border bg-background p-3 focus:outline-none focus:ring-2 focus:ring-ring"
      />

      {gui === 'loi' && (
        <p role="alert" className="text-sm text-destructive">
          Chưa gửi được. Vui lòng thử lại sau ít phút.
        </p>
      )}

      <button
        type="submit"
        disabled={gui === 'dangGui'}
        className="h-12 rounded-md bg-primary text-base font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
      >
        {gui === 'dangGui' ? 'Đang gửi…' : 'Gửi thông tin'}
      </button>
    </form>
  )
}
