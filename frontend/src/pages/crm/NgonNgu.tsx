import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'

export interface NgonNguDto {
  id: string
  ten: string
  thuTu: number
  dangDung: boolean
  soKhoaHoc: number
  soSanPham: number
}

/**
 * Danh mục ngôn ngữ giảng dạy (11/10/2026).
 *
 * Gắn vào khoá học và sản phẩm, để từ đó lọc được khách hàng và doanh thu theo ngôn ngữ —
 * câu hỏi "tiếng Đức năm nay bán được bao nhiêu" trước đây phải tự nhẩm theo tên khoá.
 *
 * **Ngôn ngữ KHÔNG cấp quyền gì**, nó chỉ là nhãn phân loại — cùng tinh thần với chức vụ.
 */
export default function NgonNgu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<NgonNguDto | null>(null)
  const [dangDung, setDangDung] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['ngon-ngu'],
    queryFn: async () => (await api.get<NgonNguDto[]>('/ngon-ngu')).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['ngon-ngu'] })
    // Khoá học và sản phẩm hiện tên ngôn ngữ — đổi tên phải thấy ngay ở đó.
    void qc.invalidateQueries({ queryKey: ['khoa-hoc'] })
    void qc.invalidateQueries({ queryKey: ['san-pham'] })
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        ten: String(fd.get('ten')).trim(),
        thuTu: Number(fd.get('thuTu') ?? 0),
        dangDung,
      }
      if (dangSua) await api.put(`/ngon-ngu/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/ngon-ngu', than)
    },
    onSuccess: () => {
      lamMoi()
      setMoForm(false)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/ngon-ngu/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const mo = (nn: NgonNguDto | null) => {
    setDangSua(nn)
    setDangDung(nn?.dangDung ?? true)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{t('menu.ngonNgu')}</h2>
        {coQuyen('NgonNgu', 'Them') && (
          <Button className="ml-auto" onClick={() => mo(null)}>
            <Plus className="h-4 w-4" />
            {t('danhMucNgonNgu.them')}
          </Button>
        )}
      </div>

      <p className="text-sm text-muted-foreground">{t('danhMucNgonNgu.giaiThich')}</p>

      {maLoi && !moForm && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : ds.length === 0 ? (
            <TrangTrong thongDiep={t('danhMucNgonNgu.chuaCo')} />
          ) : (
            <Table rongToiThieu="">
              <thead>
                <tr>
                  <Th>{t('danhMucNgonNgu.ten')}</Th>
                  <Th className="text-right">{t('danhMucNgonNgu.thuTu')}</Th>
                  <Th className="text-right">{t('danhMucNgonNgu.soKhoaHoc')}</Th>
                  <Th className="text-right">{t('danhMucNgonNgu.soSanPham')}</Th>
                  <Th>{t('danhMucNgonNgu.trangThai')}</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {ds.map((nn) => {
                  // Gộp hai con số: ràng buộc xoá là "còn thứ gì đang gắn", không phân biệt
                  // khoá học hay sản phẩm.
                  const dangDung2 = nn.soKhoaHoc + nn.soSanPham
                  return (
                    <tr key={nn.id} className="hover:bg-muted/40">
                      <Td className="font-medium">{nn.ten}</Td>
                      <Td className="text-right text-muted-foreground">{nn.thuTu}</Td>
                      <Td className="text-right text-muted-foreground">{nn.soKhoaHoc}</Td>
                      <Td className="text-right text-muted-foreground">{nn.soSanPham}</Td>
                      <Td>
                        {nn.dangDung ? (
                          <Badge variant="ok">{t('danhMucNgonNgu.dangDung')}</Badge>
                        ) : (
                          <Badge variant="muted">{t('danhMucNgonNgu.ngungDung')}</Badge>
                        )}
                      </Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('NgonNgu', 'Sua'),
                                onChon: () => mo(nn),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                an: !coQuyen('NgonNgu', 'Xoa'),
                                // Hiện kèm lý do thay vì ẩn — xem `MucThaoTac.khoa`.
                                khoa:
                                  dangDung2 > 0
                                    ? t('danhMucNgonNgu.khongXoaDuocDangDung', { so: dangDung2 })
                                    : undefined,
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('danhMucNgonNgu.hoiXoa', { ten: nn.ten }),
                                    nhanDongY: t('chung.xoa'),
                                    nguyHiem: true,
                                    onDongY: () => xoa.mutate(nn.id),
                                  }),
                              },
                            ]}
                          />
                        </div>
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('danhMucNgonNgu.sua') : t('danhMucNgonNgu.them')}
        moTa={dangSua?.ten}
        rong="sm"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            luu.mutate(fd)
          }}
        >
          <div>
            <Label htmlFor="ten">{t('danhMucNgonNgu.ten')} *</Label>
            <Input id="ten" name="ten" required defaultValue={dangSua?.ten ?? ''} autoFocus />
          </div>

          <div>
            <Label htmlFor="thuTu">{t('danhMucNgonNgu.thuTu')}</Label>
            <Input
              id="thuTu"
              name="thuTu"
              type="number"
              min={0}
              defaultValue={dangSua?.thuTu ?? 0}
            />
            <p className="mt-1 text-xs text-muted-foreground">{t('danhMucNgonNgu.thuTuGoiY')}</p>
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="h-4 w-4 accent-[hsl(var(--primary))]"
              checked={dangDung}
              onChange={(e) => setDangDung(e.target.checked)}
            />
            {t('danhMucNgonNgu.conDung')}
          </label>
          <p className="text-xs text-muted-foreground">{t('danhMucNgonNgu.conDungGoiY')}</p>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" dangXuLy={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
