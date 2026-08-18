import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Check, X, Trash2, MapPin, Phone } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Card, CardContent, Textarea, TrangTrong } from '@/components/ui'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Lời mời bắt đối — hiện trong Hòm thư, cả lời mời ta gửi lẫn ta nhận.
 *
 * Gộp hai chiều vào một danh sách thay vì hai tab: đội phong trào có vài lời mời mỗi tháng,
 * chia tab chỉ bắt người dùng bấm thêm để tìm.
 */

interface ThuBatDoi {
  id: string
  toiGui: boolean
  maDoiBenKia: string
  tenDoiBenKia: string
  logoBenKia: string | null
  khuVucBenKia: string | null
  lienHeBenKia: string | null
  thoiGianDeXuat: string | null
  diaDiem: string | null
  loiNhan: string | null
  trangThai: 'ChoPhanHoi' | 'DaChapNhan' | 'DaTuChoi'
  phanHoi: string | null
  thoiGianPhanHoi: string | null
  ngayTao: string
  tranDauCuaToi: string | null
}

export function LoiMoiBatDoi() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [dangTraLoi, setDangTraLoi] = useState<{ thu: ThuBatDoi; chapNhan: boolean } | null>(null)
  const [xacNhanHuy, setXacNhanHuy] = useState<ThuBatDoi | null>(null)

  const { data: ds, isLoading } = useQuery({
    queryKey: ['loi-moi-bat-doi'],
    queryFn: async () => (await api.get<ThuBatDoi[]>('/san-doi-thu/loi-moi')).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['loi-moi-bat-doi'] })
    // Chấp nhận sẽ TẠO trận trong lịch ta — làm mới cả hai, không thì lịch trông như chưa có gì.
    void qc.invalidateQueries({ queryKey: ['tran-dau'] })
    void qc.invalidateQueries({ queryKey: ['san-doi-thu'] })
  }

  const traLoi = useMutation({
    mutationFn: async (v: { id: string; chapNhan: boolean; phanHoi: string | null }) =>
      api.post(`/san-doi-thu/loi-moi/${v.id}/tra-loi`, {
        chapNhan: v.chapNhan,
        phanHoi: v.phanHoi,
      }),
    onSuccess: () => {
      lamMoi()
      setDangTraLoi(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const huy = useMutation({
    mutationFn: async (id: string) => api.delete(`/san-doi-thu/loi-moi/${id}`),
    onSuccess: () => {
      lamMoi()
      setXacNhanHuy(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const gioDep = (iso: string | null) =>
    iso
      ? new Date(iso).toLocaleString(i18n.language, {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        })
      : t('san.chuaHenGio')

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (!ds || ds.length === 0) return <TrangTrong thongDiep={t('san.chuaCoLoiMoi')} />

  return (
    <div className="flex flex-col gap-3">
      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {ds.map((thu) => (
        <Card key={thu.id}>
          <CardContent className="flex flex-col gap-2.5 pt-5">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={thu.toiGui ? 'muted' : 'accent'}>
                {thu.toiGui ? t('san.toiGui') : t('san.toiNhan')}
              </Badge>
              <span className="font-medium">{thu.tenDoiBenKia}</span>
              <span className="font-mono text-xs text-muted-foreground">{thu.maDoiBenKia}</span>

              <span className="ml-auto">
                {thu.trangThai === 'ChoPhanHoi' && (
                  <Badge variant="draw">{t('san.choPhanHoi')}</Badge>
                )}
                {thu.trangThai === 'DaChapNhan' && (
                  <Badge variant="win">{t('san.daChapNhan')}</Badge>
                )}
                {thu.trangThai === 'DaTuChoi' && (
                  <Badge variant="lose">{t('san.daTuChoi')}</Badge>
                )}
              </span>
            </div>

            <p className="text-sm">{gioDep(thu.thoiGianDeXuat)}</p>

            {thu.diaDiem && (
              <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
                <MapPin className="h-3.5 w-3.5 shrink-0" />
                {thu.diaDiem}
              </p>
            )}

            {thu.loiNhan && (
              <p className="rounded-md bg-muted/50 p-2 text-sm" style={{ overflowWrap: 'anywhere' }}>
                {thu.loiNhan}
              </p>
            )}

            {thu.phanHoi && (
              <p className="text-sm text-muted-foreground" style={{ overflowWrap: 'anywhere' }}>
                <span className="font-medium">{t('san.phanHoi')}: </span>
                {thu.phanHoi}
              </p>
            )}

            {/* Liên hệ chỉ có sau khi hai bên đồng ý — backend không trả trước đó. */}
            {thu.lienHeBenKia && (
              <p className="flex items-center gap-1.5 text-sm">
                <Phone className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                {thu.lienHeBenKia}
              </p>
            )}

            {thu.tranDauCuaToi && (
              <Link
                to={`/lich-thi-dau/${thu.tranDauCuaToi}`}
                className="text-sm text-primary hover:underline"
              >
                {t('san.xemTranDaTao')}
              </Link>
            )}

            {thu.trangThai === 'ChoPhanHoi' && (
              <div className="flex flex-wrap gap-2 pt-1">
                {thu.toiGui ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => setXacNhanHuy(thu)}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    {t('san.huyLoiMoi')}
                  </Button>
                ) : (
                  <>
                    <Button
                      type="button"
                      size="sm"
                      onClick={() => setDangTraLoi({ thu, chapNhan: true })}
                    >
                      <Check className="h-3.5 w-3.5" />
                      {t('san.dongY')}
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => setDangTraLoi({ thu, chapNhan: false })}
                    >
                      <X className="h-3.5 w-3.5" />
                      {t('san.tuChoi')}
                    </Button>
                  </>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      ))}

      {dangTraLoi && (
        <HopTraLoi
          chapNhan={dangTraLoi.chapNhan}
          tenDoi={dangTraLoi.thu.tenDoiBenKia}
          dangGui={traLoi.isPending}
          onDong={() => setDangTraLoi(null)}
          onGui={(phanHoi) =>
            traLoi.mutate({ id: dangTraLoi.thu.id, chapNhan: dangTraLoi.chapNhan, phanHoi })
          }
        />
      )}

      {xacNhanHuy && (
        <HopXacNhan
          mo
          tieuDe={t('san.huyLoiMoi')}
          thongDiep={t('san.huyXacNhan', { ten: xacNhanHuy.tenDoiBenKia })}
          nhanDongY={t('san.huyLoiMoi')}
          onHuy={() => setXacNhanHuy(null)}
          onDongY={() => huy.mutate(xacNhanHuy.id)}
        />
      )}
    </div>
  )
}

/**
 * Hộp trả lời — có ô lời nhắn kèm.
 *
 * Đồng ý sẽ TẠO trận ở lịch cả hai bên, nên phải nói trước; người dùng bấm "Đồng ý" mà thấy
 * trận tự mọc ra trong lịch sẽ tưởng hệ thống làm gì sai.
 */
function HopTraLoi({
  chapNhan,
  tenDoi,
  dangGui,
  onDong,
  onGui,
}: {
  chapNhan: boolean
  tenDoi: string
  dangGui: boolean
  onDong: () => void
  onGui: (phanHoi: string | null) => void
}) {
  const { t } = useTranslation()
  const [phanHoi, setPhanHoi] = useState('')

  // Dùng Modal thay vì HopXacNhan: hộp xác nhận không nhận children nên không chèn được ô lời
  // nhắn, mà lời nhắn là phần quan trọng — "hôm đó bận, tuần sau được không" giữ cho hai bên
  // còn nói tiếp được.
  return (
    <Modal
      mo
      onDong={onDong}
      tieuDe={chapNhan ? t('san.dongY') : t('san.tuChoi')}
      rong="sm"
    >
      <form
        onSubmit={(e) => {
          e.preventDefault()
          onGui(phanHoi.trim() || null)
        }}
        className="flex flex-col gap-3"
      >
        <p className="text-sm text-muted-foreground">
          {chapNhan
            ? t('san.dongYGiaiThich', { ten: tenDoi })
            : t('san.tuChoiGiaiThich', { ten: tenDoi })}
        </p>

        <Textarea
          rows={2}
          value={phanHoi}
          onChange={(e) => setPhanHoi(e.target.value)}
          placeholder={t('san.phanHoiGoiY')}
        />

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={dangGui}>
            {dangGui ? t('chung.dangTai') : chapNhan ? t('san.dongY') : t('san.tuChoi')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}
