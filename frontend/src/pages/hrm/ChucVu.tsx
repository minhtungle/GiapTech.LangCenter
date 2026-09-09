import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2, Users } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'

interface ChucVuDto {
  id: string
  ten: string
  moTa: string | null
  thuTu: number
  dangDung: boolean
  soNhanSu: number
}

/**
 * FR-24 — danh mục chức vụ: "Ban quản lý", "Trưởng phòng", "Kế toán"…
 *
 * **Khác `LoaiNguoiDung`, đừng nhầm.** Đây là *chức danh* do admin tự quản; `LoaiNguoiDung`
 * (Nhân viên · Giáo viên · Trợ giảng · Học viên) là *loại nghiệp vụ* cố định trong code, quyết
 * định ai gán được vào lớp và hồ sơ con nào áp dụng.
 *
 * Chủ sản phẩm đề xuất thêm "ban quản lý" vào danh sách vai trò (09/09/2026) — làm ở đây chứ
 * không thêm vào enum, vì enum đó load-bearing ở 6+ chỗ LMS/CRM.
 *
 * **Chức vụ KHÔNG cấp quyền gì** — quyền vẫn theo phân quyền tài khoản (quy tắc #9).
 */
export default function ChucVu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<ChucVuDto | null>(null)
  const [dangDung, setDangDung] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['chuc-vu'],
    queryFn: async () => (await api.get<ChucVuDto[]>('/chuc-vu')).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['chuc-vu'] })
    // Danh sách nhân sự hiện tên chức vụ — đổi tên chức vụ phải thấy ngay ở đó.
    void qc.invalidateQueries({ queryKey: ['/nhan-su'] })
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        ten: String(fd.get('ten')).trim(),
        moTa: String(fd.get('moTa') ?? '').trim() || null,
        thuTu: Number(fd.get('thuTu') ?? 0),
        dangDung,
      }
      if (dangSua) await api.put(`/chuc-vu/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/chuc-vu', than)
    },
    onSuccess: () => {
      lamMoi()
      setMoForm(false)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/chuc-vu/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const mo = (cv: ChucVuDto | null) => {
    setDangSua(cv)
    setDangDung(cv?.dangDung ?? true)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{t('menu.chucVu')}</h2>
        {coQuyen('ChucVu', 'Them') && (
          <Button className="ml-auto" onClick={() => mo(null)}>
            <Plus className="h-4 w-4" />
            {t('chucVu.them')}
          </Button>
        )}
      </div>

      {/* Nói rõ ngay đầu màn để không ai tưởng chức vụ cấp quyền. */}
      <p className="text-sm text-muted-foreground">{t('chucVu.giaiThich')}</p>

      {maLoi && !moForm && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : ds.length === 0 ? (
            <TrangTrong thongDiep={t('chucVu.chuaCo')} />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('chucVu.ten')}</Th>
                  <Th>{t('chung.ghiChu')}</Th>
                  <Th className="text-right">{t('chucVu.soNhanSu')}</Th>
                  <Th>{t('chucVu.trangThai')}</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {ds.map((cv) => (
                  <tr key={cv.id} className="hover:bg-muted/40">
                    <Td className="font-medium">{cv.ten}</Td>
                    <Td className="text-muted-foreground">{cv.moTa ?? '—'}</Td>
                    <Td className="text-right">
                      <Badge variant="muted">
                        <Users className="mr-1 h-3 w-3" />
                        {cv.soNhanSu}
                      </Badge>
                    </Td>
                    <Td>
                      {cv.dangDung ? (
                        <Badge variant="ok">{t('chucVu.dangDung')}</Badge>
                      ) : (
                        <Badge variant="muted">{t('chucVu.ngungDung')}</Badge>
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
                              an: !coQuyen('ChucVu', 'Sua'),
                              onChon: () => mo(cv),
                            },
                            {
                              nhan: t('chung.xoa'),
                              icon: Trash2,
                              nguyHiem: true,
                              ngatNhom: true,
                              an: !coQuyen('ChucVu', 'Xoa'),
                              onChon: () =>
                                hoi({
                                  tieuDe: t('chung.xacNhanXoa'),
                                  thongDiep: t('chucVu.hoiXoa', { ten: cv.ten }),
                                  nhanDongY: t('chung.xoa'),
                                  nguyHiem: true,
                                  onDongY: () => xoa.mutate(cv.id),
                                }),
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

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('chucVu.sua') : t('chucVu.them')}
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
              thongDiep: t('chucVu.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="ten">{t('chucVu.ten')} *</Label>
            <Input id="ten" name="ten" required defaultValue={dangSua?.ten ?? ''} />
          </div>

          <div>
            <Label htmlFor="thuTu">{t('chucVu.thuTu')}</Label>
            <Input
              id="thuTu"
              name="thuTu"
              type="number"
              min={0}
              defaultValue={dangSua?.thuTu ?? 0}
            />
          </div>

          <div>
            <Label htmlFor="moTa">{t('chung.ghiChu')}</Label>
            <Textarea id="moTa" name="moTa" rows={2} defaultValue={dangSua?.moTa ?? ''} />
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
          {/* Nói rõ vì sao có ô này: xoá chức vụ đang có người giữ bị chặn. */}
          <p className="text-xs text-muted-foreground">{t('chucVu.ngungDungGoiY')}</p>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
