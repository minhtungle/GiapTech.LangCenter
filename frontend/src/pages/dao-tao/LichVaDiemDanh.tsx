import { Fragment, lazy, Suspense, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  CalendarDays, CalendarPlus, CalendarRange, CircleDot, ClipboardCheck, Eye, List, RotateCcw,
  Trash2, X,
} from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Textarea, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { NhanTinhTrangBuoi } from '@/components/dao-tao/NhanTinhTrangBuoi'
import { TRANG_THAI_DAT_DUOC, type TrangThaiBuoiHoc } from '@/lib/tinhTrangBuoi'
import { BangDiemDanh } from './BangDiemDanh'
import { type BuoiHocDto, gioVN } from './buoiHocTypes'

const THU: { ma: number; khoa: string }[] = [
  { ma: 1, khoa: 'Monday' }, { ma: 2, khoa: 'Tuesday' }, { ma: 3, khoa: 'Wednesday' },
  { ma: 4, khoa: 'Thursday' }, { ma: 5, khoa: 'Friday' }, { ma: 6, khoa: 'Saturday' },
  { ma: 0, khoa: 'Sunday' },
]

/**
 * Lịch tải theo yêu cầu: FullCalendar nặng ~200 kB và chỉ dùng khi người dùng chủ động bật
 * chế độ Lịch. Import thẳng thì mọi người mở bất kỳ màn nào cũng phải tải nó.
 */
const LichBuoiHoc = lazy(() =>
  import('@/components/ui/LichBuoiHoc').then((m) => ({ default: m.LichBuoiHoc })),
)

/** Tab Lịch học + Điểm danh của một lớp (bước 2 wizard, và dùng lại khi vận hành lớp). */
export function LichVaDiemDanh({
  lopHocId,
  tenLop,
  onDong,
  nhung,
}: {
  lopHocId: string
  tenLop: string
  onDong: () => void
  /** true = đang là tab trong view chi tiết lớp, không bọc Modal. */
  nhung?: boolean
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  // Mặc định BẢNG: nó là chỗ điểm danh và xem số liệu từng buổi. Lịch để nhìn tổng quát.
  const [kieuXem, setKieuXem] = useState<'bang' | 'lich'>('bang')
  const { coQuyen } = useQuyen()

  /*
    Gác nút theo QUYỀN THẬT (17/09/2026).

    Trước đó màn này không gọi `coQuyen` một lần nào: học viên vào lớp mình học vẫn thấy đủ nút
    "Sinh lịch", "Sinh lại lịch", "Điểm danh", "Huỷ buổi", "Xoá". Backend chặn hết (403 — đã thử
    bằng tài khoản `hv1`), nên KHÔNG rò rỉ dữ liệu; nhưng người dùng bấm vào chỉ nhận lỗi đỏ mà
    không hiểu vì sao — và tệ hơn, họ tưởng mình vừa làm hỏng thứ gì đó.

    Quyền lấy ĐÚNG theo endpoint tương ứng, không gom một cờ "là giáo viên":
      sinh lịch / sinh lại  → LopHoc.SinhLich
      huỷ buổi              → BuoiHoc.Huy
      xoá buổi              → BuoiHoc.Xoa
      mở bảng điểm danh     → DiemDanh.Sua   (ghi điểm danh; `Xem` chỉ để đọc)
  */
  const duocSinhLich = coQuyen('LopHoc', 'SinhLich')
  // "Thêm buổi" gọi `sinh-them-buoi` — gác bằng `BuoiHoc.Them`, KHÔNG phải `LopHoc.SinhLich`.
  // Giáo viên có `BuoiHoc.Them` nhưng KHÔNG có `SinhLich` (sinh lịch là việc của điều phối),
  // nên gộp hai nút vào một cờ sẽ giấu mất nút mà giáo viên vẫn dùng được.
  const duocThemBuoi = coQuyen('BuoiHoc', 'Them')
  const duocHuyBuoi = coQuyen('BuoiHoc', 'Huy')
  const duocXoaBuoi = coQuyen('BuoiHoc', 'Xoa')
  // Đặt trạng thái buổi dùng chung quyền với sửa buổi — xem `BuoiHocController.DatTrangThai`.
  const duocSuaBuoi = coQuyen('BuoiHoc', 'Sua')
  const duocDiemDanh = coQuyen('DiemDanh', 'Sua')

  const [moSinhLich, setMoSinhLich] = useState(false)
  const [moSinhThem, setMoSinhThem] = useState(false)
  const [xacNhanSinhLai, setXacNhanSinhLai] = useState(false)
  const [huyCho, setHuyCho] = useState<BuoiHocDto | null>(null)
  const [xoaCho, setXoaCho] = useState<BuoiHocDto | null>(null)
  const [buoiDiemDanh, setBuoiDiemDanh] = useState<BuoiHocDto | null>(null)
  const [trangThaiCho, setTrangThaiCho] = useState<BuoiHocDto | null>(null)

  const { data: buoiHocs = [], isLoading } = useQuery({
    queryKey: ['lop-hoc', lopHocId, 'buoi-hoc'],
    queryFn: async () => (await api.get<BuoiHocDto[]>(`/lop-hoc/${lopHocId}/buoi-hoc`)).data,
  })

  /*
    Ưu tiên BUỔI SẮP TỚI lên đầu (yêu cầu chủ sản phẩm 16/09/2026).

    API trả theo `ThuTu` (1→N) nên buổi đã dạy nằm trên; lớp 30 buổi thì buổi tới phải cuộn gần
    hết bảng mới thấy — mà "buổi tới dạy gì, ở đâu, phòng nào" mới là việc thường ngày.

    **Sắp ở ĐÂY, không sửa `OrderBy` của API**: cùng endpoint `/lop-hoc/{id}/buoi-hoc` còn bốn màn
    khác dùng, và `ChiTietBuoiHoc` suy "buổi trước / buổi sau" từ **vị trí trong mảng** — đổi thứ
    tự ở API thì nút "buổi sau" nhảy về quá khứ, một lỗi im lặng. Thứ tự theo `ThuTu` vẫn là hợp
    đồng của API; đây chỉ là cách MÀN NÀY trình bày.

    Mốc so sánh là thời điểm tuyệt đối hai bên nên KHÔNG phụ thuộc múi giờ — khác các phép cắt
    kỳ theo ngày (chỗ đó phải dùng múi giờ trung tâm, xem `ThongKeCrmDtos`).

    Buổi đã huỷ xuống nhóm "đã qua" dù ngày còn ở tương lai: nó không còn là việc phải làm.
  */
  const bayGio = Date.now()
  const laSapToi = (b: BuoiHocDto) =>
    b.trangThai !== 'DaHuy' && new Date(b.batDau).getTime() >= bayGio

  const sapToi = buoiHocs
    .filter(laSapToi)
    // Gần nhất trước — buổi kế tiếp phải là DÒNG ĐẦU.
    .sort((a, b) => +new Date(a.batDau) - +new Date(b.batDau))
  const daQua = buoiHocs
    .filter((b) => !laSapToi(b))
    // Mới nhất trước: buổi vừa dạy xong hay được xem lại nhất (điểm danh, nhận xét).
    .sort((a, b) => +new Date(b.batDau) - +new Date(a.batDau))

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc', lopHocId, 'buoi-hoc'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
  }

  const huyBuoi = useMutation({
    mutationFn: (id: string) => api.post(`/buoi-hoc/${id}/huy`),
    onSuccess: () => {
      lamMoi()
      setHuyCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setHuyCho(null)
    },
  })

  const xoaBuoi = useMutation({
    mutationFn: (id: string) => api.delete(`/buoi-hoc/${id}`),
    onSuccess: () => {
      lamMoi()
      setXoaCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setXoaCho(null)
    },
  })

  const datTrangThai = useMutation({
    mutationFn: ({ id, trangThai }: { id: string; trangThai: TrangThaiBuoiHoc }) =>
      api.post(`/buoi-hoc/${id}/trang-thai`, { trangThai }),
    onSuccess: () => {
      lamMoi()
      setTrangThaiCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setTrangThaiCho(null)
    },
  })

  const soDaChot = buoiHocs.filter((b) => b.trangThai === 'DaHoanThanh').length

  return (
    <KhungNoiDung nhung={nhung} onDong={onDong} tieuDe={`${t('buoiHoc.lich')} — ${tenLop}`}>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-3">
            {buoiHocs.length > 0 && (
              <div className="flex gap-1 rounded-lg border border-border p-1">
                {(['bang', 'lich'] as const).map((x) => (
                  <button
                    key={x}
                    type="button"
                    onClick={() => setKieuXem(x)}
                    className={
                      'flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium ' +
                      'transition-colors ' +
                      (kieuXem === x
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:bg-muted')
                    }
                  >
                    {x === 'bang' ? (
                      <List className="h-3.5 w-3.5" />
                    ) : (
                      <CalendarRange className="h-3.5 w-3.5" />
                    )}
                    {t(x === 'bang' ? 'lich.kieuBang' : 'lich.kieuLich')}
                  </button>
                ))}
              </div>
            )}

            <p className="text-sm text-muted-foreground">
              {buoiHocs.length > 0 && t('buoiHoc.daSinh', { soLuong: buoiHocs.length })}
              {soDaChot > 0 && ` · ${t('buoiHoc.daChot', { soLuong: soDaChot })}`}
            </p>
          </div>

          {/* Ba nút, ba việc khác nhau — trước đây chỉ có một nút vừa sinh vừa xoá.
              "Sinh lại" để cuối và viền đỏ vì nó là nút duy nhất xoá dữ liệu. */}
          {/* Cụm nút ẩn hẳn với người không có quyền ghi nào — học viên chỉ xem. */}
          <div className={duocSinhLich || duocThemBuoi ? 'flex flex-wrap gap-2' : 'hidden'}>
            {buoiHocs.length === 0 ? (
              <Button
                size="sm"
                className={duocSinhLich ? '' : 'hidden'}
                onClick={() => setMoSinhLich(true)}
              >
                <CalendarDays className="mr-1.5 h-4 w-4" />
                {t('buoiHoc.sinhLich')}
              </Button>
            ) : (
              <>
                {/* MỘT nút thêm buổi. Từng có hai nút ("Thêm buổi" cho buổi lẻ và "Sinh thêm
                    buổi" theo tần suất) — tên gần giống nhau nên gây nhầm, gộp 07/09/2026.
                    Thêm một buổi = để số buổi là 1. */}
                <Button
                  size="sm"
                  className={duocThemBuoi ? '' : 'hidden'}
                  onClick={() => setMoSinhThem(true)}
                >
                  <CalendarPlus className="mr-1.5 h-4 w-4" />
                  {t('buoiHoc.sinhThemBuoi')}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className={
                    'border-destructive/40 text-destructive hover:bg-destructive/10 '
                    + (duocSinhLich ? '' : 'hidden')
                  }
                  onClick={() => setXacNhanSinhLai(true)}
                >
                  <RotateCcw className="mr-1.5 h-4 w-4" />
                  {t('buoiHoc.sinhLaiLich')}
                </Button>
              </>
            )}
          </div>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {isLoading ? (
          <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
        ) : buoiHocs.length === 0 ? (
          <TrangTrong thongDiep={t('buoiHoc.chuaCoLich')} />
        ) : kieuXem === 'lich' ? (
          <Suspense
            fallback={
              <p className="py-8 text-center text-sm text-muted-foreground">
                {t('chung.dangTai')}
              </p>
            }
          >
            <LichBuoiHoc
              buoi={buoiHocs}
              // Bấm buổi → view chi tiết, không mở thẳng modal điểm danh nữa: view chi tiết
              // có cả điểm danh lẫn nhận xét / bài tập, và chuyển được sang buổi khác.
              // Điểm danh nhanh vẫn còn ở menu thao tác của bảng.
              onChonBuoi={(id) => navigate(`/lms/buoi-hoc/${id}`)}
            />
          </Suspense>
        ) : (
          <div className="max-h-[26rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th className="w-16">{t('buoiHoc.thuTu')}</Th>
                  <Th>{t('buoiHoc.thoiGian')}</Th>
                  <Th>{t('buoiHoc.giaoVien')}</Th>
                  <Th>{t('buoiHoc.diemDanh')}</Th>
                  <Th>{t('buoiHoc.trangThai')}</Th>
                  <Th className="w-24" />
                </tr>
              </thead>
              <tbody>
                {/*
                  Hai NHÓM, mỗi nhóm một hàng tiêu đề: đổi thứ tự mà không nói gì thì người dùng
                  tưởng dữ liệu lẫn lộn. Nhóm rỗng thì bỏ luôn cả tiêu đề (lớp chưa dạy buổi nào,
                  hoặc đã dạy hết).
                */}
                {([
                  ['sapToi', sapToi],
                  ['daQua', daQua],
                ] as const).map(([nhom, ds]) => ds.length === 0 ? null : (
                  <Fragment key={nhom}>
                    <tr className="bg-muted/30">
                      <Td colSpan={6} className="py-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                        {t(`buoiHoc.nhom.${nhom}`)} ({ds.length})
                      </Td>
                    </tr>
                    {ds.map((b) => (
                  <tr
                    key={b.id}
                    // Dòng đã qua mờ hơn: tiêu đề nhóm cuộn khỏi tầm mắt thì vẫn phân biệt được.
                    className={
                      'cursor-pointer hover:bg-muted/40 '
                      + (nhom === 'daQua' ? 'text-muted-foreground' : '')
                    }
                    onClick={() => navigate(`/lms/buoi-hoc/${b.id}`)}
                  >
                    <Td className="font-medium">{b.thuTu}</Td>
                    <Td className="text-muted-foreground">
                      {gioVN(b.batDau)}
                      {b.laHocBu && (
                        <Badge variant="accent" className="ml-2">
                          {t('buoiHoc.hocBu')}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {b.tenGiaoVien}
                      {b.giaoVienRieng && (
                        <Badge variant="cho" className="ml-2">
                          {t('buoiHoc.giaoVienRieng')}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {b.soDaDiemDanh}/{b.soHocVien}
                    </Td>
                    <Td>
                      {/* Tình trạng SUY TỪ GIỜ, không hiện thẳng `trangThai`: buổi đã qua mà
                          chưa chốt vẫn mang `DaLenLich` trong DB, hiện thẳng ra thì thành
                          "Đã lên lịch" — đúng lỗi chủ sản phẩm báo 18/09/2026. */}
                      <NhanTinhTrangBuoi
                        trangThai={b.trangThai}
                        batDau={b.batDau}
                        ketThuc={b.ketThuc}
                      />
                    </Td>
                    {/* Chặn nổi bọt: bấm menu thao tác không được đồng thời mở view chi tiết. */}
                    <Td onClick={(e) => e.stopPropagation()}>
                      <div className="flex justify-end">
                        <MenuThaoTac
                          nhanMo={t('chung.thaoTac')}
                          muc={[
                            {
                              nhan: t('chung.xemChiTiet'),
                              icon: Eye,
                              onChon: () => navigate(`/lms/buoi-hoc/${b.id}`),
                            },
                            {
                              nhan: t('buoiHoc.diemDanh'),
                              icon: ClipboardCheck,
                              // Mở bảng điểm danh = GHI điểm danh, cần `DiemDanh.Sua`.
                              // Học viên chỉ có `Xem` + `TuLam` (tự khai có mặt).
                              an: !duocDiemDanh,
                              onChon: () => setBuoiDiemDanh(b),
                            },
                            {
                              nhan: t('buoiHoc.doiTrangThai'),
                              icon: CircleDot,
                              // Cùng quyền với sửa buổi (`BuoiHoc.Sua`) — xem endpoint
                              // `/buoi-hoc/{id}/trang-thai`. Học viên chỉ XEM.
                              an: !duocSuaBuoi,
                              onChon: () => setTrangThaiCho(b),
                            },
                            {
                              nhan: t('buoiHoc.huyBuoi'),
                              icon: X,
                              nguyHiem: true,
                              ngatNhom: true,
                              // Buổi đã chốt là bằng chứng chuyên cần; buổi đã huỷ thì huỷ nữa
                              // cũng vô nghĩa. Backend chặn cả hai, đây chỉ là ẩn cho gọn.
                              an: !duocHuyBuoi || b.trangThai !== 'DaLenLich',
                              onChon: () => setHuyCho(b),
                            },
                            {
                              nhan: t('chung.xoa'),
                              icon: Trash2,
                              nguyHiem: true,
                              an: !duocXoaBuoi || b.trangThai === 'DaHoanThanh',
                              onChon: () => setXoaCho(b),
                            },
                          ]}
                        />
                      </div>
                    </Td>
                  </tr>
                    ))}
                  </Fragment>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </div>

      {moSinhLich && (
        <FormSinhLich
          lopHocId={lopHocId}
          daCoLich={buoiHocs.length > 0}
          onXong={() => {
            setMoSinhLich(false)
            lamMoi()
          }}
          onDong={() => setMoSinhLich(false)}
        />
      )}

      {moSinhThem && (
        <FormSinhLich
          noiTiep
          lopHocId={lopHocId}
          daCoLich={buoiHocs.length > 0}
          onXong={() => {
            setMoSinhThem(false)
            lamMoi()
          }}
          onDong={() => setMoSinhThem(false)}
        />
      )}

      {/* Ba hộp xác nhận RIÊNG, mỗi cái nói đúng hậu quả của nó — hộp chung chung
          "Bạn chắc chắn?" không giúp người dùng phân biệt xoá 1 buổi với xoá cả lịch. */}
      <HopXacNhan
        mo={xacNhanSinhLai}
        tieuDe={t('buoiHoc.sinhLaiLich')}
        thongDiep={t('buoiHoc.xacNhanSinhLai', {
          soLuong: buoiHocs.length - soDaChot,
          daChot: soDaChot,
        })}
        nhanDongY={t('buoiHoc.sinhLaiLich')}
        onHuy={() => setXacNhanSinhLai(false)}
        onDongY={() => {
          setXacNhanSinhLai(false)
          setMoSinhLich(true)
        }}
      />

      <HopXacNhan
        mo={huyCho !== null}
        tieuDe={t('buoiHoc.huyBuoi')}
        thongDiep={
          huyCho
            ? t('buoiHoc.xacNhanHuyBuoi', { thuTu: huyCho.thuTu, gio: gioVN(huyCho.batDau) })
            : ''
        }
        nhanDongY={t('buoiHoc.huyBuoi')}
        onHuy={() => setHuyCho(null)}
        onDongY={() => huyCho && huyBuoi.mutate(huyCho.id)}
      />

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('buoiHoc.xoaBuoi')}
        thongDiep={
          xoaCho
            ? t('buoiHoc.xacNhanXoaBuoi', { thuTu: xoaCho.thuTu, gio: gioVN(xoaCho.batDau) })
            : ''
        }
        nhanDongY={t('chung.xoa')}
        onHuy={() => setXoaCho(null)}
        onDongY={() => xoaCho && xoaBuoi.mutate(xoaCho.id)}
      />

      {/*
        Chọn trạng thái — dùng `Modal` chứ không `HopXacNhan`: đây là chọn MỘT TRONG NHIỀU, còn
        hộp xác nhận chỉ có đồng ý/huỷ.

        Chỉ liệt kê `TRANG_THAI_DAT_DUOC`. "Chưa bắt đầu"/"đang diễn ra"/"chưa chốt" KHÔNG có ở
        đây vì chúng suy từ giờ — cho đặt tay là tạo hai nguồn sự thật cho cùng câu hỏi.
      */}
      <Modal
        mo={trangThaiCho !== null}
        onDong={() => setTrangThaiCho(null)}
        tieuDe={t('buoiHoc.doiTrangThai')}
        moTa={
          trangThaiCho
            ? t('buoiHoc.moTaDoiTrangThai', {
                thuTu: trangThaiCho.thuTu,
                gio: gioVN(trangThaiCho.batDau),
              })
            : undefined
        }
        rong="sm"
      >
        <div className="grid gap-2">
          {TRANG_THAI_DAT_DUOC.map((tt) => {
            const dangChon = trangThaiCho?.trangThai === tt
            return (
              <button
                key={tt}
                type="button"
                disabled={datTrangThai.isPending}
                onClick={() =>
                  trangThaiCho && datTrangThai.mutate({ id: trangThaiCho.id, trangThai: tt })
                }
                className={
                  'flex items-center justify-between rounded-lg border px-3 py-2 text-left '
                  + 'text-sm transition-colors disabled:opacity-50 '
                  + (dangChon
                    ? 'border-primary bg-primary/5 font-medium'
                    : 'border-border hover:bg-muted/50')
                }
              >
                <span>{t(`trangThaiBuoiHoc.${tt}`)}</span>
                {/* Đánh dấu trạng thái ĐANG áp dụng để người dùng biết mình đang đổi TỪ đâu. */}
                {dangChon && (
                  <span className="text-xs text-muted-foreground">
                    {t('buoiHoc.dangApDung')}
                  </span>
                )}
              </button>
            )
          })}
        </div>
      </Modal>

      {buoiDiemDanh && (
        <BangDiemDanh
          buoi={buoiDiemDanh}
          onDong={() => setBuoiDiemDanh(null)}
          onXong={lamMoi}
        />
      )}
    </KhungNoiDung>
  )
}

/** Bước 2 wizard: khai tần suất, hệ thống sinh danh sách buổi. */
/**
 * Form sinh lịch — dùng cho CẢ hai việc:
 * - `theTheLich` (mặc định): thay cả lịch, xoá buổi chưa học.
 * - `noiTiep`: sinh thêm nối tiếp, **không xoá gì**.
 *
 * Cùng một form vì đầu vào giống hệt (tần suất, giờ, điều kiện dừng); chỉ khác endpoint và
 * lời cảnh báo. Viết hai form gần giống nhau là mời gọi chúng lệch nhau về sau.
 */
function FormSinhLich({
  lopHocId,
  daCoLich,
  noiTiep,
  onXong,
  onDong,
}: {
  lopHocId: string
  daCoLich: boolean
  /** true = gọi `sinh-them-buoi` thay vì `sinh-lich`. */
  noiTiep?: boolean
  onXong: () => void
  onDong: () => void
}) {
  const { t } = useTranslation()
  const { hoi, hop } = useXacNhan()
  const [thuChon, setThuChon] = useState<number[]>([2, 4, 6])
  const [ketThucTheo, setKetThucTheo] = useState<'soBuoi' | 'ngay'>('soBuoi')
  const [laHocBu, setLaHocBu] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const sinh = useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api.post(
        noiTiep ? `/lop-hoc/${lopHocId}/sinh-them-buoi` : `/lop-hoc/${lopHocId}/sinh-lich`,
        body,
      ),
    onSuccess: onXong,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const batThu = (ma: number) =>
    setThuChon((cu) => (cu.includes(ma) ? cu.filter((x) => x !== ma) : [...cu, ma]))

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)

    if (thuChon.length === 0) {
      setMaLoi('TAN_SUAT_TRONG')
      return
    }

    const loaiTruTho = ((fd.get('ngayLoaiTru') as string) || '')
      .split('\n')
      .map((d) => d.trim())
      .filter(Boolean)

    // Endpoint nối tiếp nhận `tuNgay`, endpoint thay lịch nhận `ngayKhaiGiang`.
    const ngay = String(fd.get('ngayKhaiGiang'))

    hoi({
      tieuDe: noiTiep ? t('buoiHoc.sinhThemBuoi') : t('buoiHoc.sinhLich'),
      thongDiep: noiTiep
        ? t('buoiHoc.hoiSinhThem')
        : daCoLich
          ? t('buoiHoc.hoiSinhLai')
          : t('buoiHoc.hoiSinhLich'),
      nguyHiem: !noiTiep && daCoLich,
      onDongY: () => {
        sinh.mutate({
          ...(noiTiep ? { tuNgay: ngay } : { ngayKhaiGiang: ngay }),
          // Backend dùng DayOfWeek: CN=0, T2=1… trùng với mã đang lưu.
          thuTrongTuan: thuChon,
          gioBatDau: String(fd.get('gioBatDau')) + ':00',
          gioKetThuc: String(fd.get('gioKetThuc')) + ':00',
          soBuoi: ketThucTheo === 'soBuoi' ? Number(fd.get('soBuoi')) : null,
          denNgay: ketThucTheo === 'ngay' ? String(fd.get('denNgay')) : null,
          ngayLoaiTru: loaiTruTho,
          // Chỉ có nghĩa ở chế độ nối tiếp; sinh lịch mới thì bỏ qua.
          ...(noiTiep
            ? {
                laHocBu,
                phongHoc: (fd.get('phongHoc') as string) || null,
                ghiChu: (fd.get('ghiChu') as string) || null,
              }
            : {}),
        })
      },
    })
  }

  return (
    <Modal
      mo
      onDong={onDong}
      tieuDe={noiTiep ? t('buoiHoc.sinhThemBuoi') : t('buoiHoc.sinhLich')}
    >
      <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
        {daCoLich && !noiTiep && (
          <div className="sm:col-span-2">
            <CanhBaoLoi>{t('buoiHoc.canhBaoSinhLai')}</CanhBaoLoi>
          </div>
        )}

        {noiTiep && (
          <p className="text-sm text-muted-foreground sm:col-span-2">
            {t('buoiHoc.goiYSinhThem')}
          </p>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ngayKhaiGiang">
            {noiTiep ? t('buoiHoc.tuNgay') : t('lopHoc.ngayKhaiGiang')}
          </Label>
          <Input id="ngayKhaiGiang" name="ngayKhaiGiang" type="date" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('buoiHoc.thuTrongTuan')}</Label>
          <div className="flex flex-wrap gap-1">
            {THU.map((th) => (
              <button
                key={th.ma}
                type="button"
                aria-pressed={thuChon.includes(th.ma)}
                onClick={() => batThu(th.ma)}
                className={
                  'h-9 w-11 rounded-md border text-xs transition-colors ' +
                  (thuChon.includes(th.ma)
                    ? 'border-[hsl(var(--primary))] bg-[hsl(var(--primary))]/10 font-medium'
                    : 'border-border hover:bg-muted')
                }
              >
                {t(`thu.${th.khoa}`)}
              </button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="gioBatDau">{t('buoiHoc.gioBatDau')}</Label>
          <Input id="gioBatDau" name="gioBatDau" type="time" defaultValue="18:00" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="gioKetThuc">{t('buoiHoc.gioKetThuc')}</Label>
          <Input id="gioKetThuc" name="gioKetThuc" type="time" defaultValue="20:00" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ketThucTheo">{t('buoiHoc.ketThucTheo')}</Label>
          <SelectTimKiem
            id="ketThucTheo"
            choPhepXoa={false}
            luaChon={[
              { giaTri: 'soBuoi', nhan: t('buoiHoc.theoSoBuoi') },
              { giaTri: 'ngay', nhan: t('buoiHoc.theoNgay') },
            ]}
            giaTri={ketThucTheo}
            onDoi={(v) => setKetThucTheo((v as 'soBuoi' | 'ngay') ?? 'soBuoi')}
          />
        </div>

        {ketThucTheo === 'soBuoi' ? (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soBuoi">{t('buoiHoc.theoSoBuoi')}</Label>
            {/* Nối tiếp thì mặc định 1 buổi (ca hay dùng nhất: thêm một buổi bù); sinh lịch
                mới thì 24 buổi — một khoá điển hình. */}
            <Input
              id="soBuoi" name="soBuoi" type="number" min={1} max={500}
              defaultValue={noiTiep ? 1 : 24}
            />
          </div>
        ) : (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="denNgay">{t('buoiHoc.theoNgay')}</Label>
            <Input id="denNgay" name="denNgay" type="date" />
          </div>
        )}

        <div className="flex flex-col gap-1.5 sm:col-span-2">
          <Label htmlFor="ngayLoaiTru">{t('buoiHoc.ngayLoaiTru')}</Label>
          <Textarea
            id="ngayLoaiTru"
            name="ngayLoaiTru"
            rows={2}
            placeholder={t('buoiHoc.ngayLoaiTruGoiY')}
          />
        </div>

        {/* Chỉ khi NỐI TIẾP: các trường của buổi bù. Sinh lịch chính khoá thì phòng và ghi
            chú lấy từ lớp, và cả một lịch không thể là "học bù". */}
        {noiTiep && (
          <>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="phongHoc">{t('lopHoc.phongHoc')}</Label>
              <Input id="phongHoc" name="phongHoc" placeholder={t('buoiHoc.macDinhTheoLop')} />
            </div>

            <label className="flex items-center gap-2 self-end pb-2 text-sm">
              <input
                type="checkbox"
                checked={laHocBu}
                onChange={(e) => setLaHocBu(e.target.checked)}
                className="h-4 w-4 rounded border-input"
              />
              {t('buoiHoc.laHocBu')}
            </label>

            <div className="flex flex-col gap-1.5 sm:col-span-2">
              <Label htmlFor="ghiChu">{t('lopHoc.ghiChu')}</Label>
              <Input id="ghiChu" name="ghiChu" />
            </div>
          </>
        )}

        {maLoi && (
          <div className="sm:col-span-2">
            <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
          </div>
        )}

        <div className="flex justify-end gap-2 sm:col-span-2">
          <Button type="button" variant="outline" onClick={onDong}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={sinh.isPending}>
            {t('buoiHoc.sinhLich')}
          </Button>
        </div>
      </form>
      {hop}
    </Modal>
  )
}

/** Bảng điểm danh một buổi — hai nguồn: học viên tự khai và giáo viên xác nhận. */
