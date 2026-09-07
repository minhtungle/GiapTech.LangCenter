import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { useXacNhan } from '@/lib/xacNhan'
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
      setMaLoi(layMaLoi(e))
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
                          disabled={!canLyDo}
                          placeholder={canLyDo ? t('diemDanh.lyDoVang') : ''}
                          className="h-8"
                        />
                      </Td>
                      <Td>
                        {/* Nhận xét của GV về học viên trong buổi này — khác lý do vắng: lý do
                            nói vì sao không có mặt, nhận xét nói về việc học. */}
                        <Input
                          aria-label={`${t('diemDanh.nhanXetGv')} — ${d.hoTen}`}
                          value={v?.nhanXet ?? ''}
                          onChange={(e) => doi(d.hocVienId, { nhanXet: e.target.value })}
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

        <div className="flex items-center justify-end gap-2">
          {daLuu && (
            <span className="mr-auto flex items-center gap-1 text-sm text-status-ok">
              <CheckCircle2 className="h-4 w-4" />
              {t('diemDanh.daLuu')}
            </span>
          )}
          <Button
            variant="outline"
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
            disabled={luu.isPending || ds.length === 0}
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
