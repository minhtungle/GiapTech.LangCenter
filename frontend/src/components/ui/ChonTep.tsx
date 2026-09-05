import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Download, Paperclip, Trash2, Upload } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi } from '@/components/ui'

export interface TepDto {
  id: string
  tenGoc: string
  loaiNoiDung: string
  kichThuoc: number
}

const coKB = (n: number) =>
  n < 1024 * 1024 ? `${Math.round(n / 1024)} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`

/**
 * Danh sách tệp đính kèm + nút tải lên, dùng chung cho bài tập, bài nộp, tài liệu.
 *
 * Tải lên gọi thẳng `/tep` với `loai` + `doiTuongId`, nên component không cần biết mình đang
 * gắn vào loại nào — thêm loại thứ sáu không phải sửa gì ở đây.
 */
export function ChonTep({
  loai,
  doiTuongId,
  teps,
  onDoi,
  chiDoc = false,
}: {
  loai: 'BaiTap' | 'BaiNop' | 'BaiKiemTra' | 'BaiLam' | 'TaiLieu'
  doiTuongId: string
  teps: TepDto[]
  onDoi: () => void
  chiDoc?: boolean
}) {
  const { t } = useTranslation()
  const oTep = useRef<HTMLInputElement>(null)
  const [dangTai, setDangTai] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const taiLen = async (f: File) => {
    setDangTai(true)
    setMaLoi(null)
    try {
      const fd = new FormData()
      fd.append('tep', f)
      await api.post(`/tep?loai=${loai}&doiTuongId=${doiTuongId}`, fd)
      onDoi()
    } catch (e) {
      setMaLoi(layMaLoi(e))
    } finally {
      setDangTai(false)
      if (oTep.current) oTep.current.value = ''
    }
  }

  const xoa = async (id: string) => {
    try {
      await api.delete(`/tep/${id}`)
      onDoi()
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  // Tải về qua blob chứ không mở thẳng URL: endpoint cần header Authorization, mà thẻ <a>
  // thường không gửi được nó.
  const taiVe = async (tep: TepDto) => {
    try {
      const res = await api.get(`/tep/${tep.id}`, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = tep.tenGoc
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  return (
    <div className="grid gap-2">
      {teps.length > 0 && (
        <ul className="grid gap-1">
          {teps.map((tep) => (
            <li
              key={tep.id}
              className="flex items-center gap-2 rounded-md border border-border px-2 py-1.5 text-sm"
            >
              <Paperclip className="h-4 w-4 shrink-0 text-muted-foreground" />
              <span className="flex-1 truncate" title={tep.tenGoc}>
                {tep.tenGoc}
              </span>
              <span className="shrink-0 text-xs text-muted-foreground">
                {coKB(tep.kichThuoc)}
              </span>
              <Button variant="ghost" size="sm" title={t('chung.taiVe')} onClick={() => taiVe(tep)}>
                <Download className="h-4 w-4" />
              </Button>
              {!chiDoc && (
                <Button variant="ghost" size="sm" title={t('chung.xoa')} onClick={() => xoa(tep.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}

      {!chiDoc && (
        <>
          <input
            ref={oTep}
            type="file"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0]
              if (f) void taiLen(f)
            }}
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={dangTai}
            onClick={() => oTep.current?.click()}
          >
            <Upload className="mr-1.5 h-4 w-4" />
            {dangTai ? t('chung.dangTai') : t('hocLieu.themTep')}
          </Button>
          <p className="text-xs text-muted-foreground">{t('hocLieu.keoThaTep')}</p>
        </>
      )}

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
    </div>
  )
}
