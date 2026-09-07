import { useTranslation } from 'react-i18next'
import { DangPhatTrien } from '@/components/ui/DangPhatTrien'

export default function NhanVienKinhDoanh() {
  const { t } = useTranslation()
  return <DangPhatTrien tieuDe={t('menu.nhanVienKinhDoanh')} moTa={t('hrm.moTaNvkd')} />
}
