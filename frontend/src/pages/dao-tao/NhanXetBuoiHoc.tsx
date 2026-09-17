import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Label, Textarea, TrangTrong,
} from '@/components/ui'
import { useXacNhan } from '@/lib/xacNhan'

interface DiemTieuChiDto {
  tieuChiId: string
  tenTieuChi: string
  diem: number
  /** null = điểm chấm TRƯỚC 18/09/2026, khi phiếu chưa tách người đứng lớp. */
  nguoiDuocChamId: string | null
  tenNguoiDuocCham: string | null
}

interface NhanXetDto {
  id: string
  hocVienId: string
  hoTen: string
  noiDung: string
  /** Giữ để ĐỌC nhận xét cũ; phiếu mới không còn chấm trường này (18/09/2026). */
  mucHaiLong: number | null
  diemTieuChis: DiemTieuChiDto[]
  thoiDiem: string
  /** true = nhận xét của chính người đang xem — backend tự xác định từ token. */
  cuaToi: boolean
}

interface NhanXetGvDto {
  hoTen: string
  nhanXet: string | null
}

interface NguoiDungLopDto {
  id: string
  hoTen: string
  vaiTro: 'GiaoVien' | 'TroGiang'
}

const MUC = [1, 2, 3, 4, 5]

/** Khoá của một ô điểm: một tiêu chí cho một người. */
const khoa = (tieuChiId: string, nguoiId: string) => `${nguoiId}|${tieuChiId}`

/**
 * Nhận xét quanh một buổi học — HAI chiều, cố ý gộp vào một tab.
 *
 * - Học viên nhận xét về BUỔI + **chấm từng người đứng lớp theo tiêu chí** (bảng
 *   `NHAN_XET_BUOI_HOC` + `DIEM_TIEU_CHI`), mỗi người một bản, gửi lại = sửa.
 * - Giáo viên nhận xét về TỪNG HỌC VIÊN (cột `DIEM_DANH.nhan_xet`) — chỉ ĐỌC ở đây, ghi ở
 *   tab Điểm danh nơi đã có sẵn một dòng cho mỗi học viên.
 *
 * ## Chấm SAO → chấm theo TIÊU CHÍ, riêng từng người (18/09/2026)
 *
 * Chủ sản phẩm: *"phần đánh sao cho mức hài lòng cần thay bằng tiêu chí đánh giá cho giáo viên
 * và trợ giảng như đã quy định tại HRM, bố trí lại giao diện phần nhận xét cho thuận tiện hiển
 * thị và thao tác"*.
 *
 * Ba thay đổi:
 *
 * 1. **Bỏ ô "mức hài lòng" chung** khỏi form. Vẫn ĐỌC được ở nhận xét cũ (quy tắc #1) — 2 điểm
 *    thật đã chấm trước đây không được biến mất khỏi màn hình.
 * 2. **Chấm riêng giáo viên và trợ giảng**: mỗi người một khối, cùng bộ tiêu chí. Trước đây
 *    chấm chung nên xếp hạng trợ giảng ở FR-29 thực chất là điểm của giáo viên.
 * 3. **Bảng thay vì cột sao dọc**: N tiêu chí × M người mà xếp dọc thì phiếu dài mấy màn hình.
 *    Dạng bảng — tiêu chí theo hàng, mức 1–5 theo cột — thao tác một lần bấm, so sánh được các
 *    tiêu chí với nhau ngay trên một khối.
 *
 * Ai thấy nhận xét của ai do BACKEND quyết (`LayNhanXetBuoiHocQuery` lọc theo quyền), không
 * do component này ẩn hiện — ẩn ở frontend thì gọi API trực tiếp vẫn đọc được.
 */
export function NhanXetBuoiHoc({
  buoiHocId,
  toiLaHocVien,
}: {
  buoiHocId: string
  /** Chỉ học viên đang học của lớp gửi được nhận xét về buổi — xem `BuoiHocDto.toiLaHocVien`. */
  toiLaHocVien: boolean
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { hoi, hop } = useXacNhan()
  const [noiDung, setNoiDung] = useState('')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daGui, setDaGui] = useState(false)

  /** Điểm đang chọn, khoá theo `người|tiêu chí`. */
  const [diems, setDiems] = useState<Record<string, number>>({})

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['buoi-hoc', buoiHocId, 'nhan-xet'],
    queryFn: async () =>
      (await api.get<NhanXetDto[]>(`/buoi-hoc/${buoiHocId}/nhan-xet`)).data,
  })

  // Nhận xét của GV lấy từ chính bảng điểm danh — không có endpoint riêng, và cũng không nên
  // có: quyền đọc điểm danh đã đúng là quyền cần để đọc nhận xét kèm nó.
  const { data: diemDanh = [] } = useQuery({
    queryKey: ['buoi-hoc', buoiHocId, 'diem-danh'],
    queryFn: async () =>
      (await api.get<NhanXetGvDto[]>(`/buoi-hoc/${buoiHocId}/diem-danh`)).data,
  })

  /*
    Người đứng lớp của buổi — phiếu chấm dựng từ đây.

    Gọi endpoint riêng thay vì ghép từ `tenGiaoVien` + `tenTroGiangs` của `BuoiHocDto`: hai
    trường đó chỉ có TÊN, mà chấm điểm cần `id`. Ghép ở frontend cũng sai khi buổi dùng giáo
    viên riêng.
  */
  const { data: nguoiDungLop = [] } = useQuery({
    queryKey: ['buoi-hoc', buoiHocId, 'nguoi-dung-lop'],
    queryFn: async () =>
      (await api.get<NguoiDungLopDto[]>(`/buoi-hoc/${buoiHocId}/nguoi-dung-lop`)).data,
    enabled: toiLaHocVien,
  })

  /*
    Tiêu chí nhóm GiangDay. Gọi `/de-cham` — endpoint gác bằng `TieuChiDanhGia.TuLam`, quyền mà
    nhóm "Học viên" ĐƯỢC cấp từ 18/09/2026.

    Trước đó màn này gọi endpoint quản lý danh mục (gác `TieuChiDanhGia.Xem`), học viên nhận
    403, `catch` trả rỗng và phiếu âm thầm rơi về chấm sao — chính là lỗi được báo. Nay không
    `catch` che lỗi nữa: hỏng thì phải thấy, không được im lặng đổi hành vi.
  */
  const { data: tieuChis = [] } = useQuery({
    queryKey: ['tieu-chi-de-cham', 'GiangDay'],
    queryFn: async () =>
      (await api.get<{ id: string; ten: string; moTa: string | null }[]>(
        '/tieu-chi-danh-gia/de-cham', { params: { nhom: 'GiangDay' } })).data,
    enabled: toiLaHocVien,
  })

  // Dùng cờ `cuaToi` của backend chứ không suy từ "danh sách có 1 phần tử": giáo viên đọc
  // được mọi nhận xét, nếu lớp chỉ có một học viên đã gửi thì suy kiểu đó sẽ nạp nhận xét của
  // HỌC VIÊN vào form của GIÁO VIÊN, và bấm Gửi là ghi đè nhầm chủ.
  const cuaToi = ds.find((n) => n.cuaToi) ?? null

  useEffect(() => {
    if (!cuaToi) return
    setNoiDung(cuaToi.noiDung)
    setDiems(Object.fromEntries(
      (cuaToi.diemTieuChis ?? [])
        // Điểm cũ không có người được chấm thì không nạp vào ô nào — nạp bừa vào giáo viên là
        // sửa dữ liệu lịch sử theo phỏng đoán. Vẫn hiện ở phần "điểm đã chấm" bên dưới.
        .filter((d) => d.nguoiDuocChamId !== null)
        .map((d) => [khoa(d.tieuChiId, d.nguoiDuocChamId!), d.diem]),
    ))
  }, [cuaToi])

  const gui = useMutation({
    mutationFn: () =>
      api.post(`/buoi-hoc/${buoiHocId}/nhan-xet`, {
        noiDung: noiDung.trim(),
        // Chỉ gửi khi màn này CÓ tiêu chí và CÓ người đứng lớp: gửi `[]` là lệnh xoá hết điểm,
        // mà nếu danh mục tải lỗi thì học viên sẽ vô tình xoá điểm đã chấm lần trước.
        diemTieuChis: tieuChis.length > 0 && nguoiDungLop.length > 0
          ? Object.entries(diems).map(([k, diem]) => {
              const [nguoiDuocChamId, tieuChiId] = k.split('|')
              return { tieuChiId, diem, nguoiDuocChamId }
            })
          : undefined,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['buoi-hoc', buoiHocId, 'nhan-xet'] })
      setMaLoi(null)
      setDaGui(true)
      window.setTimeout(() => setDaGui(false), 2500)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setDaGui(false)
    },
  })

  const nhanXetGv = diemDanh.filter((d) => d.nhanXet)

  /** Điểm cũ "chấm chung" (trước 18/09) — hiện riêng để không mất dấu dữ liệu thật. */
  const diemChamChung = useMemo(
    () => (cuaToi?.diemTieuChis ?? []).filter((d) => d.nguoiDuocChamId === null),
    [cuaToi],
  )

  const datDiem = (tieuChiId: string, nguoiId: string, d: number) =>
    setDiems((cu) => {
      const k = khoa(tieuChiId, nguoiId)
      const moi = { ...cu }
      // Bấm lại mức đang chọn = bỏ chấm tiêu chí đó cho người đó.
      if (moi[k] === d) delete moi[k]
      else moi[k] = d
      return moi
    })

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {!toiLaHocVien ? (
        /* Giáo viên / trợ giảng / quản trị: KHÔNG hiện form. Trước đây hiện cho mọi người nên
           giáo viên nhập xong mới nhận `KHONG_THUOC_LOP_NAY` — vô lý với người dạy chính lớp.
           Nói luôn chỗ ghi nhận xét của họ thay vì chỉ ẩn đi. */
        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetGiaoVien')}</h3>
            <p className="text-sm text-muted-foreground">{t('nhanXetBuoi.giaiThichChoGv')}</p>
          </CardContent>
        </Card>
      ) : (
      <Card>
        <CardContent className="grid gap-3 pt-6">
          <div className="flex items-center justify-between">
            <h3 className="font-semibold">{t('nhanXetBuoi.cuaToi')}</h3>
            {cuaToi && (
              <span className="text-xs text-muted-foreground">
                {new Date(cuaToi.thoiDiem).toLocaleString('vi-VN')}
              </span>
            )}
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="grid gap-1.5">
            <Label>{t('nhanXetBuoi.noiDung')}</Label>
            <Textarea
              rows={4}
              value={noiDung}
              onChange={(e) => setNoiDung(e.target.value)}
              placeholder={t('nhanXetBuoi.noiDungGoiY')}
            />
          </div>

          {/*
            Chấm RIÊNG từng người đứng lớp, mỗi người một bảng tiêu chí × mức 1–5.

            Dạng bảng thay vì cột sao: với 2 người × N tiêu chí thì xếp dọc làm phiếu dài mấy
            màn hình, còn bảng gọn trong một khối và so sánh được các tiêu chí với nhau.
          */}
          {tieuChis.length === 0 ? (
            <p className="text-xs text-muted-foreground">{t('nhanXetBuoi.khongCoTieuChi')}</p>
          ) : (
            <div className="grid gap-3">
              <Label>{t('nhanXetBuoi.chamNguoiDungLop')}</Label>
              {nguoiDungLop.map((ng) => (
                <div key={ng.id} className="rounded-lg border border-border">
                  <div className="flex items-center gap-2 border-b border-border px-3 py-2">
                    <span className="text-sm font-medium">{ng.hoTen}</span>
                    <Badge variant={ng.vaiTro === 'GiaoVien' ? 'accent' : 'muted'}>
                      {t(`nhanXetBuoi.vaiTro.${ng.vaiTro}`)}
                    </Badge>
                  </div>

                  <table className="w-full text-sm">
                    <tbody>
                      {tieuChis.map((tc) => {
                        const dangChon = diems[khoa(tc.id, ng.id)]
                        return (
                          <tr key={tc.id} className="border-b border-border last:border-0">
                            <td className="px-3 py-1.5">
                              <div>{tc.ten}</div>
                              {tc.moTa && (
                                <div className="text-xs text-muted-foreground">{tc.moTa}</div>
                              )}
                            </td>
                            <td className="px-3 py-1.5">
                              <div className="flex justify-end gap-1">
                                {MUC.map((d) => (
                                  <button
                                    key={d}
                                    type="button"
                                    aria-label={`${ng.hoTen} — ${tc.ten}: ${d}`}
                                    aria-pressed={dangChon === d}
                                    title={t('nhanXetBuoi.boChon')}
                                    onClick={() => datDiem(tc.id, ng.id, d)}
                                    className={
                                      'h-7 w-7 rounded-md border text-xs font-medium '
                                      + 'transition-colors '
                                      + (dangChon !== undefined && d <= dangChon
                                        ? 'border-primary bg-primary text-primary-foreground'
                                        : 'border-border hover:bg-muted')
                                    }
                                  >
                                    {d}
                                  </button>
                                ))}
                              </div>
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              ))}
            </div>
          )}

          {/*
            Điểm đã chấm TRƯỚC 18/09 (chấm chung cho buổi, không rõ của ai) và mức hài lòng cũ.
            Chỉ ĐỌC — quy tắc #1: dữ liệu thật không được biến mất khỏi màn hình chỉ vì thiết kế
            đã đổi. Gửi lại phiếu sẽ thay bằng điểm chấm riêng từng người.
          */}
          {(diemChamChung.length > 0 || cuaToi?.mucHaiLong != null) && (
            <div className="rounded-md border border-dashed border-border p-2 text-xs">
              <div className="font-medium text-muted-foreground">
                {t('nhanXetBuoi.diemDaCham')} — {t('nhanXetBuoi.chamChung')}
              </div>
              <ul className="mt-1 grid gap-0.5 text-muted-foreground">
                {cuaToi?.mucHaiLong != null && (
                  <li>{t('nhanXetBuoi.mucHaiLong')}: {cuaToi.mucHaiLong}/5</li>
                )}
                {diemChamChung.map((d) => (
                  <li key={d.tieuChiId}>{d.tenTieuChi}: {d.diem}/5</li>
                ))}
              </ul>
            </div>
          )}

          <div className="flex items-center justify-end gap-2">
            {daGui && (
              <span className="mr-auto flex items-center gap-1 text-sm text-status-ok">
                <CheckCircle2 className="h-4 w-4" />
                {t('nhanXetBuoi.daGui')}
              </span>
            )}
            <Button
              disabled={gui.isPending || !noiDung.trim()}
              onClick={() =>
                hoi({
                  tieuDe: cuaToi ? t('nhanXetBuoi.capNhat') : t('nhanXetBuoi.gui'),
                  thongDiep: cuaToi
                    ? t('nhanXetBuoi.hoiCapNhat')
                    : t('nhanXetBuoi.hoiGui'),
                  onDongY: () => gui.mutate(),
                })
              }
            >
              {cuaToi ? t('nhanXetBuoi.capNhat') : t('nhanXetBuoi.gui')}
            </Button>
          </div>
        </CardContent>
      </Card>
      )}

      <div className="grid gap-4">
        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetHocVien')}</h3>
            {isLoading ? (
              <TrangTrong thongDiep={t('chung.dangTai')} />
            ) : ds.length === 0 ? (
              <TrangTrong thongDiep={t('nhanXetBuoi.chuaCo')} />
            ) : (
              <ul className="grid gap-3">
                {ds.map((n) => (
                  <li key={n.id} className="rounded-md border border-border p-3">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{n.hoTen}</span>
                      {n.cuaToi && <Badge variant="accent">{t('nhanXetBuoi.cuaToi')}</Badge>}
                      <span className="ml-auto text-xs text-muted-foreground">
                        {new Date(n.thoiDiem).toLocaleString('vi-VN')}
                      </span>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {n.noiDung}
                    </p>

                    {/*
                      Điểm gom theo NGƯỜI được chấm — đọc "cô Lan 5/5, thầy Hoà 2/5" mới có
                      nghĩa; liệt kê phẳng theo tiêu chí thì không biết điểm của ai.
                      Điểm cũ (`nguoiDuocChamId = null`) gom vào nhóm "chấm chung".
                    */}
                    {n.diemTieuChis.length > 0 && (
                      <div className="mt-2 grid gap-1">
                        {Object.entries(
                          n.diemTieuChis.reduce<Record<string, DiemTieuChiDto[]>>((acc, d) => {
                            const ten = d.tenNguoiDuocCham ?? t('nhanXetBuoi.chamChung')
                            ;(acc[ten] ??= []).push(d)
                            return acc
                          }, {}),
                        ).map(([ten, dsDiem]) => (
                          <div key={ten} className="text-xs">
                            <span className="font-medium">{ten}</span>
                            <span className="text-muted-foreground">
                              {' — '}
                              {dsDiem.map((d) => `${d.tenTieuChi} ${d.diem}/5`).join(' · ')}
                            </span>
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Mức hài lòng cũ: chỉ hiện khi có, phiếu mới không chấm nữa. */}
                    {n.mucHaiLong !== null && (
                      <div className="mt-1 text-xs text-muted-foreground">
                        {t('nhanXetBuoi.mucHaiLong')}: {n.mucHaiLong}/5
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent className="grid gap-2 pt-6">
            <h3 className="font-semibold">{t('nhanXetBuoi.nhanXetGiaoVien')}</h3>
            <p className="text-xs text-muted-foreground">{t('nhanXetBuoi.ghiONhanXetGv')}</p>
            {nhanXetGv.length === 0 ? (
              <TrangTrong thongDiep={t('nhanXetBuoi.chuaCoNhanXetGv')} />
            ) : (
              <ul className="grid gap-3">
                {nhanXetGv.map((d) => (
                  <li key={d.hoTen} className="rounded-md border border-border p-3">
                    <span className="text-sm font-medium">{d.hoTen}</span>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {d.nhanXet}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      {hop}
    </div>
  )
}
