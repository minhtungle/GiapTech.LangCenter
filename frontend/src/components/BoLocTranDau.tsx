import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Filter, X } from 'lucide-react'
import { api, type KetQuaTrang } from '@/lib/api'
import { Badge, Button, Input, Label } from '@/components/ui'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

/**
 * Bộ lọc trận đấu — **dùng chung** giữa Lịch thi đấu (FR-07) và Thống kê (FR-12).
 *
 * Đặc tả FR-12 ghi rõ "cùng component, cùng tập tham số. Không dựng hai bộ lọc song song" —
 * hai bản sao sẽ trôi lệch nhau ngay lần thêm tiêu chí đầu tiên.
 */

export type KetQuaTranDau = 'Thang' | 'Hoa' | 'Thua'

export interface GiaTriBoLoc {
  tuNgay: string
  denNgay: string
  ketQua: KetQuaTranDau[]
  doiThuId: string | null
}

export const BO_LOC_RONG: GiaTriBoLoc = {
  tuNgay: '',
  denNgay: '',
  ketQua: [],
  doiThuId: null,
}

export const coLocNao = (loc: GiaTriBoLoc) =>
  Boolean(loc.tuNgay || loc.denNgay || loc.ketQua.length || loc.doiThuId)

/** Chuyển sang body API — bỏ trống thành null vì backend hiểu null là "không lọc". */
export const sangThamSoApi = (loc: GiaTriBoLoc) => ({
  tuNgay: loc.tuNgay || null,
  denNgay: loc.denNgay || null,
  ketQua: loc.ketQua.length ? loc.ketQua : null,
  doiThuId: loc.doiThuId,
})

interface DoiThuNgan {
  id: string
  tenDoi: string
}

/** Nút mở/đóng bộ lọc, kèm huy hiệu báo đang lọc. */
export function NutBoLoc({
  mo,
  onDoi,
  loc,
}: {
  mo: boolean
  onDoi: (mo: boolean) => void
  loc: GiaTriBoLoc
}) {
  const { t } = useTranslation()
  return (
    <Button variant={mo ? 'primary' : 'outline'} onClick={() => onDoi(!mo)}>
      <Filter className="h-4 w-4" />
      {t('tranDau.boLoc')}
      {coLocNao(loc) && (
        <Badge variant="accent" className="ml-1">
          {t('tranDau.dangLoc')}
        </Badge>
      )}
    </Button>
  )
}

export function BoLocTranDau({
  loc,
  onDoi,
}: {
  loc: GiaTriBoLoc
  onDoi: (loc: GiaTriBoLoc) => void
}) {
  const { t } = useTranslation()

  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DoiThuNgan>>('/doi-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  return (
    <div className="grid gap-3 rounded-lg border border-border bg-card p-4 sm:grid-cols-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="tuNgay">{t('tranDau.tuNgay')}</Label>
        <Input
          id="tuNgay"
          type="date"
          value={loc.tuNgay}
          onChange={(e) => onDoi({ ...loc, tuNgay: e.target.value })}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="denNgay">{t('tranDau.denNgay')}</Label>
        <Input
          id="denNgay"
          type="date"
          value={loc.denNgay}
          onChange={(e) => onDoi({ ...loc, denNgay: e.target.value })}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="locDoiThu">{t('tranDau.doiThu')}</Label>
        <SelectTimKiem
          id="locDoiThu"
          luaChon={(doiThus ?? []).map((d) => ({ giaTri: d.id, nhan: d.tenDoi }))}
          giaTri={loc.doiThuId}
          onDoi={(v) => onDoi({ ...loc, doiThuId: v })}
          placeholder={t('tranDau.moiDoiThu')}
          placeholderTimKiem={t('tranDau.timDoiThu')}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label>{t('tranDau.ketQua')}</Label>
        <div className="flex flex-wrap gap-2 pt-1.5">
          {(['Thang', 'Hoa', 'Thua'] as const).map((k) => (
            <label key={k} className="flex items-center gap-1.5 text-sm">
              <input
                type="checkbox"
                className="h-4 w-4 accent-[hsl(var(--primary))]"
                checked={loc.ketQua.includes(k)}
                onChange={(e) =>
                  onDoi({
                    ...loc,
                    ketQua: e.target.checked
                      ? [...loc.ketQua, k]
                      : loc.ketQua.filter((x) => x !== k),
                  })
                }
              />
              {t(`tranDau.kq.${k}`)}
            </label>
          ))}
        </div>
      </div>

      {coLocNao(loc) && (
        <div className="sm:col-span-4">
          <Button variant="ghost" size="sm" onClick={() => onDoi(BO_LOC_RONG)}>
            <X className="h-3.5 w-3.5" />
            {t('tranDau.xoaLoc')}
          </Button>
        </div>
      )}
    </div>
  )
}
