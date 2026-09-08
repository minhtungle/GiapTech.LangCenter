import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Trash2, UserPlus } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  tienVN, ngayVN,
  type LopHocDto, type NguoiDungNgan, type HocVienTrongLop,
} from './lopHocTypes'
import { tien, type YeuCauXepLopDto } from '../crm/crmTypes'


/** Bước 3 của wizard, cũng dùng lại làm màn quản lý học viên của lớp. */
export function HocVienCuaLop({
  lop,
  nguoiDungs,
  onDong,
  nhung,
}: {
  lop: LopHocDto
  nguoiDungs: NguoiDungNgan[]
  onDong: () => void
  /** true = đang là tab trong view chi tiết lớp, không bọc Modal. */
  nhung?: boolean
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  // Ghi danh và gỡ học viên suy từ quyền SỬA LỚP, không phải một quyền riêng.
  const duocSuaLop = coQuyen('LopHoc', 'Sua')
  const [chon, setChon] = useState<string[]>([])
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: hocViens = [] } = useQuery({
    queryKey: ['lop-hoc', lop.id, 'hoc-vien'],
    queryFn: async () =>
      (await api.get<HocVienTrongLop[]>(`/lop-hoc/${lop.id}/hoc-vien`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc', lop.id, 'hoc-vien'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
    void qc.invalidateQueries({ queryKey: ['cho-xep-lop'] })
  }

  const them = useMutation({
    mutationFn: () => api.post(`/lop-hoc/${lop.id}/hoc-vien`, { hocVienIds: chon }),
    onSuccess: () => {
      setChon([])
      setMaLoi(null)
      lamMoi()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const go = useMutation({
    mutationFn: (hocVienId: string) =>
      api.delete(`/lop-hoc/${lop.id}/hoc-vien/${hocVienId}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * CÁCH 2 của FR-21: chọn học viên đang chờ ngay trong lớp.
   *
   * Chỉ tải khi người dùng được sửa lớp — người chỉ xem không có nút duyệt nên gọi API cũng
   * chỉ để nhận 403.
   */
  const { data: dsCho = [] } = useQuery({
    queryKey: ['cho-xep-lop'],
    queryFn: async () =>
      (await api.get<YeuCauXepLopDto[]>('/lop-hoc/cho-xep-lop')).data,
    enabled: duocSuaLop,
  })

  const duyet = useMutation({
    mutationFn: (yeuCauIds: string[]) =>
      api.post(`/lop-hoc/${lop.id}/duyet-cho-xep-lop`, { yeuCauIds }),
    onSuccess: () => {
      setMaLoi(null)
      lamMoi()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const daTrongLop = new Set(hocViens.map((h) => h.hocVienId))
  const luaChon = nguoiDungs
    .filter((u) => u.loaiNguoiDung === 'HocVien' && !daTrongLop.has(u.id))
    .map((u) => ({ giaTri: u.id, nhan: u.hoTen, phu: u.email ?? undefined }))

  // Backend trả null cho người không được xem tiền, nên chỉ cần hỏi dữ liệu chứ không cần
  // biết vai trò. Ẩn hẳn cột thay vì hiện cột toàn dấu "—" — cột rỗng chỉ tổ khiến người
  // dùng tưởng dữ liệu bị mất.
  const hienCotTien = hocViens.some((h) => h.hocPhiApDung !== null)

  return (
    <KhungNoiDung nhung={nhung} onDong={onDong} tieuDe={`${t('lopHoc.hocVien')} — ${lop.ten}`}>
      <div className="grid gap-4">
        {/*
          CÁCH 2 của FR-21 — danh sách học viên đã mua khoá, chờ xếp lớp.
          Đặt TRÊN ô thêm học viên thường: người đã trả tiền phải được xếp trước, và học phí của
          họ lấy từ đơn CRM nên không được thêm bằng ô bên dưới (ô đó dùng học phí của lớp).

          Ẩn hẳn khi không có ai chờ — một khung rỗng thường trực chỉ làm màn hình dài thêm.
        */}
        {duocSuaLop && dsCho.length > 0 && (
          <Card>
            <CardContent className="grid gap-2 pt-6">
              <div className="flex flex-wrap items-center gap-2">
                <h4 className="text-sm font-semibold">{t('xepLop.dangCho')}</h4>
                <Badge variant="cho">{dsCho.length}</Badge>
              </div>

              <ul className="grid gap-1.5">
                {dsCho.map((y) => (
                  <li
                    key={y.id}
                    className="flex flex-wrap items-center gap-2 rounded-md border border-border p-2"
                  >
                    <span className="text-sm font-medium">{y.tenHocVien}</span>
                    <Badge variant="muted">{y.tenKhoaHoc}</Badge>
                    <span className="text-xs text-muted-foreground">
                      {t('khoaHoc.soBuoiNgan', { so: y.soBuoi })} · {ngayVN(y.thoiDiemGui)}
                    </span>
                    <span className="ml-auto text-sm">
                      {/* Số tiền đơn CRM — sẽ thành học phí áp dụng, nên người xếp lớp thấy
                          trước khi bấm. Ngoại tệ hiện luôn số VND vì sổ học phí chỉ có VND. */}
                      {tien(y.soTien, y.donViTien)}
                      {y.donViTien !== 'VND' && (
                        <span className="ml-1 text-xs text-muted-foreground">
                          = {tienVN(y.soTien * y.tyGiaVeVnd)}
                        </span>
                      )}
                    </span>
                    <Button
                      size="sm"
                      disabled={duyet.isPending}
                      onClick={() =>
                        hoi({
                          tieuDe: t('xepLop.duyetVaoLop'),
                          thongDiep: t('xepLop.hoiDuyet', {
                            ten: y.tenHocVien,
                            lop: lop.ten,
                            hocPhi: tienVN(y.soTien * y.tyGiaVeVnd),
                          }),
                          nhanDongY: t('xepLop.duyet'),
                          onDongY: () => duyet.mutate([y.id]),
                        })
                      }
                    >
                      <UserPlus className="h-4 w-4" />
                      {t('xepLop.duyet')}
                    </Button>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        )}

        <div className="flex flex-wrap items-end gap-2">
          <div className="flex min-w-64 flex-1 flex-col gap-1.5">
            <Label htmlFor="themHocVien">{t('lopHoc.themHocVien')}</Label>
            <SelectTimKiemNhieu
              id="themHocVien"
              luaChon={luaChon}
              giaTri={chon}
              onDoi={setChon}
              placeholder={t('lopHoc.timHocVien')}
              placeholderTimKiem={t('lopHoc.timHocVien')}
            />
          </div>
          {duocSuaLop && (
            <Button
              disabled={chon.length === 0 || them.isPending}
              onClick={() =>
                hoi({
                  tieuDe: t('lopHoc.themHocVien'),
                  thongDiep: t('lopHoc.hoiThemHocVien', { soLuong: chon.length }),
                  onDongY: () => them.mutate(),
                })
              }
            >
              {t('chung.them')}
            </Button>
          )}
        </div>

        <p className="text-xs text-muted-foreground">
          {lop.sucChuaToiDa === null
            ? t('lopHoc.daChon', { soLuong: hocViens.length })
            : t('lopHoc.sucChuaConLai', {
                daChon: hocViens.length,
                toiDa: lop.sucChuaToiDa,
              })}
        </p>

        {maLoi && (
          <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
        )}

        {hocViens.length === 0 ? (
          <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>{t('taiKhoan.hoTen')}</Th>
                <Th>{t('taiKhoan.email')}</Th>
                <Th>{t('lopHoc.ngayVaoLop')}</Th>
                {hienCotTien && <Th>{t('lopHoc.hocPhiApDung')}</Th>}
                <Th className="w-16" />
              </tr>
            </thead>
            <tbody>
              {hocViens.map((h) => (
                <tr key={h.id} className="hover:bg-muted/40">
                  <Td className="font-medium">{h.hoTen}</Td>
                  <Td className="text-muted-foreground">{h.email ?? '—'}</Td>
                  <Td className="text-muted-foreground">{ngayVN(h.ngayVaoLop)}</Td>
                  {hienCotTien && (
                    <Td className="text-muted-foreground">{tienVN(h.hocPhiApDung)}</Td>
                  )}
                  <Td>
                    <div className="flex justify-end">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('lopHoc.goHocVien'),
                            icon: Trash2,
                            nguyHiem: true,
                            an: !duocSuaLop,
                            onChon: () =>
                              hoi({
                                tieuDe: t('lopHoc.goHocVien'),
                                thongDiep: t('lopHoc.hoiGoHocVien', { ten: h.hoTen }),
                                nhanDongY: t('lopHoc.goHocVien'),
                                nguyHiem: true,
                                onDongY: () => go.mutate(h.hocVienId),
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
      </div>
      {hop}
    </KhungNoiDung>
  )
}
