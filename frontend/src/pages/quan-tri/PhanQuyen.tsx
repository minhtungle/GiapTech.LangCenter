import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import type { DanhMucDto, QuyenDto } from './phan-quyen/kieu'
import { demTheoChucNang } from './phan-quyen/kieu'

/**
 * FR-05 — danh sách nhóm quyền.
 *
 * Ma trận quyền **không** ở đây nữa (đổi 14/09/2026): nó chuyển sang trang riêng
 * `/quan-tri/phan-quyen/:id`. Lý do đầy đủ trong `phan-quyen/ChiTietQuyen.tsx` — tóm lại là
 * 30 chức năng × tới 7 thao tác không nhồi vừa một modal, và người dùng cần gửi được link
 * tới đúng nhóm quyền đang bàn.
 *
 * Trang này chỉ còn: liệt kê, **tạo nhóm mới** (một ô tên → vẫn là modal, đúng quy ước
 * "thêm/cập nhật không cần chuyển view thì dùng modal"), và xoá.
 */
export default function PhanQuyen() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { coQuyen } = useQuyen()
  const [moForm, setMoForm] = useState(false)
  const [tenQuyen, setTenQuyen] = useState('')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)
  const [xoaCho, setXoaCho] = useState<QuyenDto | null>(null)

  const { data: danhMuc } = useQuery({
    queryKey: ['quyen-danh-muc'],
    queryFn: async () => (await api.get<DanhMucDto>('/quyen/danh-muc')).data,
  })

  const { data: quyens, isLoading } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenDto[]>('/quyen')).data,
  })

  /**
   * Tạo nhóm RỖNG rồi chuyển sang trang cấu hình.
   *
   * Không cho tick quyền ngay trong modal tạo: sẽ lại là cái modal khổng lồ vừa bỏ đi. Hai
   * bước rõ ràng hơn — đặt tên, rồi cấu hình (có mẫu vai trò để không phải tick từ số không).
   */
  const tao = useMutation({
    mutationFn: async () => {
      const res = await api.post<string>('/quyen', { tenQuyen, moTa: null, chucNangs: [] })
      return res.data
    },
    onSuccess: (id) => {
      void qc.invalidateQueries({ queryKey: ['quyen'] })
      setMoForm(false)
      setTenQuyen('')
      nav(`/quan-tri/phan-quyen/${id}`)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/quyen/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['quyen'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  /** Số ô CÒN HỢP LỆ của một nhóm — không đếm ô đã bỏ khỏi bảng khai. */
  const demO = (q: QuyenDto) =>
    danhMuc
      ? demTheoChucNang(
          danhMuc,
          new Set(q.chucNangs.flatMap((c) => c.hanhDongs.map((h) => `${c.tenChucNang}:${h}`))),
          Object.keys(danhMuc.thaoTacTheoChucNang),
        )
      : 0

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        {coQuyen('PhanQuyen', 'Them') && (
          <Button onClick={() => { setTenQuyen(''); setMaLoi(null); setMoForm(true) }}>
            <Plus className="h-4 w-4" />
            {t('quyen.themMoi')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={tao.isPending}
        tieuDe={t('quyen.themMoi')}
        moTa={t('quyen.themMoiGoiY')}
      >
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenQuyenMoi">{t('quyen.tenQuyen')}</Label>
            <Input
              id="tenQuyenMoi"
              value={tenQuyen}
              onChange={(e) => setTenQuyen(e.target.value)}
              autoFocus
            />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button variant="outline" onClick={() => setMoForm(false)} disabled={tao.isPending}>
              {t('chung.huy')}
            </Button>
            <Button onClick={() => tao.mutate()} disabled={tao.isPending || !tenQuyen}>
              {tao.isPending ? t('chung.dangTai') : t('quyen.taoVaCauHinh')}
            </Button>
          </ModalChan>
        </div>
      </Modal>

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('quyen.tenQuyen')}</Th>
              <Th>{t('quyen.soTaiKhoan')}</Th>
              <Th>{t('quyen.soO')}</Th>
              <Th className="w-24" />
            </tr>
          </thead>
          <tbody>
            {quyens?.map((q) => (
              <tr key={q.id} className="hover:bg-muted/40">
                <Td className="font-medium">
                  {/* Thao tác chính của dòng phải bấm thẳng được, không giấu trong menu. */}
                  <Link
                    to={`/quan-tri/phan-quyen/${q.id}`}
                    className="text-primary underline-offset-2 hover:underline"
                  >
                    {q.tenQuyen}
                  </Link>
                </Td>
                <Td>
                  <Badge variant={q.soTaiKhoan > 0 ? 'accent' : 'muted'}>{q.soTaiKhoan}</Badge>
                </Td>
                <Td className="text-muted-foreground">{demO(q)}</Td>
                <Td>
                  <div className="flex justify-end">
                    <MenuThaoTac
                      nhanMo={t('chung.thaoTac')}
                      muc={[
                        {
                          nhan: t('quyen.cauHinh'),
                          icon: Pencil,
                          onChon: () => nav(`/quan-tri/phan-quyen/${q.id}`),
                        },
                        {
                          nhan: t('chung.xoa'),
                          icon: Trash2,
                          nguyHiem: true,
                          ngatNhom: true,
                          an: !coQuyen('PhanQuyen', 'Xoa'),
                          onChon: () => {
                            setMaLoiBang(null)
                            setXoaCho(q)
                          },
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

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('chung.xacNhanXoa')}
        thongDiep={xoaCho ? `${t('chung.xoa')} "${xoaCho.tenQuyen}"?` : ''}
        nhanDongY={t('chung.xoa')}
        onHuy={() => setXoaCho(null)}
        onDongY={() => {
          if (xoaCho) xoa.mutate(xoaCho.id)
          setXoaCho(null)
        }}
      />
    </div>
  )
}
