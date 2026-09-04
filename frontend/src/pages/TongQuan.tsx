import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ChevronRight, Settings, ShieldCheck, Users } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui'
import { useAuth } from '@/lib/auth'

/**
 * Màn Tổng quan — màn ĐẦU TIÊN người dùng thấy sau khi đăng nhập.
 *
 * Ở bản base này nó chỉ dẫn tới các màn quản trị hệ thống, vì chưa có nghiệp vụ nào để tóm
 * lược. Khi thêm nghiệp vụ, thay phần dưới bằng danh sách "việc cần làm" — và giữ nguyên tắc
 * đã rút ra từ dự án trước: **mỗi dòng việc phải bấm được để tới đúng chỗ xử lý**. Một con số
 * không kèm đường đi tiếp chỉ làm người dùng biết có việc mà không biết làm ở đâu.
 *
 * Cũng đừng làm dải 4 số thống kê chỉ để lấp chỗ trống — nếu số đó đã có ở màn chuyên trách
 * thì lặp lại ở đây không thêm thông tin gì.
 */
export default function TongQuan() {
  const { t } = useTranslation()
  const { phien } = useAuth()

  const loiVao = [
    { to: '/quan-tri/tai-khoan', nhan: t('menu.taiKhoan'), icon: Users },
    { to: '/quan-tri/phan-quyen', nhan: t('menu.phanQuyen'), icon: ShieldCheck },
    { to: '/quan-tri/thiet-lap', nhan: t('menu.thietLap'), icon: Settings },
  ]

  return (
    <div className="grid gap-4 max-w-3xl">
      <Card>
        <CardHeader>
          <CardTitle>{phien?.tenTrungTam || phien?.maTrungTam || t('menu.tongQuan')}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <p className="text-sm text-muted-foreground">{t('tongQuan.chuaCoNoiDung')}</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('menu.quanTri')}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <ul className="divide-y divide-border">
            {loiVao.map(({ to, nhan, icon: Icon }) => (
              <li key={to}>
                <Link
                  to={to}
                  className="flex items-center gap-3 py-2.5 text-sm hover:text-[hsl(var(--primary))]"
                >
                  <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                  <span className="flex-1">{nhan}</span>
                  <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                </Link>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  )
}
