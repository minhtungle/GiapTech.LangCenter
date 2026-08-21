import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import QRCode from 'qrcode'
import { Check, ClipboardCopy, Link2, Send, Undo2, XCircle } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, Input, Label, Table, Td, Th, Textarea, TrangTrong,
} from '@/components/ui'
import { HopXacNhan } from '@/components/ui/HopXacNhan'

/**
 * FR-19 — tab Đăng ký trong chi tiết trận.
 *
 * Đặt ở ĐÂY chứ không ở Hòm thư: lời mời đăng ký thuộc về một trận cụ thể, nhưng nút tạo nó vốn
 * nằm ở Hòm thư nên trưởng nhóm phải rời trận đang xem, sang Hòm thư, tìm đúng lời mời. Cùng loại
 * lỗi với "muốn thêm đối thủ phải rời form thêm trận" đã sửa 17/08.
 *
 * Hòm thư vẫn hiển thị lời mời để cầu thủ trả lời — chỉ chỗ TẠO và QUẢN LÝ chuyển về đây.
 */

interface CauThuNgan {
  id: string
  hoTen: string
  soAo: number | null
}

interface PhanHoiDto {
  cauThuId: string
  hoTen: string
  soAo: number | null
  traLoi: string
  ghiChu: string | null
  soLanSua: number
  quaLink: boolean
}

interface LoiMoiDto {
  id: string
  loiNhan: string | null
  hanTraLoi: string | null
  daDong: boolean
  linkHetHan: string | null
  linkDaThuHoi: boolean
}

/**
 * Khớp `TraLoiThamGia` ở backend — API serialize enum thành **CHUỖI** tên, không phải số.
 *
 * Bản đầu tôi dùng số (0/1/2/3) và mọi phép so đều sai im lặng: bảng hiện "Chưa trả lời" cho
 * người đã trả lời, dòng thống kê đếm 0. Không có lỗi nào ở console — chỉ thấy khi xem ảnh chụp
 * và so với dữ liệu thật trong DB.
 *
 * Lệnh GHI thì nhận cả số lẫn chuỗi, nên gửi số vẫn thành công — điều đó che mất lỗi ở phần đọc.
 */
const CHUA_TRA_LOI = 'ChuaTraLoi'
const THAM_GIA = 'ThamGia'
const KHONG_THAM_GIA = 'KhongThamGia'
const CHUA_CHAC = 'ChuaChac'

/** Khớp `HanLinkDangKy` ở backend — bốn mốc, không nhận số phút tuỳ ý. */
const MOC_HAN = [
  { giaTri: 0, khoa: 'namPhut' },
  { giaTri: 1, khoa: 'motGio' },
  { giaTri: 2, khoa: 'truocGioDa' },
  { giaTri: 3, khoa: 'bayNgay' },
] as const

export function TabDangKy({
  tranDauId,
  onLoi,
}: {
  tranDauId: string
  onLoi: (ma: string | null) => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()

  const [loiNhan, setLoiNhan] = useState('')
  const [daChon, setDaChon] = useState<Set<string> | null>(null)
  const [han, setHan] = useState<number>(2)
  const [link, setLink] = useState<string | null>(null)
  const [daChep, setDaChep] = useState(false)
  const [canhBaoXoa, setCanhBaoXoa] = useState<{ ten: string[] } | null>(null)
  const oQr = useRef<HTMLCanvasElement>(null)

  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu-ngan'],
    queryFn: async () =>
      (await api.get<{ duLieu: CauThuNgan[] }>('/cau-thu', { params: { soDong: 200 } })).data
        .duLieu,
  })

  const { data: loiMoi } = useQuery({
    queryKey: ['dang-ky', tranDauId],
    queryFn: async () =>
      (await api.get<LoiMoiDto | null>(`/hom-thu/dang-ky/theo-tran/${tranDauId}`)).data,
    // 404 = trận này chưa có lời mời, không phải lỗi.
    retry: false,
  })

  const { data: phanHois } = useQuery({
    queryKey: ['dang-ky-phan-hoi', loiMoi?.id],
    enabled: !!loiMoi?.id,
    queryFn: async () =>
      (await api.get<PhanHoiDto[]>(`/hom-thu/dang-ky/${loiMoi!.id}/phan-hoi`)).data,
  })

  /**
   * Đặt danh sách tick ban đầu.
   *
   * - Đã có lời mời → tick đúng những người ĐANG trong lời mời.
   * - Chưa có lời mời → tick hết (giữ hành vi cũ "mời tất cả" làm mặc định).
   *
   * Phải chờ `phanHois` tải xong trước khi quyết định, nếu không sẽ tick hết 16 người trong khi
   * lời mời chỉ có 14 — lỗi thật đã gặp 21/08: bấm "Lưu danh sách" ngay sau khi mở tab sẽ âm
   * thầm mời lại 2 người trưởng nhóm vừa bỏ.
   */
  const dangChoPhanHoi = !!loiMoi?.id && phanHois === undefined

  useEffect(() => {
    if (daChon !== null || dangChoPhanHoi) return
    if (phanHois) setDaChon(new Set(phanHois.map((p) => p.cauThuId)))
    else if (cauThus?.length) setDaChon(new Set(cauThus.map((c) => c.id)))
  }, [cauThus, phanHois, daChon, dangChoPhanHoi])

  useEffect(() => {
    if (link && oQr.current) void QRCode.toCanvas(oQr.current, link, { width: 180, margin: 1 })
  }, [link])

  const guiLoiMoi = useMutation({
    mutationFn: async () =>
      (
        await api.post<string>('/hom-thu/dang-ky', {
          tranDauId,
          loiNhan: loiNhan.trim() || null,
          hanTraLoi: null,
          cauThuIds: daChon ? [...daChon] : null,
        })
      ).data,
    onSuccess: () => {
      onLoi(null)
      void qc.invalidateQueries({ queryKey: ['dang-ky', tranDauId] })
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const taoLink = useMutation({
    mutationFn: async () =>
      (
        await api.post<{ token: string; hetHan: string }>(
          `/dang-ky-nhanh/link/${loiMoi!.id}`,
          { han },
        )
      ).data,
    onSuccess: (kq) => {
      // Link tuyệt đối: người nhận mở từ Zalo trên máy khác, đường dẫn tương đối vô dụng.
      setLink(`${window.location.origin}/dang-ky-nhanh?token=${kq.token}`)
      onLoi(null)
      void qc.invalidateQueries({ queryKey: ['dang-ky', tranDauId] })
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const thuHoi = useMutation({
    mutationFn: async () => api.post(`/dang-ky-nhanh/link/${loiMoi!.id}/thu-hoi`),
    onSuccess: () => {
      setLink(null)
      void qc.invalidateQueries({ queryKey: ['dang-ky', tranDauId] })
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  /**
   * Lưu danh sách đã tick. Hỏi trước khi xoá câu trả lời (quy tắc #1) — backend cũng chặn nên
   * đây chỉ là để hộp thoại nói đúng TÊN ai sẽ mất, không phải lớp bảo vệ.
   */
  const luuDanhSach = useMutation({
    mutationFn: async (dongY: boolean) =>
      api.put(`/dang-ky-nhanh/link/${loiMoi!.id}/danh-sach`, {
        cauThuIds: [...(daChon ?? [])],
        dongYXoaCauTraLoi: dongY,
      }),
    onSuccess: () => {
      setCanhBaoXoa(null)
      onLoi(null)
      void qc.invalidateQueries({ queryKey: ['dang-ky-phan-hoi', loiMoi?.id] })
    },
    onError: (e) => {
      if (layMaLoi(e) === 'XOA_SE_MAT_CAU_TRA_LOI') return
      onLoi(layMaLoi(e))
    },
  })

  /** Ai sẽ mất câu trả lời nếu lưu danh sách hiện tại. */
  const xemTruoc = useMutation({
    mutationFn: async () =>
      (
        await api.post<{ cauThuId: string; hoTen: string; traLoi: string }[]>(
          `/dang-ky-nhanh/link/${loiMoi!.id}/xem-truoc-mat-du-lieu`,
          { cauThuIds: [...(daChon ?? [])] },
        )
      ).data,
    onSuccess: (seMat) => {
      if (seMat.length === 0) luuDanhSach.mutate(false)
      else setCanhBaoXoa({ ten: seMat.map((x) => x.hoTen) })
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const doi = (id: string) =>
    setDaChon((truoc) => {
      const moi = new Set(truoc ?? [])
      if (moi.has(id)) moi.delete(id)
      else moi.add(id)
      return moi
    })

  const nhanTraLoi = (tl: string) =>
    tl === THAM_GIA
      ? t('homThu.tl.ThamGia')
      : tl === KHONG_THAM_GIA
        ? t('homThu.tl.KhongThamGia')
        : tl === CHUA_CHAC
          ? t('homThu.tl.ChuaChac')
          : t('dangKyNhanh.chuaTraLoi')

  const tomTat = useMemo(() => {
    const p = phanHois ?? []
    return {
      thamGia: p.filter((x) => x.traLoi === THAM_GIA).length,
      chuaChac: p.filter((x) => x.traLoi === CHUA_CHAC).length,
      khong: p.filter((x) => x.traLoi === KHONG_THAM_GIA).length,
      chuaTraLoi: p.filter((x) => x.traLoi === CHUA_TRA_LOI).length,
    }
  }, [phanHois])

  const linkConHieuLuc =
    !!loiMoi?.linkHetHan && !loiMoi.linkDaThuHoi && new Date(loiMoi.linkHetHan) > new Date()

  // ----- Chưa có lời mời: form gửi, có chọn ai được mời -----
  if (!loiMoi)
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">{t('dangKyNhanh.chuaGuiMoTa')}</p>

        <div>
          <Label htmlFor="dkLoiNhan">{t('congDong.loiNhan')}</Label>
          <Textarea
            id="dkLoiNhan"
            rows={2}
            value={loiNhan}
            onChange={(e) => setLoiNhan(e.target.value)}
            placeholder={t('dangKyNhanh.loiNhanGoiY')}
          />
        </div>

        <DanhSachTick
          cauThus={cauThus ?? []}
          daChon={daChon}
          onDoi={doi}
          onTatCa={() => setDaChon(new Set((cauThus ?? []).map((c) => c.id)))}
          onBoHet={() => setDaChon(new Set())}
        />

        <div>
          <Button
            disabled={guiLoiMoi.isPending || !daChon?.size}
            onClick={() => guiLoiMoi.mutate()}
          >
            <Send className="h-4 w-4" />
            {guiLoiMoi.isPending ? t('chung.dangTai') : t('dangKyNhanh.guiLoiMoi')}
          </Button>
        </div>
      </div>
    )

  // ----- Đã có lời mời -----
  return (
    <div className="flex flex-col gap-5">
      {loiMoi.daDong && <Badge variant="muted">{t('dangKyNhanh.daDong')}</Badge>}

      <div className="flex flex-wrap gap-4 text-sm">
        <span className="text-[hsl(var(--status-win))]">
          {t('homThu.tl.ThamGia')}: <strong>{tomTat.thamGia}</strong>
        </span>
        <span className="text-[hsl(var(--status-draw))]">
          {t('homThu.tl.ChuaChac')}: <strong>{tomTat.chuaChac}</strong>
        </span>
        <span className="text-destructive">
          {t('homThu.tl.KhongThamGia')}: <strong>{tomTat.khong}</strong>
        </span>
        <span className="text-muted-foreground">
          {t('dangKyNhanh.chuaTraLoi')}: <strong>{tomTat.chuaTraLoi}</strong>
        </span>
      </div>

      {/* ----- Link/QR ----- */}
      {!loiMoi.daDong && (
        <div className="flex flex-col gap-3 rounded-md border border-border p-3">
          <div className="flex flex-wrap items-end gap-2">
            <div>
              <Label htmlFor="dkHan">{t('dangKyNhanh.hanLink')}</Label>
              <select
                id="dkHan"
                value={han}
                onChange={(e) => setHan(Number(e.target.value))}
                className="h-9 rounded-md border border-input bg-background px-3 text-sm"
              >
                {MOC_HAN.map((m) => (
                  <option key={m.giaTri} value={m.giaTri}>
                    {t(`dangKyNhanh.han.${m.khoa}`)}
                  </option>
                ))}
              </select>
            </div>
            <Button
              variant="outline"
              disabled={taoLink.isPending}
              onClick={() => taoLink.mutate()}
            >
              <Link2 className="h-4 w-4" />
              {linkConHieuLuc ? t('dangKyNhanh.taoLinkMoi') : t('dangKyNhanh.taoLink')}
            </Button>
            {linkConHieuLuc && (
              <Button
                variant="outline"
                disabled={thuHoi.isPending}
                onClick={() => thuHoi.mutate()}
              >
                <XCircle className="h-4 w-4" />
                {t('dangKyNhanh.thuHoi')}
              </Button>
            )}
          </div>

          {/* Tạo link MỚI thì link cũ chết — nói rõ để trưởng nhóm không gửi lại link cũ. */}
          {linkConHieuLuc && !link && (
            <p className="text-xs text-muted-foreground">
              {t('dangKyNhanh.linkConHieuLuc', {
                den: new Date(loiMoi.linkHetHan!).toLocaleString('vi-VN'),
              })}
            </p>
          )}

          {link && (
            <div className="flex flex-wrap items-start gap-4">
              <canvas ref={oQr} className="rounded-md border border-border" />
              <div className="min-w-0 flex-1">
                <Label htmlFor="dkLink">{t('moiLink.linkDaTao')}</Label>
                <div className="flex gap-2">
                  <Input id="dkLink" readOnly value={link} className="font-mono text-xs" />
                  <Button
                    variant="outline"
                    onClick={() => {
                      void navigator.clipboard.writeText(link)
                      setDaChep(true)
                      setTimeout(() => setDaChep(false), 2500)
                    }}
                  >
                    {daChep ? <Check className="h-4 w-4" /> : <ClipboardCopy className="h-4 w-4" />}
                  </Button>
                </div>
                {/* Token chỉ trả về một lần — DB lưu hash, không lấy lại được. */}
                <p className="mt-2 text-sm text-[hsl(var(--status-draw))]">
                  {t('moiLink.chepNgay')}
                </p>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ----- Bảng phản hồi ----- */}
      {phanHois?.length ? (
        <Table>
          <thead>
            <tr>
              <Th>{t('cauThu.soAo')}</Th>
              <Th>{t('cauThu.hoTen')}</Th>
              <Th>{t('dangKyNhanh.traLoi')}</Th>
              <Th>{t('dangKyNhanh.nguon')}</Th>
              <Th>{t('congDong.loiNhan')}</Th>
            </tr>
          </thead>
          <tbody>
            {phanHois.map((p) => (
              <tr key={p.cauThuId}>
                <Td>{p.soAo ?? '—'}</Td>
                <Td>{p.hoTen}</Td>
                <Td>{nhanTraLoi(p.traLoi)}</Td>
                <Td>
                  {/* Câu trả lời qua link KHÔNG xác thực được ai bấm — trưởng nhóm cần thấy. */}
                  {p.quaLink && <Badge variant="muted">{t('dangKyNhanh.quaLink')}</Badge>}
                  {p.soLanSua > 0 && (
                    <span className="ml-1 inline-flex items-center gap-0.5 text-xs text-muted-foreground">
                      <Undo2 className="h-3 w-3" />
                      {t('dangKyNhanh.soLanSua', { so: p.soLanSua })}
                    </span>
                  )}
                </Td>
                <Td className="text-muted-foreground">{p.ghiChu ?? '—'}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      ) : (
        <TrangTrong thongDiep={t('dangKyNhanh.chuaCoPhanHoi')} />
      )}

      {/* ----- Sửa danh sách người được mời ----- */}
      {!loiMoi.daDong && (
        <div className="flex flex-col gap-3">
          <DanhSachTick
            cauThus={cauThus ?? []}
            daChon={daChon}
            onDoi={doi}
            onTatCa={() => setDaChon(new Set((cauThus ?? []).map((c) => c.id)))}
            onBoHet={() => setDaChon(new Set())}
          />
          <div>
            <Button
              variant="outline"
              disabled={xemTruoc.isPending || luuDanhSach.isPending || !daChon?.size}
              onClick={() => xemTruoc.mutate()}
            >
              {t('dangKyNhanh.luuDanhSach')}
            </Button>
          </div>
        </div>
      )}

      {canhBaoXoa && (
        <HopXacNhan
          mo
          tieuDe={t('dangKyNhanh.xacNhanXoaTieuDe')}
          // Nêu TÊN, không phải "1 người": trưởng nhóm cần biết cụ thể ai để quyết định.
          thongDiep={t('dangKyNhanh.xacNhanXoaNoiDung', { ten: canhBaoXoa.ten.join(', ') })}
          nhanDongY={t('dangKyNhanh.vanXoa')}
          onDongY={() => luuDanhSach.mutate(true)}
          onHuy={() => setCanhBaoXoa(null)}
        />
      )}
    </div>
  )
}

/** Danh sách tick — dùng ở cả lúc gửi lời mời và lúc sửa danh sách. */
function DanhSachTick({
  cauThus,
  daChon,
  onDoi,
  onTatCa,
  onBoHet,
}: {
  cauThus: CauThuNgan[]
  daChon: Set<string> | null
  onDoi: (id: string) => void
  onTatCa: () => void
  onBoHet: () => void
}) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <Label>
          {t('dangKyNhanh.aiDuocMoi')} ({daChon?.size ?? 0}/{cauThus.length})
        </Label>
        <div className="flex gap-1">
          <Button type="button" variant="ghost" size="sm" onClick={onTatCa}>
            {t('dangKyNhanh.chonTatCa')}
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={onBoHet}>
            {t('dangKyNhanh.boHet')}
          </Button>
        </div>
      </div>

      <div className="grid gap-1 sm:grid-cols-2 lg:grid-cols-3">
        {cauThus.map((c) => (
          <label
            key={c.id}
            className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-muted/60"
          >
            <input
              type="checkbox"
              checked={daChon?.has(c.id) ?? false}
              onChange={() => onDoi(c.id)}
              className="h-4 w-4 rounded border-input"
            />
            {c.soAo !== null && (
              <span className="w-6 text-center font-mono text-xs text-muted-foreground">
                {c.soAo}
              </span>
            )}
            <span className="truncate">{c.hoTen}</span>
          </label>
        ))}
      </div>
    </div>
  )
}
