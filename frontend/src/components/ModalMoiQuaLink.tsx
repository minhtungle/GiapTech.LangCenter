import { useEffect, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import QRCode from 'qrcode'
import { Check, ClipboardCopy, Link2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi, Input, Label, Textarea } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Sinh link + QR mời một đối thủ chưa liên kết (FR-18).
 *
 * Token trả về ĐÚNG MỘT LẦN — DB chỉ lưu hash. Nên modal này phải hiện link ngay và nhắc người
 * dùng copy trước khi đóng; mất là phải thu hồi rồi tạo lại.
 */
export function ModalMoiQuaLink({
  doiThuId,
  tenDoiThu,
  tranDauId,
  thoiGianTran,
  onDong,
}: {
  doiThuId: string
  tenDoiThu: string
  tranDauId?: string | null
  /** Giờ trận nếu mời cho một trận cụ thể — điền sẵn vào ô thời gian. */
  thoiGianTran?: string | null
  onDong: () => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [thoiGian, setThoiGian] = useState(
    thoiGianTran ? new Date(thoiGianTran).toISOString().slice(0, 16) : '',
  )
  const [diaDiem, setDiaDiem] = useState('')
  const [loiNhan, setLoiNhan] = useState('')
  const [link, setLink] = useState<string | null>(null)
  const [daChep, setDaChep] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const oQr = useRef<HTMLCanvasElement>(null)

  const tao = useMutation({
    mutationFn: async () =>
      (
        await api.post<{ id: string; token: string; hetHan: string }>('/moi-qua-link', {
          doiThuId,
          tranDauId: tranDauId ?? null,
          thoiGianDeXuat: thoiGian ? new Date(thoiGian).toISOString() : null,
          diaDiem: diaDiem.trim() || null,
          loiNhan: loiNhan.trim() || null,
        })
      ).data,
    onSuccess: (kq) => {
      // Link tuyệt đối: người nhận mở từ Zalo trên máy khác, đường dẫn tương đối vô dụng.
      setLink(`${window.location.origin}/loi-moi?token=${kq.token}`)
      setMaLoi(null)
      void qc.invalidateQueries({ queryKey: ['moi-qua-link'] })
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  // Vẽ QR sau khi có link. Vẽ ở frontend chứ không sinh ảnh ở backend: backend sinh thì phải
  // lưu file và dọn file, mà không thêm giá trị gì.
  useEffect(() => {
    if (link && oQr.current) {
      void QRCode.toCanvas(oQr.current, link, { width: 200, margin: 1 })
    }
  }, [link])

  const chep = async () => {
    if (!link) return
    await navigator.clipboard.writeText(link)
    setDaChep(true)
    setTimeout(() => setDaChep(false), 2500)
  }

  return (
    <Modal mo onDong={onDong} tieuDe={t('moiLink.taoTieuDe', { ten: tenDoiThu })} rong="md">
      {!link ? (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            tao.mutate()
          }}
          className="flex flex-col gap-3"
        >
          <p className="text-sm text-muted-foreground">{t('moiLink.taoMoTa')}</p>

          <div>
            <Label htmlFor="linkThoiGian">{t('congDong.thoiGianDeXuat')}</Label>
            <Input
              id="linkThoiGian"
              type="datetime-local"
              value={thoiGian}
              onChange={(e) => setThoiGian(e.target.value)}
            />
          </div>

          <div>
            <Label htmlFor="linkDiaDiem">{t('congDong.diaDiem')}</Label>
            <Input
              id="linkDiaDiem"
              value={diaDiem}
              onChange={(e) => setDiaDiem(e.target.value)}
              placeholder={t('congDong.diaDiemGoiY')}
            />
          </div>

          <div>
            <Label htmlFor="linkLoiNhan">{t('congDong.loiNhan')}</Label>
            <Textarea
              id="linkLoiNhan"
              rows={3}
              value={loiNhan}
              onChange={(e) => setLoiNhan(e.target.value)}
              placeholder={t('moiLink.loiNhanGoiY')}
            />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={onDong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={tao.isPending}>
              <Link2 className="h-4 w-4" />
              {tao.isPending ? t('chung.dangTai') : t('moiLink.taoLink')}
            </Button>
          </ModalChan>
        </form>
      ) : (
        <div className="flex flex-col items-center gap-4">
          <canvas ref={oQr} className="rounded-md border border-border" />

          <div className="w-full">
            <Label htmlFor="linkDaTao">{t('moiLink.linkDaTao')}</Label>
            <div className="flex gap-2">
              <Input id="linkDaTao" readOnly value={link} className="font-mono text-xs" />
              <Button type="button" variant="outline" onClick={() => void chep()}>
                {daChep ? <Check className="h-4 w-4" /> : <ClipboardCopy className="h-4 w-4" />}
              </Button>
            </div>
          </div>

          {/* Token chỉ trả về một lần — nhắc rõ, không thì họ đóng modal rồi mất link. */}
          <p className="text-sm text-[hsl(var(--status-draw))]">{t('moiLink.chepNgay')}</p>

          <ModalChan>
            <Button type="button" onClick={onDong}>
              {t('chung.dong')}
            </Button>
          </ModalChan>
        </div>
      )}
    </Modal>
  )
}
