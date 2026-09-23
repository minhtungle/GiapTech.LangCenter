import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import axios from 'axios'

type LoaiKhoi =
  | 'Hero' | 'GioiThieu' | 'KhoaHoc' | 'GiaoVien' | 'CamNhan' | 'TinTuc' | 'LienHe'

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
 * Cùng một component phục vụ cả hai: khác biệt duy nhất là có gửi header hay không.
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

  // Tiêu đề tab và mô tả tìm kiếm — đặt sau khi có dữ liệu.
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
    return <div className="flex min-h-screen items-center justify-center text-muted-foreground">Đang tải…</div>
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

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 border-b bg-background/95 backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center gap-3 px-4 py-3">
          {trang.logoUrl && (
            <img src={trang.logoUrl} alt="" className="h-9 w-9 rounded object-contain" />
          )}
          <span className="font-semibold">{trang.tenTrungTam}</span>
        </div>
      </header>

      <main>
        {trang.khois.map((k) => (
          <KhoiView key={k.loai} khoi={k} maTrungTam={ma} />
        ))}
      </main>

      <footer className="border-t py-6 text-center text-sm text-muted-foreground">
        © {new Date().getFullYear()} {trang.tenTrungTam}
      </footer>
    </div>
  )
}

function KhoiView({ khoi, maTrungTam }: { khoi: Khoi; maTrungTam?: string }) {
  if (khoi.loai === 'Hero') {
    return (
      <section
        className="relative flex min-h-[60vh] items-center justify-center bg-muted/40 px-4 py-16 text-center"
        style={khoi.anhUrl ? { backgroundImage: `url(${khoi.anhUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' } : undefined}
      >
        {/* Lớp phủ để chữ đọc được trên ảnh bất kỳ — không có nó thì ảnh sáng làm mất chữ. */}
        {khoi.anhUrl && <div className="absolute inset-0 bg-background/70" />}
        <div className="relative mx-auto max-w-3xl">
          {khoi.tieuDe && <h1 className="text-3xl font-bold sm:text-5xl">{khoi.tieuDe}</h1>}
          {khoi.moTa && <p className="mt-4 text-lg text-muted-foreground">{khoi.moTa}</p>}
          {khoi.nhanNut && (
            <a
              href={khoi.duongDanNut || '#lien-he'}
              className="mt-6 inline-flex h-11 items-center rounded-md bg-primary px-6 font-medium text-primary-foreground hover:bg-primary/90"
            >
              {khoi.nhanNut}
            </a>
          )}
        </div>
      </section>
    )
  }

  if (khoi.loai === 'LienHe') {
    return (
      <section id="lien-he" className="border-t bg-muted/30 px-4 py-14">
        <div className="mx-auto max-w-2xl">
          {khoi.tieuDe && <h2 className="text-center text-2xl font-semibold">{khoi.tieuDe}</h2>}
          {khoi.moTa && (
            <p className="mt-2 whitespace-pre-line text-center text-muted-foreground">{khoi.moTa}</p>
          )}
          <FormLienHe maTrungTam={maTrungTam} />
        </div>
      </section>
    )
  }

  if (khoi.loai === 'GioiThieu') {
    return (
      <section className="px-4 py-14">
        <div className="mx-auto grid max-w-5xl items-center gap-8 sm:grid-cols-2">
          <div>
            {khoi.tieuDe && <h2 className="text-2xl font-semibold">{khoi.tieuDe}</h2>}
            {khoi.moTa && (
              <p className="mt-3 whitespace-pre-line text-muted-foreground">{khoi.moTa}</p>
            )}
          </div>
          {khoi.anhUrl && (
            <img src={khoi.anhUrl} alt="" className="w-full rounded-lg object-cover" />
          )}
        </div>
      </section>
    )
  }

  // Bốn khối còn lại cùng hình dạng: tiêu đề + lưới thẻ.
  return (
    <section className="border-t px-4 py-14">
      <div className="mx-auto max-w-5xl">
        {khoi.tieuDe && <h2 className="text-center text-2xl font-semibold">{khoi.tieuDe}</h2>}
        {khoi.moTa && (
          <p className="mx-auto mt-2 max-w-2xl text-center text-muted-foreground">{khoi.moTa}</p>
        )}

        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {khoi.mucs.map((m) => (
            <article key={m.tieuDe} className="overflow-hidden rounded-lg border">
              {m.anhUrl && (
                <img src={m.anhUrl} alt="" className="h-40 w-full object-cover" />
              )}
              <div className="p-4">
                <h3 className="font-medium">{m.tieuDe}</h3>
                {m.phuDe && <p className="text-sm text-muted-foreground">{m.phuDe}</p>}
                {m.moTa && <p className="mt-2 text-sm">{m.moTa}</p>}
                {m.giaNiemYet != null && (
                  <p className="mt-3 font-semibold text-primary">
                    {m.giaNiemYet.toLocaleString('vi-VN')} ₫
                  </p>
                )}
                {m.duongDan && (
                  <a
                    href={m.duongDan}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="mt-2 inline-block text-sm text-primary hover:underline"
                  >
                    Xem thêm
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

function FormLienHe({ maTrungTam }: { maTrungTam?: string }) {
  const [gui, setGui] = useState<'chua' | 'dangGui' | 'xong' | 'loi'>('chua')
  const [form, setForm] = useState({
    hoTen: '', soDienThoai: '', email: '', quanTam: '', loiNhan: '', website: '',
  })

  const doi = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
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
      <div className="mt-6 rounded-lg border border-primary/30 bg-primary/5 p-6 text-center">
        <p className="font-medium">Đã nhận thông tin của bạn.</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Trung tâm sẽ liên hệ lại trong thời gian sớm nhất.
        </p>
      </div>
    )
  }

  return (
    <form onSubmit={guiForm} className="mt-6 flex flex-col gap-3">
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
        className="h-11 rounded-md border bg-background px-3"
      />
      <input
        required type="tel" placeholder="Số điện thoại"
        value={form.soDienThoai} onChange={doi('soDienThoai')}
        className="h-11 rounded-md border bg-background px-3"
      />
      <input
        type="email" placeholder="Email (không bắt buộc)"
        value={form.email} onChange={doi('email')}
        className="h-11 rounded-md border bg-background px-3"
      />
      <input
        placeholder="Quan tâm khoá nào?" value={form.quanTam} onChange={doi('quanTam')}
        className="h-11 rounded-md border bg-background px-3"
      />
      <textarea
        rows={3} placeholder="Lời nhắn" value={form.loiNhan} onChange={doi('loiNhan')}
        className="rounded-md border bg-background p-3"
      />

      {gui === 'loi' && (
        <p role="alert" className="text-sm text-destructive">
          Chưa gửi được. Vui lòng thử lại sau ít phút.
        </p>
      )}

      <button
        type="submit"
        disabled={gui === 'dangGui'}
        className="h-11 rounded-md bg-primary font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
      >
        {gui === 'dangGui' ? 'Đang gửi…' : 'Gửi thông tin'}
      </button>
    </form>
  )
}
