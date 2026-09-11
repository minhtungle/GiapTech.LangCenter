import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { UserPlus, X } from 'lucide-react'
import {
  api, layDuLieuLoi, layMaLoi, trangRong, type KetQuaTrang, type ThamSoTrang,
} from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
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
 *
 * **Không phải module riêng** (10/09/2026): hiện thành một TAB của màn Lớp học. Hàng chờ là
 * việc của người xếp lớp, không phải một khu vực nghiệp vụ tách biệt — và tab đó chỉ hiện cho
 * ai có `LopHoc.Sua`, đúng bằng quyền mà endpoint đang đòi.
 *
 * `nhung` = đang nằm trong tab của màn khác → bỏ tiêu đề và link quay lại của riêng nó, vì màn
 * cha đã có sẵn hai thứ đó. Cùng quy ước với `LichVaDiemDanh`, `BaiTapCuaLop`.
 */
export default function ChoXepLop({ nhung = false }: { nhung?: boolean } = {}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const duocSuaLop = coQuyen('LopHoc', 'Sua')
  const [duyetCho, setDuyetCho] = useState<YeuCauXepLopDto | null>(null)
  const [tuChoiCho, setTuChoiCho] = useState<YeuCauXepLopDto | null>(null)
  const [lopChon, setLopChon] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  // Tìm kiếm + phân trang ở SERVER (12/09/2026): hàng chờ của trung tâm đông lên vài trăm
  // dòng, kéo hết về rồi cắt ở trình duyệt sẽ chậm dần mà không ai để ý cho tới khi quá muộn.
  const [timKiem, setTimKiem] = useState('')
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const thamSo: ThamSoTrang = { trang, soDong }

  const { data: kq = trangRong<YeuCauXepLopDto>(), isLoading } = useQuery({
    queryKey: ['cho-xep-lop', timKiem, trang, soDong],
    queryFn: async () =>
      (
        await api.get<KetQuaTrang<YeuCauXepLopDto>>('/lop-hoc/cho-xep-lop', {
          params: { timKiem: timKiem || undefined, ...thamSo },
        })
      ).data,
  })
  const ds = kq.duLieu

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

  /**
   * Cảnh báo lệch khoá học (12/09/2026). null = chưa có cảnh báo nào đang chờ trả lời.
   *
   * Là **cảnh báo, không phải chặn**: backend trả `KHOA_HOC_KHONG_KHOP_LOP` ở lần gọi đầu; người
   * duyệt đọc rồi đồng ý thì gọi lại với `boQuaCanhBaoKhoaHoc: true`.
   */
  const [canhBaoKhoa, setCanhBaoKhoa] = useState<{
    khoaCuaDon: string[]
    khoaCuaLop: string[]
    tenLop: string
  } | null>(null)

  const duyet = useMutation({
    // Kiểu tham số khai tường minh: `(x = false) =>` làm TanStack suy ra `void` và mọi lời
    // gọi `mutate(true)` thành lỗi biên dịch.
    mutationFn: (boQuaCanhBao: boolean) =>
      api.post(`/lop-hoc/${lopChon}/duyet-cho-xep-lop`, {
        yeuCauIds: [duyetCho!.id],
        boQuaCanhBaoKhoaHoc: boQuaCanhBao,
      }),
    onSuccess: () => {
      lamMoi()
      setDuyetCho(null)
      setLopChon(null)
      setMaLoi(null)
      setCanhBaoKhoa(null)
    },
    onError: (e) => {
      const ma = layMaLoi(e)
      if (ma === 'KHOA_HOC_KHONG_KHOP_LOP') {
        // Không hiện ô lỗi đỏ: đây chưa phải lỗi, chỉ là điều người duyệt cần biết trước khi
        // quyết. Mở hộp hỏi lại kèm tên khoá hai bên để họ so được ngay tại chỗ.
        const du = layDuLieuLoi(e)
        setCanhBaoKhoa({
          khoaCuaDon: (du?.khoaCuaDon as string[]) ?? [],
          khoaCuaLop: (du?.khoaCuaLop as string[]) ?? [],
          tenLop: (du?.tenLop as string) ?? '',
        })
        setMaLoi(null)
        return
      }
      setMaLoi(ma)
    },
  })

  const tuChoi = useMutation({
    mutationFn: ({ id, lyDo }: { id: string; lyDo: string }) =>
      api.post(`/lop-hoc/cho-xep-lop/${id}/tu-choi`, { lyDo }),
    onSuccess: () => {
      lamMoi()
      setTuChoiCho(null)
      setMaLoi(null)
    },
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
      {!nhung && (
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="text-lg font-semibold">{t('menu.choXepLop')}</h2>
          {kq.tongSoDong > 0 && <Badge variant="cho">{kq.tongSoDong}</Badge>}
          <Link
            to="/lms/lop-hoc"
            className="ml-auto text-sm text-muted-foreground hover:text-foreground"
          >
            {t('menu.lopHoc')}
          </Link>
        </div>
      )}

      {maLoi && !duyetCho && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="space-y-4 pt-6">
          {/* Ô tìm: gõ là đổi `timKiem` → queryKey đổi → TanStack tự gọi lại. Về trang 1 vì
              đang ở trang 3 mà lọc còn 2 kết quả thì bảng trống trơn một cách vô lý. */}
          <Input
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
            placeholder={t('xepLop.timHocVienCho')}
            className="sm:max-w-xs"
          />

          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : ds.length === 0 ? (
            // Phân biệt hai tình huống: chưa ai chờ, vs lọc không ra. Cùng một câu thì người
            // dùng tưởng hàng chờ rỗng trong khi chỉ là gõ sai tên.
            <TrangTrong
              thongDiep={timKiem ? t('xepLop.timKhongThayCho') : t('xepLop.khongCoAiCho')}
            />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>{t('xepLop.hocVien')}</Th>
                  <Th>{t('khoaHoc.tenKhoa')}</Th>
                  <Th className="text-right">{t('xepLop.hocPhiTuDon')}</Th>
                  {/* Người gửi + ngày gửi gộp MỘT cột và ẩn ở màn hẹp: hàng chờ dài thì
                      thông tin quyết định là "ai · khoá gì · bao nhiêu tiền", còn ai gửi
                      lúc nào là bối cảnh — vẫn cần nhưng không đáng hai cột (12/09/2026). */}
                  <Th className="hidden lg:table-cell">{t('xepLop.nguoiGui')}</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {ds.map((y) => (
                  <tr key={y.id} className="hover:bg-muted/40">
                    <Td>
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span className="font-medium">{y.tenHocVien}</span>
                        {/* Lần gửi > 1 = đơn này đã bị từ chối trước đó. Đánh dấu để người xử lý
                            đọc ghi chú thay vì từ chối lại vì cùng một lý do. */}
                        {y.lanGui > 1 && (
                          <Badge variant="cho">
                            {t('xepLop.lanGuiThu', { so: y.lanGui })}
                          </Badge>
                        )}
                      </div>
                      {/* Điện thoại và ghi chú gộp một dòng thay vì hai: mỗi dòng bảng bớt
                          được một tầng, mà hàng chờ dài thì chiều cao dòng là thứ tốn nhất.
                          Ghi chú cắt ở 1 dòng (`truncate`) + `title` để xem đủ khi cần —
                          ghi chú dài 500 ký tự từng đẩy một dòng cao gấp ba. */}
                      {(y.soDienThoai || y.ghiChu) && (
                        <div
                          className="truncate text-xs text-muted-foreground"
                          title={
                            [
                              y.soDienThoai,
                              y.ghiChu && `${t('xepLop.ghiChuNguoiGui')}: ${y.ghiChu}`,
                            ]
                              .filter(Boolean)
                              .join(' · ')
                          }
                        >
                          {[y.soDienThoai, y.ghiChu].filter(Boolean).join(' · ')}
                        </div>
                      )}
                      {/* Màn hẹp không có cột người gửi → đưa xuống đây để không mất thông tin. */}
                      <div className="text-xs text-muted-foreground lg:hidden">
                        {(y.tenNguoiGui ?? '—') + ' · ' + ngayVN(y.thoiDiemGui)}
                      </div>
                    </Td>
                    <Td>
                      {y.tenKhoaHoc}
                      <div className="text-xs text-muted-foreground">
                        {t('khoaHoc.soBuoiNgan', { so: y.soBuoi })}
                      </div>
                    </Td>
                    <Td className="whitespace-nowrap text-right">
                      {tien(y.soTien, y.donViTien)}
                      {y.donViTien !== 'VND' && (
                        <div className="text-xs text-muted-foreground">
                          = {tienVN(y.soTien * y.tyGiaVeVnd)}
                        </div>
                      )}
                    </Td>
                    <Td className="hidden whitespace-nowrap text-muted-foreground lg:table-cell">
                      {y.tenNguoiGui ?? '—'}
                      <div className="text-xs">{ngayVN(y.thoiDiemGui)}</div>
                    </Td>
                    <Td>
                      {/*
                        GIỮ nút Duyệt ra ngoài, đưa Từ chối vào menu (12/09/2026).

                        Không gom cả hai vào menu: duyệt là việc làm hàng chục lần mỗi phiên,
                        bắt bấm hai lần (mở menu → chọn) cho thao tác chính là tính thêm một
                        cú bấm vào mọi học viên. Từ chối thì hiếm và đã phải mở form nhập lý
                        do, thêm một cú bấm không đáng kể.

                        Cột hẹp lại đáng kể: hai nút có chữ chiếm ~210px, nay còn ~140px.
                      */}
                      <div className="flex items-center justify-end gap-1">
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
                            {/* TỪ CHỐI, không phải "huỷ": huỷ là hành động của bên BÁN thu
                                lại yêu cầu, từ chối là bên đào tạo không nhận. Lý do bắt buộc
                                nên phải mở form, không dùng hộp confirm trơn. */}
                            <MenuThaoTac
                              nhanMo={t('chung.thaoTac')}
                              muc={[
                                {
                                  nhan: t('xepLop.tuChoi'),
                                  icon: X,
                                  nguyHiem: true,
                                  onChon: () => {
                                    setMaLoi(null)
                                    setTuChoiCho(y)
                                  },
                                },
                              ]}
                            />
                          </>
                        )}
                      </div>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}

          {/* Ẩn khi chỉ có một trang: thanh phân trang cho 3 dòng là nhiễu. */}
          {kq.tongSoTrang > 1 && (
            <PhanTrang
              trang={kq.trang}
              soDong={kq.soDong}
              tongSoDong={kq.tongSoDong}
              tongSoTrang={kq.tongSoTrang}
              onDoiTrang={setTrang}
              onDoiSoDong={(n) => {
                setSoDong(n)
                setTrang(1)
              }}
            />
          )}
        </CardContent>
      </Card>

      <Modal
        mo={!!tuChoiCho}
        onDong={() => setTuChoiCho(null)}
        chanDoiKhiXuLy={tuChoi.isPending}
        tieuDe={t('xepLop.tuChoiXepLop')}
        moTa={tuChoiCho?.tenHocVien}
        rong="sm"
      >
        {tuChoiCho && (
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              e.preventDefault()
              const fd = new FormData(e.currentTarget)
              const lyDo = String(fd.get('lyDo') ?? '').trim()
              hoi({
                tieuDe: t('xepLop.tuChoiXepLop'),
                thongDiep: t('xepLop.hoiTuChoi', { ten: tuChoiCho.tenHocVien }),
                nhanDongY: t('xepLop.tuChoi'),
                nguyHiem: true,
                onDongY: () => tuChoi.mutate({ id: tuChoiCho.id, lyDo }),
              })
            }}
          >
            <p className="text-sm text-muted-foreground">{t('xepLop.lyDoBatBuoc')}</p>

            <div>
              <Label htmlFor="lyDo">{t('xepLop.lyDo')} *</Label>
              <Textarea
                id="lyDo"
                name="lyDo"
                rows={3}
                required
                maxLength={500}
                placeholder={t('xepLop.lyDoGoiY')}
              />
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => setTuChoiCho(null)}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" variant="destructive" disabled={tuChoi.isPending}>
                {t('xepLop.tuChoi')}
              </Button>
            </div>
          </form>
        )}
      </Modal>

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
                    onDongY: () => duyet.mutate(false),
                  })
                }
              >
                {t('xepLop.duyet')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {/*
        CẢNH BÁO LỆCH KHOÁ HỌC (12/09/2026) — thông báo để người duyệt lưu ý, **vẫn cho phép**
        nếu họ đồng ý (yêu cầu chủ sản phẩm).

        Dùng `HopXacNhan` trực tiếp chứ không qua `useXacNhan`: hộp này mở TỪ TRONG modal duyệt
        và cần lời văn nhiều dòng (khoá của đơn vs khoá của lớp) để người duyệt so được ngay.
        Modal duyệt bên dưới bị `inert` tự động nên không bấm nhầm được (vá 11/09/2026).
      */}
      <HopXacNhan
        mo={canhBaoKhoa !== null}
        tieuDe={t('xepLop.canhBaoLechKhoa')}
        thongDiep={
          canhBaoKhoa
            ? t('xepLop.hoiLechKhoa', {
                khoaDon: canhBaoKhoa.khoaCuaDon.join(', '),
                lop: canhBaoKhoa.tenLop,
                khoaLop: canhBaoKhoa.khoaCuaLop.join(', '),
              })
            : ''
        }
        nhanDongY={t('xepLop.vanDuyet')}
        onHuy={() => setCanhBaoKhoa(null)}
        onDongY={() => {
          setCanhBaoKhoa(null)
          duyet.mutate(true)
        }}
      />

      {hop}
    </div>
  )
}
