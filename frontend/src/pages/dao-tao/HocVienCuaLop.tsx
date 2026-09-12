import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Info, Trash2, UserPlus } from 'lucide-react'
import { api, layDuLieuLoi, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
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
  const [moThem, setMoThem] = useState(false)
  const [xemHv, setXemHv] = useState<HocVienTrongLop | null>(null)

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
    queryKey: ['cho-xep-lop', 'trong-lop'],
    queryFn: async () =>
      // `soDong: 200` (chặn trên của `ThamSoTrang`): đây là danh sách CHỌN trong lớp, không
      // phải bảng duyệt — người phụ trách cần thấy cả hàng chờ để lấp chỗ, không phân trang.
      // Endpoint có phân trang từ 12/09/2026 nên mặc định 20 sẽ cắt mất người chờ lâu nhất.
      (
        await api.get<KetQuaTrang<YeuCauXepLopDto>>('/lop-hoc/cho-xep-lop', {
          params: { soDong: 200 },
        })
      ).data.duLieu,
    enabled: duocSuaLop,
  })

  /** Cảnh báo lệch khoá học (12/09/2026) — xem chú thích cùng tên ở `ChoXepLop.tsx`. */
  const [canhBaoKhoa, setCanhBaoKhoa] = useState<{
    ids: string[]
    khoaCuaDon: string[]
    khoaCuaLop: string[]
  } | null>(null)

  const duyet = useMutation({
    mutationFn: ({ ids, boQua }: { ids: string[]; boQua: boolean }) =>
      api.post(`/lop-hoc/${lop.id}/duyet-cho-xep-lop`, {
        yeuCauIds: ids,
        boQuaCanhBaoKhoaHoc: boQua,
      }),
    onSuccess: () => {
      setMaLoi(null)
      setCanhBaoKhoa(null)
      lamMoi()
    },
    onError: (e, bien) => {
      const ma = layMaLoi(e)
      if (ma === 'KHOA_HOC_KHONG_KHOP_LOP') {
        const du = layDuLieuLoi(e)
        // Giữ lại `ids` của lần gọi vừa thất bại: người duyệt đồng ý thì gọi lại đúng lô đó,
        // không phải bắt họ chọn lại từ đầu.
        setCanhBaoKhoa({
          ids: bien.ids,
          khoaCuaDon: (du?.khoaCuaDon as string[]) ?? [],
          khoaCuaLop: (du?.khoaCuaLop as string[]) ?? [],
        })
        setMaLoi(null)
        return
      }
      setMaLoi(ma)
    },
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
          THANH ĐẦU: sĩ số bên trái, nút mở modal bên phải (12/09/2026).

          Trước đó ô "Thêm học viên" và khối "Đang chờ xếp lớp" bày sẵn TRÊN bảng, nên mỗi lần
          chỉ muốn xem danh sách đều phải cuộn qua hai khối không dùng tới. Việc thêm người là
          thao tác thỉnh thoảng; việc XEM danh sách mới là thứ làm thường xuyên.

          Cùng hướng với form sửa lớp đã chuyển vào modal 09/09/2026.
        */}
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-sm text-muted-foreground">
            {lop.sucChuaToiDa === null
              ? t('lopHoc.daChon', { soLuong: hocViens.length })
              : t('lopHoc.sucChuaConLai', {
                  daChon: hocViens.length,
                  toiDa: lop.sucChuaToiDa,
                })}
          </p>

          {duocSuaLop && (
            <Button className="ml-auto" onClick={() => { setMaLoi(null); setMoThem(true) }}>
              <UserPlus className="h-4 w-4" />
              {t('lopHoc.themHocVien')}
              {/* Badge số người chờ ngay trên nút: người điều phối biết có việc cần làm mà
                  không phải mở modal ra xem. */}
              {dsCho.length > 0 && (
                <Badge variant="cho" className="ml-1">{dsCho.length}</Badge>
              )}
            </Button>
          )}
        </div>

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
                {/* Email bỏ khỏi bảng, vào modal thông tin (12/09/2026): nó dài, hiếm khi cần
                    đọc lướt, và đẩy các cột đáng quan tâm hơn ra rìa màn hình. */}
                <Th>{t('lopHoc.ngayVaoLop')}</Th>
                {hienCotTien && <Th>{t('lopHoc.hocPhiApDung')}</Th>}
                <Th className="w-16" />
              </tr>
            </thead>
            <tbody>
              {hocViens.map((h) => (
                <tr key={h.id} className="hover:bg-muted/40">
                  <Td className="font-medium">
                    {h.hoTen}
                    {/* Số điện thoại dưới tên: thứ hay cần nhất khi phải liên hệ gấp, không
                        đáng một cột riêng nhưng cũng không nên chôn hết vào modal. */}
                    {h.soDienThoai && (
                      <div className="text-xs font-normal text-muted-foreground">
                        {h.soDienThoai}
                      </div>
                    )}
                  </Td>
                  <Td className="whitespace-nowrap text-muted-foreground">
                    {ngayVN(h.ngayVaoLop)}
                  </Td>
                  {hienCotTien && (
                    <Td className="text-muted-foreground">{tienVN(h.hocPhiApDung)}</Td>
                  )}
                  <Td>
                    <div className="flex justify-end">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('lopHoc.xemThongTin'),
                            icon: Info,
                            onChon: () => setXemHv(h),
                          },
                          {
                            nhan: t('lopHoc.goHocVien'),
                            ngatNhom: true,
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
      {/*
        MODAL THÔNG TIN HỌC VIÊN (12/09/2026).

        Chủ sản phẩm: "nếu nhiều thông tin đang hiện gây rối thì cho ấn mở modal thông tin".
        Bảng giữ 3 cột đọc lướt (tên · ngày vào lớp · học phí); phần còn lại — email, điện
        thoại, nhân viên kinh doanh — vào đây.

        `tenNhanVienKinhDoanh` null có BA nghĩa nên KHÔNG hiện dấu "—" trơn: người dùng sẽ
        tưởng dữ liệu mất. Ẩn hẳn dòng khi không có.
      */}
      <Modal
        mo={xemHv !== null}
        onDong={() => setXemHv(null)}
        tieuDe={xemHv?.hoTen ?? ''}
        moTa={t('lopHoc.hocVienCuaLop', { lop: lop.ten })}
        rong="sm"
      >
        {xemHv && (
          <dl className="grid gap-3">
            <ThongTinDong nhan={t('taiKhoan.email')} giaTri={xemHv.email} />
            <ThongTinDong nhan={t('taiKhoan.soDienThoai')} giaTri={xemHv.soDienThoai} />
            <ThongTinDong
              nhan={t('lopHoc.ngayVaoLop')}
              giaTri={ngayVN(xemHv.ngayVaoLop)}
            />
            {xemHv.hocPhiApDung !== null && (
              <ThongTinDong
                nhan={t('lopHoc.hocPhiApDung')}
                giaTri={tienVN(xemHv.hocPhiApDung)}
              />
            )}
            {/* Chỉ hiện khi CÓ — xem chú thích đầu modal. */}
            {xemHv.tenNhanVienKinhDoanh && (
              <ThongTinDong
                nhan={t('lopHoc.nhanVienKinhDoanh')}
                giaTri={xemHv.tenNhanVienKinhDoanh}
              />
            )}
            {xemHv.ghiChu && (
              <ThongTinDong nhan={t('lopHoc.ghiChu')} giaTri={xemHv.ghiChu} />
            )}
          </dl>
        )}
      </Modal>

      {/*
        MODAL THÊM HỌC VIÊN — gom hai đường vào một chỗ (12/09/2026).

        Hai đường KHÁC NHAU về tiền, nên không gộp thành một danh sách:
        - "Đang chờ xếp lớp" (FR-21): học phí lấy từ ĐƠN CRM, gồm miễn giảm đã chốt với khách.
        - "Thêm trực tiếp": học phí lấy từ LỚP.
        Gộp lại thì người dùng không biết mình đang áp mức nào — đó là lỗi tiền bạc.

        Người đã trả tiền đặt TRƯỚC: họ phải được xếp trước người thêm tay.
      */}
      <Modal
        mo={moThem}
        onDong={() => setMoThem(false)}
        chanDoiKhiXuLy={them.isPending || duyet.isPending}
        tieuDe={t('lopHoc.themHocVien')}
        moTa={lop.ten}
        rong="lg"
      >
        <div className="grid gap-4">
          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          {dsCho.length > 0 && (
            <div className="grid gap-2">
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
                          onDongY: () => duyet.mutate({ ids: [y.id], boQua: false }),
                        })
                      }
                    >
                      <UserPlus className="h-4 w-4" />
                      {t('xepLop.duyet')}
                    </Button>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="grid gap-1.5">
            <Label htmlFor="themHocVien">
              {dsCho.length > 0 ? t('lopHoc.themTrucTiep') : t('lopHoc.themHocVien')}
            </Label>
            <SelectTimKiemNhieu
              id="themHocVien"
              luaChon={luaChon}
              giaTri={chon}
              onDoi={setChon}
              placeholder={t('lopHoc.timHocVien')}
              placeholderTimKiem={t('lopHoc.timHocVien')}
            />
            {/* Nói rõ mức học phí sẽ áp — đường này KHÁC đường duyệt ở trên. */}
            <p className="text-xs text-muted-foreground">{t('lopHoc.themTrucTiepGiaLop')}</p>
          </div>

          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setMoThem(false)}>
              {t('chung.huy')}
            </Button>
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
          </ModalChan>
        </div>
      </Modal>

      {/* Cảnh báo lệch khoá học — thông báo để lưu ý, vẫn cho phép nếu đồng ý (12/09/2026). */}
      <HopXacNhan
        mo={canhBaoKhoa !== null}
        tieuDe={t('xepLop.canhBaoLechKhoa')}
        thongDiep={
          canhBaoKhoa
            ? t('xepLop.hoiLechKhoa', {
                khoaDon: canhBaoKhoa.khoaCuaDon.join(', '),
                lop: lop.ten,
                khoaLop: canhBaoKhoa.khoaCuaLop.join(', '),
              })
            : ''
        }
        nhanDongY={t('xepLop.vanDuyet')}
        onHuy={() => setCanhBaoKhoa(null)}
        onDongY={() => {
          const ids = canhBaoKhoa?.ids ?? []
          setCanhBaoKhoa(null)
          if (ids.length > 0) duyet.mutate({ ids, boQua: true })
        }}
      />

      {hop}
    </KhungNoiDung>
  )
}

/** Một dòng nhãn — giá trị trong modal thông tin. Ẩn khi không có giá trị. */
function ThongTinDong({ nhan, giaTri }: { nhan: string; giaTri: string | null }) {
  if (!giaTri) return null
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{nhan}</dt>
      <dd className="text-sm font-medium">{giaTri}</dd>
    </div>
  )
}
