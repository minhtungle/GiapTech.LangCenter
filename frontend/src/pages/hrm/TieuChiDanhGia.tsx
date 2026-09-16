import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'

export type NhomTieuChi = 'KinhDoanh' | 'GiangDay'

export const CAC_NHOM: NhomTieuChi[] = ['KinhDoanh', 'GiangDay']

export interface TieuChiDto {
  id: string
  ten: string
  moTa: string | null
  nhom: NhomTieuChi
  thuTu: number
  dangDung: boolean
  soLanDaCham: number
}

/**
 * FR-29 — danh mục **tiêu chí đánh giá** thang 5 (16/09/2026).
 *
 * Theo yêu cầu chủ sản phẩm: *"có thể tạo riêng 1 module các tiêu chí đánh giá trong HRM cho
 * nhân viên kinh doanh và giáo viên/trợ giảng"*.
 *
 * ## Hai nhóm, hai người chấm khác nhau
 *
 * | Nhóm | Ai chấm | Ở đâu |
 * |---|---|---|
 * | Kinh doanh | **quản lý** | màn Thống kê nhân sự, theo kỳ tháng |
 * | Giảng dạy | **học viên** | ô nhận xét trong từng buổi học |
 *
 * Chia nhóm chứ không dùng một danh mục chung: tiêu chí "Chủ động tìm khách" không có nghĩa với
 * giáo viên, mà hiện nó trên phiếu chấm buổi học chỉ làm học viên bỏ trống.
 *
 * ## Không có nút Xoá — cố ý
 *
 * Tiêu chí đã có điểm mà xoá thì **mọi kỳ đã chấm đổi số một cách im lặng** (quy tắc #1). Muốn
 * bỏ thì bỏ tích "Còn dùng": phiếu mới không hiện nữa, phiếu cũ vẫn đọc được. Backend cũng
 * không mở endpoint DELETE.
 */
export default function TieuChiDanhGia() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TieuChiDto | null>(null)
  const [nhom, setNhom] = useState<NhomTieuChi>('KinhDoanh')
  const [dangDung, setDangDung] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiForm, setMaLoiForm] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['tieu-chi-danh-gia'],
    queryFn: async () => (await api.get<TieuChiDto[]>('/tieu-chi-danh-gia')).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['tieu-chi-danh-gia'] })
    // Màn thống kê dựng cột điểm từ danh mục này — thêm tiêu chí phải thấy ngay ở đó.
    void qc.invalidateQueries({ queryKey: ['thong-ke-nhan-su'] })
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        ten: String(fd.get('ten')).trim(),
        moTa: String(fd.get('moTa') ?? '').trim() || null,
        nhom,
        thuTu: Number(fd.get('thuTu') ?? 0),
        dangDung,
      }
      if (dangSua) await api.put(`/tieu-chi-danh-gia/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/tieu-chi-danh-gia', than)
    },
    onSuccess: () => { lamMoi(); setMoForm(false); setMaLoiForm(null) },
    onError: (e) => setMaLoiForm(layMaLoi(e)),
  })

  const mo = (tc: TieuChiDto | null) => {
    setDangSua(tc)
    setNhom(tc?.nhom ?? 'KinhDoanh')
    setDangDung(tc?.dangDung ?? true)
    setMaLoi(null)
    setMaLoiForm(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{t('chucNang.TieuChiDanhGia')}</h2>
        {coQuyen('TieuChiDanhGia', 'Them') && (
          <Button className="ml-auto" onClick={() => mo(null)}>
            <Plus className="h-4 w-4" />
            {t('tieuChi.them')}
          </Button>
        )}
      </div>

      <p className="text-sm text-muted-foreground">{t('tieuChi.giaiThich')}</p>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {/* Nhóm theo NHÓM tiêu chí: hai bảng riêng đọc dễ hơn một bảng có cột "nhóm" lặp lại. */}
      {CAC_NHOM.map((n) => {
        const cua = ds.filter((x) => x.nhom === n)
        return (
          <Card key={n}>
            <CardContent className="pt-6">
              <div className="mb-3 flex flex-wrap items-baseline gap-2">
                <h3 className="font-semibold">{t(`tieuChi.nhom.${n}`)}</h3>
                <span className="text-xs text-muted-foreground">
                  {t(`tieuChi.nhomMoTa.${n}`)}
                </span>
              </div>

              {isLoading ? (
                <TrangTrong thongDiep={t('chung.dangTai')} />
              ) : cua.length === 0 ? (
                <TrangTrong thongDiep={t('tieuChi.chuaCo')} />
              ) : (
                <Table>
                  <thead>
                    <tr>
                      <Th>{t('tieuChi.ten')}</Th>
                      <Th>{t('chung.ghiChu')}</Th>
                      <Th className="text-right">{t('tieuChi.soLanDaCham')}</Th>
                      <Th>{t('chucVu.trangThai')}</Th>
                      <Th />
                    </tr>
                  </thead>
                  <tbody>
                    {cua.map((tc) => (
                      <tr key={tc.id} className="hover:bg-muted/40">
                        <Td className="font-medium">{tc.ten}</Td>
                        <Td className="text-muted-foreground">{tc.moTa ?? '—'}</Td>
                        <Td className="text-right">
                          <Badge variant="muted">{tc.soLanDaCham}</Badge>
                        </Td>
                        <Td>
                          <Badge variant={tc.dangDung ? 'ok' : 'muted'}>
                            {t(tc.dangDung ? 'chucVu.dangDung' : 'chucVu.ngungDung')}
                          </Badge>
                        </Td>
                        <Td>
                          <div className="flex justify-end">
                            <MenuThaoTac
                              muc={[
                                {
                                  nhan: t('chung.sua'),
                                  icon: Pencil,
                                  an: !coQuyen('TieuChiDanhGia', 'Sua'),
                                  onChon: () => mo(tc),
                                },
                              ]}
                            />
                          </div>
                        </Td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </CardContent>
          </Card>
        )
      })}

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('tieuChi.sua') : t('tieuChi.them')}
        moTa={dangSua?.ten}
        rong="sm"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('tieuChi.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="ten">{t('tieuChi.ten')} *</Label>
            <Input id="ten" name="ten" required maxLength={200} defaultValue={dangSua?.ten ?? ''} />
          </div>

          <div>
            <Label htmlFor="nhom-tc">{t('tieuChi.nhomLabel')} *</Label>
            <SelectTimKiem
              id="nhom-tc"
              giaTri={nhom}
              luaChon={CAC_NHOM.map((n) => ({ giaTri: n, nhan: t(`tieuChi.nhom.${n}`) }))}
              onDoi={(v) => setNhom((v as NhomTieuChi) ?? 'KinhDoanh')}
            />
            {/*
              Tiêu chí ĐÃ CÓ ĐIỂM thì backend chặn đổi nhóm — nói trước ở đây thay vì để người
              dùng lưu rồi mới nhận lỗi.
            */}
            {dangSua && dangSua.soLanDaCham > 0 && (
              <p className="mt-1 text-xs text-muted-foreground">{t('tieuChi.khongDoiNhom')}</p>
            )}
          </div>

          <div>
            <Label htmlFor="thuTu">{t('chucVu.thuTu')}</Label>
            <Input id="thuTu" name="thuTu" type="number" min={0}
                   defaultValue={dangSua?.thuTu ?? 0} />
          </div>

          <div>
            <Label htmlFor="moTa">{t('tieuChi.moTa')}</Label>
            <Textarea id="moTa" name="moTa" rows={2} maxLength={500}
                      defaultValue={dangSua?.moTa ?? ''} />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={dangDung}
              onChange={(e) => setDangDung(e.target.checked)}
              className="h-4 w-4 accent-[hsl(var(--primary))]"
            />
            {t('chucVu.conDung')}
          </label>
          <p className="text-xs text-muted-foreground">{t('tieuChi.ngungDungGoiY')}</p>

          {maLoiForm && <CanhBaoLoi>{t(`loi.${maLoiForm}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>{t('chung.luu')}</Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
