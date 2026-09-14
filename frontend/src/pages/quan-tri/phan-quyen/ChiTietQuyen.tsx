import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ArrowLeft, Sparkles } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi, Input, Label } from '@/components/ui'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import { MaTranQuyen } from './MaTranQuyen'
import type { DanhMucDto, MauVaiTroDto, QuyenDto } from './kieu'
import {
  apMau, demTheoChucNang, gomMaTran, oBoSot, tapOTu, thaoTacCua,
} from './kieu'

/**
 * FR-05 — trang sửa một nhóm quyền.
 *
 * ## Vì sao là TRANG, không phải modal
 *
 * Ma trận phân quyền có 30 chức năng × tới 7 thao tác, chia ba hệ thống con, cộng một nhóm
 * quyền phạm vi dữ liệu và (với nhóm cũ) một cảnh báo ô lạc hậu. Nhồi vào modal thì:
 *
 * - Modal phải cuộn trong chính nó, mà quy ước dự án đã giới hạn chiều cao ở thẻ `<dialog>` —
 *   nội dung dài thành một khung cuộn hẹp nằm giữa màn hình rộng.
 * - Không gửi được link tới đúng nhóm quyền đang bàn, F5 mất chỗ, nút Back không dùng được.
 * - Tài liệu UI/UX đã chốt: *"Bản ghi có nhiều mặt → trang riêng có tab, không phải nhiều
 *   modal"*, và ma trận phân quyền được xếp **ưu tiên desktop**.
 *
 * Thêm/đổi tên nhóm (một ô nhập) vẫn là modal ở trang danh sách — đúng quy ước "thêm/cập nhật
 * không cần chuyển view thì dùng modal". Chỉ việc cấu hình ma trận mới lên trang riêng.
 *
 * ## Trạng thái giữ NGUYÊN VẸN qua các tab
 *
 * `oDaChon` là một tập duy nhất cho cả ba hệ thống; tab chỉ lọc phần hiển thị. Mỗi tab một
 * state riêng thì chuyển tab rồi Lưu sẽ âm thầm xoá quyền của hệ thống khác — đúng loại lỗi
 * mất dữ liệu mà quy tắc #1 cấm.
 */
export default function ChiTietQuyen() {
  const { id } = useParams<{ id: string }>()
  const nav = useNavigate()
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const duocSua = coQuyen('PhanQuyen', 'CauHinhQuyen')

  const [tenQuyen, setTenQuyen] = useState('')
  const [oDaChon, setODaChon] = useState<Set<string>>(new Set())
  const [tabHeThong, setTabHeThong] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daNap, setDaNap] = useState(false)

  const { data: danhMuc } = useQuery({
    queryKey: ['quyen-danh-muc'],
    queryFn: async () => (await api.get<DanhMucDto>('/quyen/danh-muc')).data,
  })

  const { data: quyens, isLoading } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenDto[]>('/quyen')).data,
  })

  const quyen = quyens?.find((q) => q.id === id)

  // Nạp một lần khi có dữ liệu. Không dùng `key` remount vì sẽ mất luôn tab đang xem; cờ
  // `daNap` để lần refetch sau (invalidate sau khi lưu) không ghi đè ô người dùng vừa sửa.
  useEffect(() => {
    if (!quyen || daNap) return
    setTenQuyen(quyen.tenQuyen)
    setODaChon(tapOTu(quyen.chucNangs))
    setDaNap(true)
  }, [quyen, daNap])

  const cacTab = useMemo(
    () => (danhMuc?.heThongs ?? []).filter((x) => !x.dungChung),
    [danhMuc],
  )
  const nhomDungChung = (danhMuc?.heThongs ?? []).find((x) => x.dungChung)
  const tab = tabHeThong ?? cacTab[0]?.ma ?? null

  /** Chức năng hiện trong tab: của hệ thống đang chọn + nhóm dùng chung. */
  const chucNangTrongTab = useMemo(() => {
    if (!danhMuc) return []
    return [
      ...(cacTab.find((x) => x.ma === tab)?.chucNangs ?? []),
      ...(nhomDungChung?.chucNangs ?? []),
    ].filter((cn) => thaoTacCua(danhMuc, cn).length > 0)
  }, [danhMuc, cacTab, tab, nhomDungChung])

  /**
   * Tách hai nhóm hành xử KHÁC NHAU, không trộn.
   *
   * Quyền gọi endpoint: thiếu thì bị chặn, thấy lỗi ngay. Quyền phạm vi dữ liệu: thiếu thì vào
   * được màn nhưng danh sách rỗng (tưởng hỏng), cấp thừa thì **rò rỉ dữ liệu mà không báo gì**.
   * Trộn chung làm người cấu hình không phân biệt được — đó là cách nợ N14 xảy ra.
   */
  const laPhamVi = (cn: string) => danhMuc?.phamViDuLieu.includes(cn) ?? false
  const chucNangGoiApi = chucNangTrongTab.filter((cn) => !laPhamVi(cn))
  const chucNangPhamVi = chucNangTrongTab.filter(laPhamVi)

  const boSot = oBoSot(danhMuc, oDaChon)
  const tongDaChon = danhMuc
    ? demTheoChucNang(danhMuc, oDaChon, Object.keys(danhMuc.thaoTacTheoChucNang))
    : 0

  const doiO = (khoa: string, bat: boolean) =>
    setODaChon((truoc) => {
      const moi = new Set(truoc)
      if (bat) moi.add(khoa)
      else moi.delete(khoa)
      return moi
    })

  const luu = useMutation({
    mutationFn: async () => {
      await api.put(`/quyen/${id}`, {
        id,
        tenQuyen,
        moTa: quyen?.moTa ?? null,
        chucNangs: gomMaTran(oDaChon),
      })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quyen'] })
      nav('/quan-tri/phan-quyen')
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const apDungMau = (mau: MauVaiTroDto) =>
    hoi({
      tieuDe: t('quyen.apMauTieuDe'),
      thongDiep: t('quyen.apMauHoi', { ten: mau.ten, so: mau.quyens.length }),
      onDongY: () => setODaChon(apMau(danhMuc, oDaChon, mau)),
    })

  if (isLoading || !danhMuc) {
    return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
  }

  if (!quyen) {
    return (
      <div className="flex flex-col items-start gap-3">
        <p className="text-sm text-muted-foreground">{t('quyen.khongThayNhom')}</p>
        <Button variant="outline" onClick={() => nav('/quan-tri/phan-quyen')}>
          <ArrowLeft className="h-4 w-4" />
          {t('quyen.veDanhSach')}
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Đường lùi rõ ràng: tiêu đề trang chỉ ghi "Phân quyền" nên không tự thấy mình đang ở
          trang con của nhóm nào. */}
      <Link
        to="/quan-tri/phan-quyen"
        className="inline-flex w-fit items-center gap-1.5 text-sm text-muted-foreground
                   hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('quyen.veDanhSach')}
      </Link>

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="tenQuyen">{t('quyen.tenQuyen')}</Label>
          <div className="flex items-center gap-2">
            <Input
              id="tenQuyen"
              className="max-w-xs"
              value={tenQuyen}
              disabled={!duocSua}
              onChange={(e) => setTenQuyen(e.target.value)}
            />
            {/* Chữ thường, KHÔNG `Badge`: badge là `rounded-full` một dòng nên câu "3 tài
                khoản đang dùng" bị gói thành hai dòng và cắt mất chữ. Badge dành cho nhãn
                một hai từ. Cũng không dùng màu nhấn: số tài khoản đang dùng là thông tin,
                mà theo quy ước màu nhấn nghĩa "cần chú ý". */}
            <span className="shrink-0 text-sm text-muted-foreground">
              {t('quyen.dangDung', { so: quyen.soTaiKhoan })}
            </span>
          </div>
        </div>

        {/*
          Mẫu vai trò. Đặt ở ĐẦU trang vì nó là việc làm trước: chọn điểm khởi đầu rồi tinh
          chỉnh. Để dưới cuối thì người dùng đã tick tay xong mới thấy, lúc đó bấm vào là mất
          công vừa làm.
        */}
        {duocSua && danhMuc.mauVaiTro.length > 0 && (
          <div className="flex flex-col gap-1.5">
            <span className="flex items-center gap-1 text-xs text-muted-foreground">
              <Sparkles className="h-3.5 w-3.5" />
              {t('quyen.batDauTuMau')}
            </span>
            <div className="flex flex-wrap gap-1.5">
              {danhMuc.mauVaiTro.map((m) => (
                <Button key={m.ten} variant="outline" size="sm" onClick={() => apDungMau(m)}>
                  {m.ten}
                </Button>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Không dùng `CanhBaoLoi`: thiếu quyền sửa không phải LỖI, và style đỏ của nó sẽ báo
          động sai. Đây là một ghi chú trạng thái. */}
      {!duocSua && (
        <p
          className="rounded-md border border-border bg-muted/50 px-3 py-2 text-sm
                     text-muted-foreground"
        >
          {t('quyen.chiXem')}
        </p>
      )}

      <div>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Label>{t('quyen.maTran')}</Label>
          <span className="text-xs text-muted-foreground">
            {t('quyen.tongDaChon', { so: tongDaChon })}
          </span>
        </div>

        {/*
          Tab theo hệ thống. Chuyển tab KHÔNG mất ô đã tích ở tab khác — `oDaChon` là một tập
          duy nhất cho cả ba hệ thống, tab chỉ lọc phần hiển thị.
        */}
        {cacTab.length > 1 && (
          <div className="mt-2 flex flex-wrap gap-1 rounded-lg border border-border p-1">
            {cacTab.map((x) => {
              const dem = demTheoChucNang(danhMuc, oDaChon, x.chucNangs)
              return (
                <button
                  key={x.ma}
                  type="button"
                  onClick={() => setTabHeThong(x.ma)}
                  className={
                    'rounded-md px-3 py-1.5 text-sm font-medium transition-colors '
                    + (tab === x.ma
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-muted')
                  }
                >
                  {t(`heThong.${x.ma}`, x.ma)}
                  {dem > 0 && (
                    <span
                      className={
                        'ml-1.5 text-xs '
                        + (tab === x.ma ? 'opacity-80' : 'text-muted-foreground')
                      }
                    >
                      {dem}
                    </span>
                  )}
                </button>
              )
            })}
          </div>
        )}

        <div className="mt-3 flex flex-col gap-4">
          <MaTranQuyen
            danhMuc={danhMuc}
            dsChucNang={chucNangGoiApi}
            oDaChon={oDaChon}
            onDoi={doiO}
            chiDoc={!duocSua}
          />

          {chucNangPhamVi.length > 0 && (
            <section>
              <h2 className="text-sm font-semibold">{t('quyen.phamViDuLieu')}</h2>
              <p className="mb-2 mt-0.5 max-w-3xl text-xs text-muted-foreground">
                {t('quyen.phamViGoiY')}
              </p>
              <MaTranQuyen
                danhMuc={danhMuc}
                dsChucNang={chucNangPhamVi}
                oDaChon={oDaChon}
                onDoi={doiO}
                chiDoc={!duocSua}
                laPhamVi
              />
            </section>
          )}

          {boSot.length > 0 && (
            <CanhBaoLoi>{t('quyen.oCu', { ds: boSot.join(', ') })}</CanhBaoLoi>
          )}
        </div>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {/*
        Thanh lưu DÍNH ĐÁY. Trang dài hơn một màn hình, nên nút Lưu ở cuối luồng tài liệu sẽ
        buộc người dùng cuộn xuống đáy mỗi lần đổi một ô ở giữa.
      */}
      <div
        className="sticky bottom-0 -mx-4 flex items-center justify-end gap-2 border-t
                   border-border bg-background/95 px-4 py-3 backdrop-blur"
      >
        <Button variant="outline" onClick={() => nav('/quan-tri/phan-quyen')}>
          {t('chung.huy')}
        </Button>
        {duocSua && (
          <Button
            disabled={luu.isPending || !tenQuyen}
            onClick={() =>
              hoi({
                tieuDe: t('chung.xacNhanLuu'),
                thongDiep: t('quyen.hoiLuu', { ten: tenQuyen }),
                onDongY: () => luu.mutate(),
              })
            }
          >
            {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
          </Button>
        )}
      </div>
      {hop}
    </div>
  )
}
