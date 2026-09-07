import { useTranslation } from 'react-i18next'
import { DangPhatTrien } from '@/components/ui/DangPhatTrien'

export default function GiaoVienNhanSu() {
  const { t } = useTranslation()
  return <DangPhatTrien tieuDe={t('menu.giaoVienNhanSu')} moTa={t('hrm.moTaGiaoVien')} />
}
