import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ExternalLink, Plus, Save, Trash2, Video } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi, Input, Label, Textarea, TrangTrong } from '@/components/ui'
import { HopXacNhan } from '@/components/ui/HopXacNhan'

/**
 * Quản lý nhiều link video của một trận — **tab riêng** ở màn chi tiết trận (FR-10).
 *
 * Hệ thống **chỉ lưu link**, không lưu file (ADR-0004): một trận quay 90 phút là vài GB, tự
 * host sẽ đốt hết dung lượng VPS trong một mùa.
 *
 * Một trận thường có video hiệp 1, hiệp 2, bản highlight và vài clip bàn thắng ở các nguồn
 * khác nhau — nên mỗi link có **tên riêng**, không chỉ là URL trần.
 */

export interface VideoDto {
  id: string
  ten: string
  url: string
  moTa: string | null
  thuTu: number
}

/** Dòng đang soạn: `id` null nghĩa là chưa lưu lần nào. */
interface DongVideo {
  id: string | null
  ten: string
  url: string
  moTa: string
}

export function QuanLyVideoTran({
  tranDauId,
  onLoi,
}: {
  tranDauId: string
  onLoi: (m: string | null) => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [nhap, setNhap] = useState<DongVideo[] | null>(null)
  const [daLuu, setDaLuu] = useState(false)
  const [xoaDong, setXoaDong] = useState<number | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['video', tranDauId],
    queryFn: async () => (await api.get<VideoDto[]>(`/tran-dau/${tranDauId}/video`)).data,
  })

  const luu = useMutation({
    mutationFn: async (ds: DongVideo[]) =>
      api.put(`/tran-dau/${tranDauId}/video`, {
        tranDauId,
        videos: ds.map((v) => ({ id: v.id, ten: v.ten, url: v.url, moTa: v.moTa || null })),
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['video', tranDauId] })
      // Thư viện video đọc thẳng từ đây nên phải nạp lại theo.
      void qc.invalidateQueries({ queryKey: ['thu-vien-video'] })
      onLoi(null)
      setNhap(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const ds: DongVideo[] =
    nhap ?? (data ?? []).map((v) => ({ id: v.id, ten: v.ten, url: v.url, moTa: v.moTa ?? '' }))

  const sua = (i: number, thay: Partial<DongVideo>) =>
    setNhap(ds.map((v, j) => (i === j ? { ...v, ...thay } : v)))

  const them = () =>
    setNhap([...ds, { id: null, ten: '', url: '', moTa: '' }])

  const xoa = (i: number) => setNhap(ds.filter((_, j) => j !== i))

  /** Chỉ mở link http/https — cùng lý do với validator ở backend: chặn `javascript:`. */
  const moDuoc = (url: string) => /^https?:\/\//i.test(url.trim())

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <p className="flex items-center gap-2 text-sm font-medium">
          <Video className="h-4 w-4" />
          {t('video.tieuDe')}
          <span className="text-muted-foreground">({ds.length})</span>
        </p>
        <Button variant="outline" size="sm" onClick={them}>
          <Plus className="h-4 w-4" />
          {t('video.themLink')}
        </Button>
      </div>

      {ds.length === 0 ? (
        <TrangTrong thongDiep={t('video.chuaCo')} />
      ) : (
        <div className="flex flex-col gap-2">
          {ds.map((v, i) => (
            <div key={v.id ?? `moi-${i}`} className="rounded-lg border border-border p-3">
              <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto]">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor={`vten_${i}`}>{t('video.ten')}</Label>
                  <Input
                    id={`vten_${i}`}
                    value={v.ten}
                    onChange={(e) => sua(i, { ten: e.target.value })}
                    placeholder={t('video.tenGoiY')}
                  />
                </div>

                <div className="flex flex-col gap-1.5">
                  <Label htmlFor={`vurl_${i}`}>{t('video.url')}</Label>
                  <div className="flex gap-1.5">
                    <Input
                      id={`vurl_${i}`}
                      type="url"
                      value={v.url}
                      onChange={(e) => sua(i, { url: e.target.value })}
                      placeholder="https://youtube.com/..."
                    />
                    {/* rel="noreferrer" bắt buộc: link do người dùng nhập, không để trang
                        đích đọc được document.opener của mình. */}
                    {moDuoc(v.url) && (
                      <a
                        href={v.url}
                        target="_blank"
                        rel="noreferrer"
                        title={t('video.mo')}
                        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-border hover:bg-muted"
                      >
                        <ExternalLink className="h-4 w-4" />
                      </a>
                    )}
                  </div>
                </div>

                <div className="flex items-end">
                  <Button
                    variant="ghost"
                    size="sm"
                    title={t('chung.xoa')}
                    onClick={() => (v.id ? setXoaDong(i) : xoa(i))}
                  >
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>

              <div className="mt-3 flex flex-col gap-1.5">
                <Label htmlFor={`vmota_${i}`}>{t('video.moTa')}</Label>
                <Textarea
                  id={`vmota_${i}`}
                  rows={2}
                  value={v.moTa}
                  onChange={(e) => sua(i, { moTa: e.target.value })}
                  placeholder={t('video.moTaGoiY')}
                />
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-center gap-3">
        <Button
          onClick={() => luu.mutate(ds)}
          disabled={luu.isPending || ds.some((v) => !v.ten.trim() || !v.url.trim())}
        >
          <Save className="h-4 w-4" />
          {luu.isPending ? t('chung.dangTai') : t('video.luu')}
        </Button>
        {daLuu && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
        {ds.some((v) => !v.ten.trim() || !v.url.trim()) && (
          <CanhBaoLoi>{t('video.thieuTenHoacUrl')}</CanhBaoLoi>
        )}
      </div>

      {/* Chỉ hỏi khi xoá dòng ĐÃ LƯU — dòng vừa thêm chưa có gì để mất. */}
      <HopXacNhan
        mo={xoaDong !== null}
        tieuDe={t('video.xacNhanXoa')}
        thongDiep={t('video.xacNhanXoaMoTa', {
          ten: xoaDong !== null ? ds[xoaDong]?.ten || t('video.khongTen') : '',
        })}
        onDongY={() => {
          if (xoaDong !== null) xoa(xoaDong)
          setXoaDong(null)
        }}
        onHuy={() => setXoaDong(null)}
      />
    </div>
  )
}
