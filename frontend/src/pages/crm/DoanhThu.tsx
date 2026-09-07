import { useTranslation } from 'react-i18next'
import { DangPhatTrien } from '@/components/ui/DangPhatTrien'

export default function DoanhThu() {
  const { t } = useTranslation()
  return <DangPhatTrien tieuDe={t('menu.doanhThu')} moTa={t('crm.moTaDoanhThu')} />
}
