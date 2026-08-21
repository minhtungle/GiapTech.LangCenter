import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import {
  CalendarClock, ChevronRight, CircleAlert, ClipboardList, Coins, Mail, Swords, Users,
} from 'lucide-react'
import { api } from '@/lib/api'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@/components/ui'
import { cn } from '@/lib/utils'

/**
 * Màn Tổng quan — việc cần làm + trận sắp tới (nợ N6, làm 21/08).
 *
 * Trước đó chỉ có "Xin chào, admin" — 12 dòng JSX, không thuộc FR nào nên bị bỏ sót. Đây là màn
 * ĐẦU TIÊN người dùng thấy sau khi đăng nhập.
 *
 * Nguyên tắc: mỗi dòng việc phải **bấm được để tới đúng chỗ xử lý**. Một con số không kèm đường
 * đi tiếp chỉ làm người dùng biết có việc mà không biết làm ở đâu.
 *
 * Không làm dải 4 số thống kê (tỷ lệ thắng, số dư quỹ…) — thứ đó đã có ở màn Thống kê và Tài
 * chính, lặp lại chỉ để lấp chỗ trống.
 */

interface ViecCanLam {
  ma: string
  soLuong: number
  duongDan: string | null
}

interface TranNgan {
  id: string
  thoiGian: string
  tenDoiThu: string | null
  daNhan: number | null
  tongDuocMoi: number | null
}

interface TongQuanDto {
  tenDoi: string
  laTruongNhom: boolean
  viecCanLams: ViecCanLam[]
  tranKeTiep: TranNgan | null
  tranVuaRoi: TranNgan | null
}

/** Icon theo loại việc — mắt nhận ra loại trước khi đọc chữ. */
const ICON: Record<string, typeof Mail> = {
  LOI_MOI_THACH_DAU: Swords,
  TRAN_CHUA_MOI_DANG_KY: ClipboardList,
  CON_NO_QUY: Coins,
  TOI_CHUA_TRA_LOI: Mail,
  TOI_CON_NO_QUY: Coins,
}

/** Việc của CHÍNH MÌNH quan trọng hơn việc của đội — người dùng xử lý được ngay. */
const VIEC_CUA_TOI = new Set(['TOI_CHUA_TRA_LOI', 'TOI_CON_NO_QUY'])

export default function TongQuan() {
  const { t } = useTranslation()

  const { data, isLoading } = useQuery({
    queryKey: ['tong-quan'],
    queryFn: async () => (await api.get<TongQuanDto>('/tong-quan')).data,
  })

  if (isLoading)
    return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  if (!data) return null

  // Việc của mình lên trước — đó là thứ người dùng bấm được ngay.
  const viec = [...data.viecCanLams].sort(
    (a, b) => Number(VIEC_CUA_TOI.has(b.ma)) - Number(VIEC_CUA_TOI.has(a.ma)),
  )

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold">{data.tenDoi}</h1>
        <p className="text-sm text-muted-foreground">
          {data.laTruongNhom ? t('tongQuan.moTaTruongNhom') : t('tongQuan.moTaCauThu')}
        </p>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* ----- Việc cần làm ----- */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CircleAlert className="h-4 w-4 text-[hsl(var(--status-draw))]" />
              {t('tongQuan.viecCanLam')}
              {viec.length > 0 && <Badge variant="draw">{viec.length}</Badge>}
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            {viec.length === 0 ? (
              /* Không việc gì là tin TỐT — nói rõ thay vì để trống, người dùng khỏi tưởng lỗi. */
              <p className="py-2 text-sm text-muted-foreground">{t('tongQuan.khongCoViec')}</p>
            ) : (
              <ul className="flex flex-col divide-y divide-border">
                {viec.map((v) => {
                  const Icon = ICON[v.ma] ?? CircleAlert
                  const cuaToi = VIEC_CUA_TOI.has(v.ma)
                  const noiDung = (
                    <>
                      <Icon
                        className={cn(
                          'h-4 w-4 shrink-0',
                          cuaToi ? 'text-primary' : 'text-muted-foreground',
                        )}
                      />
                      <span className="min-w-0 flex-1">
                        {t(`tongQuan.viec.${v.ma}`, { so: v.soLuong })}
                      </span>
                      {v.duongDan && (
                        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                      )}
                    </>
                  )

                  return (
                    <li key={v.ma}>
                      {v.duongDan ? (
                        <Link
                          to={v.duongDan}
                          className="flex items-center gap-2.5 py-2.5 text-sm hover:text-primary"
                        >
                          {noiDung}
                        </Link>
                      ) : (
                        <span className="flex items-center gap-2.5 py-2.5 text-sm">{noiDung}</span>
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </CardContent>
        </Card>

        {/* ----- Trận kế tiếp / vừa rồi ----- */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CalendarClock className="h-4 w-4 text-primary" />
              {t('tongQuan.tranDau')}
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3 pt-0">
            <TheTran tran={data.tranKeTiep} nhan={t('tongQuan.tranKeTiep')} keTiep />
            <TheTran tran={data.tranVuaRoi} nhan={t('tongQuan.tranVuaRoi')} />
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function TheTran({
  tran,
  nhan,
  keTiep = false,
}: {
  tran: TranNgan | null
  nhan: string
  keTiep?: boolean
}) {
  const { t } = useTranslation()

  if (!tran)
    return (
      <div>
        <p className="text-xs text-muted-foreground">{nhan}</p>
        <p className="text-sm text-muted-foreground">{t('tongQuan.khongCoTran')}</p>
      </div>
    )

  return (
    <div>
      <p className="text-xs text-muted-foreground">{nhan}</p>
      <Link
        to={`/lich-thi-dau/${tran.id}`}
        className="group flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm hover:text-primary"
      >
        <span className="font-medium">
          {new Date(tran.thoiGian).toLocaleString('vi-VN', {
            weekday: 'short', day: '2-digit', month: '2-digit',
            hour: '2-digit', minute: '2-digit',
          })}
        </span>
        {tran.tenDoiThu && <span className="text-muted-foreground">· {tran.tenDoiThu}</span>}
        <ChevronRight className="h-4 w-4 opacity-0 transition-opacity group-hover:opacity-100" />
      </Link>

      {/* Tiến độ đăng ký chỉ có nghĩa với trận SẮP tới. `null` = chưa gửi lời mời — nói rõ thay
          vì hiện 0/0, hai thứ đó khác nhau. */}
      {keTiep && (
        <p className="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
          <Users className="h-3.5 w-3.5 shrink-0" />
          {tran.daNhan === null
            ? t('tongQuan.chuaGuiLoiMoi')
            : t('tongQuan.tienDoNhan', { da: tran.daNhan, tong: tran.tongDuocMoi })}
        </p>
      )}
    </div>
  )
}
