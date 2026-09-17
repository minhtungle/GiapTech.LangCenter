import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2 } from 'lucide-react'
import { api, layDuLieuLoi, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { useXacNhan } from '@/lib/xacNhan'
import { useQuyen } from '@/lib/quyen'
import { type BuoiHocDto, gioVN } from './buoiHocTypes'

type TrangThaiDiemDanh = 'CoMat' | 'DiMuon' | 'Vang' | 'VangCoPhep'

const CAC_TRANG_THAI: TrangThaiDiemDanh[] = ['CoMat', 'DiMuon', 'Vang', 'VangCoPhep']

interface DiemDanhDto {
  hocVienId: string
  hoTen: string
  trangThaiTuKhai: TrangThaiDiemDanh | null
  thoiDiemTuCheckIn: string | null
  trangThaiChinhThuc: TrangThaiDiemDanh
  nguonGhiNhan: string
  lyDoVang: string | null
  nhanXet: string | null
  giaoVienSuaKhacTuKhai: boolean
  daGhiNhan: boolean
}

/**
 * Bảng điểm danh một buổi — dùng ở CẢ hai chỗ: modal mở từ danh sách buổi, và tab trong view
 * chi tiết buổi học.
 *
 * Tách ra file riêng (07/09/2026) vì trước đó là `function` local trong `LichVaDiemDanh.tsx`
 * và hard-code `Modal`, nên không tái dùng làm tab được. Nay bọc `KhungNoiDung` như các
 * component khác của dự án.
 */
export function BangDiemDanh({
  buoi,
  onDong,
  onXong,
  nhung,
}: {
  buoi: BuoiHocDto
  onDong: () => void
  onXong: () => void
  /** true = đang là tab trong view chi tiết buổi, không bọc Modal. */
  nhung?: boolean
}) {
  const { t } = useTranslation()
  const { hoi, hop } = useXacNhan()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()

  /*
    CHỈ ĐỌC khi không có `DiemDanh.Sua` (17/09/2026).

    Bảng này mở được từ hai chỗ: menu ở màn lịch, và tab "Điểm danh" của view chi tiết buổi. Tab
    đó gác bằng `DiemDanh` với thao tác mặc định là `Xem` — quyền mà HỌC VIÊN CÓ — nên học viên
    mở được bảng và thấy đủ ô chọn trạng thái của **cả lớp**, nút "Lưu điểm danh", "Chốt buổi".

    Backend chặn hết (403, đã thử bằng `hv1`) nên không rò rỉ và không ghi được gì; nhưng người
    dùng bấm vào chỉ nhận lỗi đỏ mà không hiểu vì sao — và tệ hơn, họ tưởng mình vừa làm hỏng
    dữ liệu của lớp.

    Gác ở ĐÂY chứ không chỉ ở chỗ gọi: bảng có hai lối vào, gác từng lối là hai chỗ để quên.
  */
  const duocSua = coQuyen('DiemDanh', 'Sua')
  const duocChot = coQuyen('DiemDanh', 'Chot')
  const [sua, setSua] = useState<
    Record<string, { tt: TrangThaiDiemDanh; lyDo: string; nhanXet: string }>
  >({})
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  const { data: ds = [] } = useQuery({
    queryKey: ['buoi-hoc', buoi.id, 'diem-danh'],
    queryFn: async () => (await api.get<DiemDanhDto[]>(`/buoi-hoc/${buoi.id}/diem-danh`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['buoi-hoc', buoi.id, 'diem-danh'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc', buoi.lopHocId, 'buoi-hoc'] })
    void qc.invalidateQueries({ queryKey: ['buoi-hoc', buoi.id] })
    onXong()
  }

  /** Trộn dữ liệu server với phần người dùng vừa sửa — sửa chưa lưu không bị mất khi refetch. */
  const hienTai = useMemo(() => {
    const m: Record<string, { tt: TrangThaiDiemDanh; lyDo: string; nhanXet: string }> = {}
    for (const d of ds) {
      m[d.hocVienId] = sua[d.hocVienId] ?? {
        tt: d.trangThaiChinhThuc,
        lyDo: d.lyDoVang ?? '',
        nhanXet: d.nhanXet ?? '',
      }
    }
    return m
  }, [ds, sua])

  /*
    Ai đang VẮNG mà CHƯA có lý do — backend bắt buộc (`THIEU_LY_DO_VANG`).

    Phải tính ở đây để chặn TRƯỚC khi gửi (18/09/2026). Trước đó bấm Lưu là nhận 400 kèm
    `DU_LIEU_KHONG_HOP_LE`, mà `layMaLoi` chỉ đọc `errorCode` ở tầng ngoài nên người dùng thấy
    đúng một câu **"Dữ liệu nhập vào chưa hợp lệ"** — không biết thiếu ở đâu, không biết phải
    sửa gì. Mã cụ thể `THIEU_LY_DO_VANG` nằm trong `duLieu.truong` và bị bỏ đi.

    Người dùng báo: *"lưu điểm danh đang lỗi không lưu được"*. Tái hiện đúng vậy: lớp 6 học
    viên, mặc định ai cũng `Vắng`, đổi 2 người thành Có mặt rồi bấm Lưu → 400, 4 dòng còn lại
    thiếu lý do mà màn hình không chỉ ra dòng nào.
  */
  const thieuLyDo = useMemo(
    () => ds.filter((d) => {
      const h = hienTai[d.hocVienId]
      return (h.tt === 'Vang' || h.tt === 'VangCoPhep') && !h.lyDo.trim()
    }),
    [ds, hienTai],
  )

  const luu = useMutation({
    mutationFn: () =>
      api.post(`/buoi-hoc/${buoi.id}/diem-danh`, {
        danhSach: ds.map((d) => ({
          hocVienId: d.hocVienId,
          trangThai: hienTai[d.hocVienId].tt,
          lyDoVang: hienTai[d.hocVienId].lyDo || null,
          nhanXet: hienTai[d.hocVienId].nhanXet,
        })),
      }),
    onSuccess: () => {
      lamMoi()
      setSua({})
      setMaLoi(null)
      setDaLuu(true)
      window.setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => {
      /*
        Lỗi validation theo TỪNG DÒNG đến trong `duLieu.truong`, ví dụ
        `{"DanhSach[2]":["THIEU_LY_DO_VANG"]}`. `layMaLoi` chỉ đọc `errorCode` ở tầng ngoài
        (`DU_LIEU_KHONG_HOP_LE`) nên người dùng thấy đúng một câu "Dữ liệu nhập vào chưa hợp
        lệ" — biết là sai mà không biết sai gì.

        Lấy mã cụ thể ĐẦU TIÊN để hiện thay: các mã này đều đã có bản dịch (`i18n.ts`). Chỉ lấy
        một mã, không ghép nhiều: một lệnh lưu thường sai cùng một kiểu ở nhiều dòng, và dòng
        cảnh báo phía trên đã liệt kê đủ tên người.
      */
      const truong = layDuLieuLoi(e)?.truong
      const maCuThe = truong && typeof truong === 'object'
        ? Object.values(truong as Record<string, unknown>)
            .flatMap((v) => (Array.isArray(v) ? v : [v]))
            .find((v): v is string => typeof v === 'string')
        : undefined

      setMaLoi(maCuThe ?? layMaLoi(e))
      setDaLuu(false)
    },
  })

  const chot = useMutation({
    mutationFn: () => api.post(`/buoi-hoc/${buoi.id}/chot`),
    onSuccess: () => {
      lamMoi()
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const doi = (
    id: string,
    phan: Partial<{ tt: TrangThaiDiemDanh; lyDo: string; nhanXet: string }>,
  ) =>
    setSua((cu) => ({
      ...cu,
      [id]: { ...(hienTai[id] ?? { tt: 'Vang' as const, lyDo: '', nhanXet: '' }), ...phan },
    }))

  return (
    <KhungNoiDung
      nhung={nhung}
      onDong={onDong}
      tieuDe={`${t('diemDanh.tieuDe')} — ${t('buoiHoc.thuTu')} ${buoi.thuTu} · ${gioVN(buoi.batDau)}`}
    >
      <div className="grid gap-4">
        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {buoi.trangThai === 'DaHoanThanh' && (
          <p className="text-sm text-muted-foreground">{t('diemDanh.buoiDaChot')}</p>
        )}

        {ds.length === 0 ? (
          <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
        ) : (
          <div className="max-h-[26rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th>{t('diemDanh.hocVien')}</Th>
                  <Th>{t('diemDanh.tuKhai')}</Th>
                  <Th className="w-40">{t('diemDanh.chinhThuc')}</Th>
                  <Th>{t('diemDanh.lyDoVang')}</Th>
                  <Th>{t('diemDanh.nhanXetGv')}</Th>
                </tr>
              </thead>
              <tbody>
                {ds.map((d) => {
                  const v = hienTai[d.hocVienId]
                  const canLyDo = v?.tt === 'Vang' || v?.tt === 'VangCoPhep'
                  return (
                    <tr key={d.hocVienId} className="hover:bg-muted/40">
                      <Td className="font-medium">{d.hoTen}</Td>
                      <Td className="text-muted-foreground">
                        {d.trangThaiTuKhai ? (
                          <>
                            {t(`trangThaiDiemDanh.${d.trangThaiTuKhai}`)}
                            {d.giaoVienSuaKhacTuKhai && (
                              <Badge variant="loi" className="ml-2">
                                {t('diemDanh.khacTuKhai')}
                              </Badge>
                            )}
                          </>
                        ) : (
                          '—'
                        )}
                      </Td>
                      <Td>
                        <select
                          aria-label={`${t('diemDanh.chinhThuc')} — ${d.hoTen}`}
                          value={v?.tt ?? 'Vang'}
                          onChange={(e) =>
                            doi(d.hocVienId, { tt: e.target.value as TrangThaiDiemDanh })
                          }
                          disabled={!duocSua}
                          className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm"
                        >
                          {CAC_TRANG_THAI.map((tt) => (
                            <option key={tt} value={tt}>
                              {t(`trangThaiDiemDanh.${tt}`)}
                            </option>
                          ))}
                        </select>
                      </Td>
                      <Td>
                        <Input
                          aria-label={`${t('diemDanh.lyDoVang')} — ${d.hoTen}`}
                          value={v?.lyDo ?? ''}
                          onChange={(e) => doi(d.hocVienId, { lyDo: e.target.value })}
                          disabled={!duocSua || !canLyDo}
                          placeholder={canLyDo ? t('diemDanh.lyDoVang') : ''}
                          /* Viền đỏ ở ĐÚNG ô còn thiếu — dòng cảnh báo phía dưới nói "ai",
                             viền nói "gõ vào đâu". Chỉ tô khi người dùng sửa được. */
                          aria-invalid={duocSua && canLyDo && !v?.lyDo.trim() ? true : undefined}
                          className={
                            'h-8 '
                            + (duocSua && canLyDo && !v?.lyDo.trim()
                              ? 'border-status-loi focus-visible:ring-status-loi'
                              : '')
                          }
                        />
                      </Td>
                      <Td>
                        {/* Nhận xét của GV về học viên trong buổi này — khác lý do vắng: lý do
                            nói vì sao không có mặt, nhận xét nói về việc học. */}
                        <Input
                          aria-label={`${t('diemDanh.nhanXetGv')} — ${d.hoTen}`}
                          value={v?.nhanXet ?? ''}
                          onChange={(e) => doi(d.hocVienId, { nhanXet: e.target.value })}
                          disabled={!duocSua}
                          placeholder={t('diemDanh.nhanXetGoiY')}
                          className="h-8"
                        />
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </Table>
          </div>
        )}

        <p className="text-xs text-muted-foreground">{t('buoiHoc.chotBuoiGoiY')}</p>

        {/*
          Nói RÕ ai đang thiếu lý do, ngay trên màn, trước khi người dùng bấm Lưu.

          Dùng `CanhBaoLoi` cùng chỗ với lỗi API để mắt chỉ phải nhìn một nơi. Liệt kê TÊN chứ
          không chỉ đếm: lớp 20 người thì "còn 7 người thiếu lý do" vẫn buộc dò từng dòng.
        */}
        {duocSua && thieuLyDo.length > 0 && (
          <CanhBaoLoi>
            {t('diemDanh.thieuLyDoVang', {
              ten: thieuLyDo.map((d) => d.hoTen).join(', '),
              soLuong: thieuLyDo.length,
            })}
          </CanhBaoLoi>
        )}

        <div className="flex items-center justify-end gap-2">
          {daLuu && (
            <span className="mr-auto flex items-center gap-1 text-sm text-status-ok">
              <CheckCircle2 className="h-4 w-4" />
              {t('diemDanh.daLuu')}
            </span>
          )}
          {/*
            Người chỉ có quyền XEM không thấy hai nút này — bấm vào chỉ nhận 403. Thay bằng một
            dòng nói rõ vì sao, để họ không tưởng màn bị lỗi.
          */}
          {!duocSua && !duocChot && (
            <span className="mr-auto text-sm text-muted-foreground">
              {t('diemDanh.chiXem')}
            </span>
          )}
          <Button
            variant="outline"
            className={duocChot ? '' : 'hidden'}
            disabled={chot.isPending || buoi.trangThai === 'DaHoanThanh'}
            onClick={() =>
              hoi({
                tieuDe: t('buoiHoc.chotBuoi'),
                thongDiep: t('buoiHoc.hoiChotBuoi'),
                nguyHiem: true,
                onDongY: () => chot.mutate(),
              })
            }
          >
            {t('buoiHoc.chotBuoi')}
          </Button>
          <Button
            className={duocSua ? '' : 'hidden'}
            /*
              Chặn ngay ở nút khi còn dòng vắng thiếu lý do — backend sẽ từ chối cả lệnh, nên
              cho bấm chỉ để nhận 400 là bắt người dùng đi một vòng vô nghĩa. `title` nói lý do
              nút bị mờ, nếu không thì nút disabled trông như màn bị treo.
            */
            disabled={luu.isPending || ds.length === 0 || thieuLyDo.length > 0}
            title={thieuLyDo.length > 0 ? t('diemDanh.thieuLyDoNgan') : undefined}
            onClick={() =>
              hoi({
                tieuDe: t('chung.xacNhanLuu'),
                thongDiep: t('diemDanh.hoiLuu', { soLuong: ds.length }),
                onDongY: () => luu.mutate(),
              })
            }
          >
            {t('diemDanh.luu')}
          </Button>
        </div>
      </div>

      {hop}
    </KhungNoiDung>
  )
}
