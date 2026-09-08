import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { UserPlus, X } from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { tienVN, ngayVN, type LopHocDto } from './lopHocTypes'
import { tien, type YeuCauXepLopDto } from '../crm/crmTypes'

/**
 * FR-21 — **Cách 1**: danh sách học viên chờ xếp lớp, bấm duyệt rồi chọn lớp đã tạo.
 *
 * Cách 2 (từ trong lớp chọn người chờ) nằm ở tab Học viên của view chi tiết lớp. Hai lối vào
 * cho hai tình huống thật: ở đây người điều phối nhìn cả hàng chờ để cân lớp, còn ở kia người
 * phụ trách một lớp cụ thể muốn lấp cho đủ chỗ. Cả hai gọi cùng một endpoint duyệt.
 */
export default function ChoXepLop() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const duocSuaLop = coQuyen('LopHoc', 'Sua')
  const [duyetCho, setDuyetCho] = useState<YeuCauXepLopDto | null>(null)
  const [lopChon, setLopChon] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['cho-xep-lop'],
    queryFn: async () => (await api.get<YeuCauXepLopDto[]>('/lop-hoc/cho-xep-lop')).data,
  })

  // Chỉ tải danh sách lớp khi thật sự mở hộp thoại duyệt — vào màn này chỉ để xem thì không
  // cần gọi thêm một API 200 dòng.
  const { data: lopHocs = [] } = useQuery({
    queryKey: ['lop-hoc', 'chon-de-xep'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<LopHocDto>>('/lop-hoc', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: !!duyetCho,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['cho-xep-lop'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
    void qc.invalidateQueries({ queryKey: ['khach-hang'] })
  }

  const duyet = useMutation({
    mutationFn: () =>
      api.post(`/lop-hoc/${lopChon}/duyet-cho-xep-lop`, { yeuCauIds: [duyetCho!.id] }),
    onSuccess: () => {
      lamMoi()
      setDuyetCho(null)
      setLopChon(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const huy = useMutation({
    mutationFn: (id: string) => api.delete(`/lop-hoc/cho-xep-lop/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * Lớp đã kết thúc/huỷ bị loại: xếp người mới vào lớp đã đóng là sai trong mọi trường hợp.
   *
   * KHÔNG ưu tiên "lớp cùng khoá" được: `LOP_HOC` hiện chưa có khoá ngoại về `KHOA_HOC` (CRM
   * đứng riêng, chốt 08/09/2026). Vì vậy tên khoá của đơn hiện ở cả bảng và hộp thoại để người
   * điều phối tự đối chiếu. Khi nối hai bảng thì đưa lớp cùng khoá lên đầu ở đây.
   */
  const luaChonLop = lopHocs
    .filter((l) => l.trangThai !== 'DaKetThuc' && l.trangThai !== 'DaHuy')
    .map((l) => ({
      giaTri: l.id,
      nhan: l.ten,
      phu: `${t(`trangThaiLopHoc.${l.trangThai}`)} · ${t('lopHoc.soHocVien')}: ${l.soHocVien}`,
    }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{t('menu.choXepLop')}</h2>
        {ds.length > 0 && <Badge variant="cho">{ds.length}</Badge>}
        <Link
          to="/lop-hoc"
          className="ml-auto text-sm text-muted-foreground hover:text-foreground"
        >
          {t('menu.lopHoc')}
        </Link>
      </div>

      {maLoi && !duyetCho && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : ds.length === 0 ? (
            <TrangTrong thongDiep={t('xepLop.khongCoAiCho')} />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('xepLop.hocVien')}</Th>
                  <Th>{t('khoaHoc.tenKhoa')}</Th>
                  <Th className="text-right">{t('xepLop.hocPhiTuDon')}</Th>
                  <Th>{t('xepLop.nguoiGui')}</Th>
                  <Th>{t('xepLop.thoiDiemGui')}</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {ds.map((y) => (
                  <tr key={y.id} className="hover:bg-muted/40">
                    <Td>
                      <div className="font-medium">{y.tenHocVien}</div>
                      {y.soDienThoai && (
                        <div className="text-xs text-muted-foreground">{y.soDienThoai}</div>
                      )}
                    </Td>
                    <Td>
                      {y.tenKhoaHoc}
                      <div className="text-xs text-muted-foreground">
                        {t('khoaHoc.soBuoiNgan', { so: y.soBuoi })}
                      </div>
                    </Td>
                    <Td className="text-right">
                      {tien(y.soTien, y.donViTien)}
                      {y.donViTien !== 'VND' && (
                        <div className="text-xs text-muted-foreground">
                          = {tienVN(y.soTien * y.tyGiaVeVnd)}
                        </div>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">{y.tenNguoiGui ?? '—'}</Td>
                    <Td className="text-muted-foreground">{ngayVN(y.thoiDiemGui)}</Td>
                    <Td>
                      <div className="flex justify-end gap-2">
                        {duocSuaLop && (
                          <>
                            <Button
                              size="sm"
                              onClick={() => {
                                setLopChon(null)
                                setMaLoi(null)
                                setDuyetCho(y)
                              }}
                            >
                              <UserPlus className="h-4 w-4" />
                              {t('xepLop.duyet')}
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                hoi({
                                  tieuDe: t('xepLop.huyYeuCau'),
                                  thongDiep: t('xepLop.hoiHuy', { ten: y.tenHocVien }),
                                  nhanDongY: t('xepLop.huyYeuCau'),
                                  nguyHiem: true,
                                  onDongY: () => huy.mutate(y.id),
                                })
                              }
                            >
                              <X className="h-4 w-4" />
                              {t('chung.huy')}
                            </Button>
                          </>
                        )}
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
        mo={!!duyetCho}
        onDong={() => {
          setDuyetCho(null)
          setLopChon(null)
        }}
        chanDoiKhiXuLy={duyet.isPending}
        tieuDe={t('xepLop.duyetVaoLop')}
        moTa={duyetCho?.tenHocVien}
        rong="sm"
      >
        {duyetCho && (
          <div className="grid gap-3">
            <p className="text-sm text-muted-foreground">
              {t('xepLop.khoaDaMua')}: <strong>{duyetCho.tenKhoaHoc}</strong> ·{' '}
              {t('khoaHoc.soBuoiNgan', { so: duyetCho.soBuoi })}
            </p>
            <p className="text-sm text-muted-foreground">
              {t('xepLop.hocPhiSeApDung', {
                so: tienVN(duyetCho.soTien * duyetCho.tyGiaVeVnd),
              })}
            </p>

            <div>
              <Label htmlFor="lopChon">{t('xepLop.chonLop')} *</Label>
              <SelectTimKiem
                id="lopChon"
                luaChon={luaChonLop}
                giaTri={lopChon}
                onDoi={setLopChon}
                placeholder={t('xepLop.chonLop')}
                placeholderTimKiem={t('lopHoc.ten')}
              />
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setDuyetCho(null)
                  setLopChon(null)
                }}
              >
                {t('chung.huy')}
              </Button>
              <Button
                disabled={!lopChon || duyet.isPending}
                onClick={() =>
                  hoi({
                    tieuDe: t('xepLop.duyetVaoLop'),
                    thongDiep: t('xepLop.hoiDuyet', {
                      ten: duyetCho.tenHocVien,
                      lop: lopHocs.find((l) => l.id === lopChon)?.ten ?? '',
                      hocPhi: tienVN(duyetCho.soTien * duyetCho.tyGiaVeVnd),
                    }),
                    nhanDongY: t('xepLop.duyet'),
                    onDongY: () => duyet.mutate(),
                  })
                }
              >
                {t('xepLop.duyet')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {hop}
    </div>
  )
}
