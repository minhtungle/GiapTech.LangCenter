import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { hotkeysCoreFeature, syncDataLoaderFeature } from '@headless-tree/core'
import { useTree } from '@headless-tree/react'
import { ChevronDown, ChevronRight, Pencil, Plus, Trash2, UserPlus, Users } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Textarea, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  CAC_TAG_VAI_TRO, type PhongBanNode, type TagVaiTroPhongBan,
} from '@/pages/quan-tri/NguoiDung'

/** Id gốc ảo — thư viện cần một node gốc, còn API trả thẳng mảng cấp 1. */
const GOC = '__goc__'

interface NguoiNgan {
  id: string
  hoTen: string
  loaiNguoiDung: string
  phongBanId: string | null
  tenPhongBan: string | null
}

/**
 * FR-22 — cơ cấu tổ chức dạng cây.
 *
 * Dùng `@headless-tree` (MIT, ~13.5 KB gzip, 0 dependency runtime) thay vì tự dựng đệ quy: nó
 * lo sẵn điều hướng bàn phím và ARIA `tree`/`treeitem` — hai thứ bản tự viết hay thiếu, mà
 * người dùng bàn phím thì không thao tác được. Thư viện **headless** nên mọi JSX là của mình,
 * dùng lại được `MenuThaoTac`, `Badge`, token Tailwind của dự án.
 *
 * Không dùng thư viện sơ đồ (`@xyflow/react`): yêu cầu ở đây — thu/mở, menu trên node, 2-4 cấp —
 * là treeview, không phải canvas. Sơ đồ tốn ~58 KB gzip cộng một engine layout, và phải tự giải
 * bài đo kích thước node.
 */
export default function CoCauToChuc() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const duocSua = coQuyen('PhongBan', 'Sua')
  const duocThem = coQuyen('PhongBan', 'Them')
  const duocXoa = coQuyen('PhongBan', 'Xoa')

  const [maLoi, setMaLoi] = useState<string | null>(null)
  /** Phòng đang sửa (null = thêm mới), cùng với cha của nó khi thêm. */
  const [dangSua, setDangSua] = useState<PhongBanNode | null>(null)
  const [chaCuaMoi, setChaCuaMoi] = useState<string | null>(null)
  const [moForm, setMoForm] = useState(false)
  const [quanLy, setQuanLy] = useState<string | null>(null)
  const [tag, setTag] = useState<TagVaiTroPhongBan | null>(null)
  /** Phòng đang mở hộp thoại "thêm nhân sự" (cách 2 của FR-22). */
  const [xepVao, setXepVao] = useState<PhongBanNode | null>(null)
  const [nhanSuChon, setNhanSuChon] = useState<string[]>([])

  const { data: cay = [], isLoading } = useQuery({
    queryKey: ['phong-ban'],
    queryFn: async () => (await api.get<PhongBanNode[]>('/phong-ban')).data,
  })

  /**
   * Danh sách nhân sự để chọn người quản lý và xếp vào phòng.
   *
   * Gọi `/nhan-su` (HRM) chứ không `/nguoi-dung`: endpoint đó gác bằng `TaiKhoan` — quyền dùng
   * chung — nên người quản trị cơ cấu sẽ phải được cấp thêm quyền quản trị tài khoản.
   */
  const { data: nhanSus = [] } = useQuery({
    queryKey: ['nhan-su', 'chon-cho-phong-ban'],
    queryFn: async () =>
      (await api.get<{ duLieu: NguoiNgan[] }>('/nhan-su', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: moForm || !!xepVao,
  })

  /** Tra cứu phẳng theo id — thư viện chỉ đưa lại id, dữ liệu tự tìm. */
  const theoId = useMemo(() => {
    const m = new Map<string, PhongBanNode>()
    const di = (ns: PhongBanNode[]) => ns.forEach((n) => { m.set(n.id, n); di(n.phongBanCons) })
    di(cay)
    return m
  }, [cay])

  const tree = useTree<PhongBanNode | null>({
    rootItemId: GOC,
    getItemName: (item) => item.getItemData()?.ten ?? '',
    isItemFolder: (item) => (item.getItemData()?.phongBanCons.length ?? 0) > 0,
    dataLoader: {
      getItem: (id) => (id === GOC ? null : theoId.get(id) ?? null),
      getChildren: (id) =>
        id === GOC
          ? cay.map((n) => n.id)
          : (theoId.get(id)?.phongBanCons ?? []).map((n) => n.id),
    },
    features: [syncDataLoaderFeature, hotkeysCoreFeature],
  })

  /**
   * Dựng lại cây khi dữ liệu từ API đổi, và mở sẵn cấp gốc.
   *
   * Cần bước này vì `useTree` gọi `createTree(config)` **đúng một lần** rồi mỗi render chỉ
   * `setConfig` trộn config mới vào: `dataLoader` có cập nhật nhưng **cấu trúc cây được cache**.
   * Lần render đầu `cay` còn rỗng (query chưa xong) và `rebuildTree()` trong effect mount của
   * thư viện chạy đúng lúc đó — không có effect này thì cây rỗng mãi: menu đúng, route đúng,
   * API trả 5 phòng, mà màn hình trắng (gặp thật 09/09/2026).
   *
   * `rebuildTree()` **rồi mới** `expand()`: đo bằng script độc lập cho thấy `getItems()` trả
   * rỗng cho tới lần rebuild đầu, nên phải có item trước khi gọi được `expand()` trên nó. Và
   * mở nhánh phải qua `item.expand()`, không phải `setState({expandedItems})` — bản kia đổi
   * state mà `getItems()` vẫn không trả con.
   */
  useEffect(() => {
    if (cay.length === 0) return
    tree.rebuildTree()

    // Mở SẴN CẢ CÂY: cơ cấu tổ chức là thứ người ta vào đây để NHÌN, đóng hết thì phải bấm
    // từng nhánh mới thấy. Người dùng tự thu lại nhánh nào không cần.
    //
    // Phải lặp: mở một cấp thì cấp dưới mới xuất hiện trong `getItems()`, nên không mở được
    // hết trong một lượt. Chặn số lượt theo số phòng — dữ liệu có chu trình (do sửa tay DB) sẽ
    // làm vòng lặp không dừng.
    for (let luot = 0; luot < theoId.size + 1; luot++) {
      let coMo = false
      for (const item of tree.getItems()) {
        if (item.isFolder() && !item.isExpanded()) { item.expand(); coMo = true }
      }
      if (!coMo) break
    }
    // `tree` là instance ổn định của thư viện; đưa vào deps sẽ lặp vô hạn.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cay])

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['phong-ban'] })
    void qc.invalidateQueries({ queryKey: ['nhan-su'] })
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        ten: String(fd.get('ten')).trim(),
        phongBanChaId: dangSua ? dangSua.phongBanChaId : chaCuaMoi,
        nguoiQuanLyId: quanLy,
        moTa: String(fd.get('moTa') ?? '').trim() || null,
        thuTu: Number(fd.get('thuTu') ?? 0),
        // Form LUÔN gửi tag (null = người dùng chủ động bỏ tag, không phải "không gửi").
        tagVaiTro: tag,
      }
      if (dangSua) await api.put(`/phong-ban/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/phong-ban', than)
    },
    onSuccess: () => {
      lamMoi()
      setMoForm(false)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/phong-ban/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xepNhanSu = useMutation({
    mutationFn: () =>
      api.post('/phong-ban/xep-nhan-su', {
        nguoiDungIds: nhanSuChon,
        phongBanId: xepVao!.id,
      }),
    onSuccess: () => {
      lamMoi()
      setXepVao(null)
      setNhanSuChon([])
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const moThem = (cha: string | null) => {
    setDangSua(null)
    setChaCuaMoi(cha)
    setQuanLy(null)
    setTag(null)
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (n: PhongBanNode) => {
    setDangSua(n)
    setChaCuaMoi(null)
    setQuanLy(n.nguoiQuanLyId)
    setTag(n.tagVaiTro)
    setMaLoi(null)
    setMoForm(true)
  }

  // Chỉ nhân sự chưa thuộc phòng nào, hoặc thuộc phòng khác — người đã ở đúng phòng này thì
  // chọn lại là vô nghĩa.
  const nhanSuCoTheXep = nhanSus
    .filter((u) => u.phongBanId !== xepVao?.id)
    .map((u) => ({
      giaTri: u.id,
      nhan: u.hoTen,
      phu: u.tenPhongBan ?? t('coCau.chuaXep'),
    }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{t('menu.coCauToChuc')}</h2>
        {duocThem && (
          <Button className="ml-auto" onClick={() => moThem(null)}>
            <Plus className="h-4 w-4" />
            {t('coCau.themPhongGoc')}
          </Button>
        )}
      </div>

      {maLoi && !moForm && !xepVao && (
        <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
      )}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : cay.length === 0 ? (
            <TrangTrong thongDiep={t('coCau.chuaCoPhongBan')} />
          ) : (
            <div {...tree.getContainerProps(t('menu.coCauToChuc'))} className="grid gap-1">
              {tree.getItems().map((item) => {
                const n = item.getItemData()
                if (!n) return null
                const meta = item.getItemMeta()
                const coCon = n.phongBanCons.length > 0
                const mo = item.isExpanded()

                return (
                  <div
                    key={item.getId()}
                    {...item.getProps()}
                    ref={item.registerElement}
                    // Thụt lề theo ĐỘ SÂU thay vì lồng DOM: thư viện trả danh sách phẳng, và
                    // phẳng thì điều hướng bàn phím đi đúng thứ tự nhìn thấy.
                    //
                    // `level` bắt đầu từ 0 ở hàng trên cùng (đã đo) — trừ 1 sẽ cho padding âm.
                    style={{ paddingLeft: `${0.625 + meta.level * 1.5}rem` }}
                    className="flex flex-wrap items-center gap-2 rounded-md border border-border p-2.5 outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    {coCon ? (
                      mo ? (
                        <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
                      ) : (
                        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                      )
                    ) : (
                      // Chỗ trống cùng bề rộng: thiếu nó thì tên phòng lá lệch so với phòng cha.
                      <span className="w-4 shrink-0" />
                    )}

                    <span className="font-medium">{n.ten}</span>

                    {/* Sĩ số: hiện "riêng / cả nhánh" khi hai số khác nhau — một số thôi thì
                        người xem không biết nó gồm cấp dưới hay không. */}
                    <Badge variant="muted">
                      <Users className="mr-1 h-3 w-3" />
                      {n.soNhanSu === n.soNhanSuCaNhanh
                        ? n.soNhanSu
                        : `${n.soNhanSu} / ${n.soNhanSuCaNhanh}`}
                    </Badge>

                    {/*
                      Tag hiện NGAY trên cây: đây là thứ quyết định phòng có xuất hiện ở module
                      khác hay không, nên phải thấy được mà không cần mở form từng phòng.
                      Phòng không tag KHÔNG hiện badge nào — im lặng là trạng thái mặc định,
                      thêm badge "không tag" cho 8 phòng chỉ làm cây rối.
                    */}
                    {n.tagVaiTro && (
                      <Badge variant={n.tagVaiTro === 'KinhDoanh' ? 'ok' : 'muted'}>
                        {t(`tagVaiTro.${n.tagVaiTro}`)}
                      </Badge>
                    )}

                    {n.tenNguoiQuanLy && (
                      <Badge variant="accent">
                        {t('coCau.quanLy')}: {n.tenNguoiQuanLy}
                      </Badge>
                    )}

                    {n.moTa && (
                      <span className="text-xs text-muted-foreground">{n.moTa}</span>
                    )}

                    <div className="ml-auto">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('coCau.themPhongCon'),
                            icon: Plus,
                            an: !duocThem,
                            onChon: () => moThem(n.id),
                          },
                          {
                            nhan: t('coCau.themNhanSu'),
                            icon: UserPlus,
                            an: !duocSua,
                            onChon: () => {
                              setNhanSuChon([])
                              setMaLoi(null)
                              setXepVao(n)
                            },
                          },
                          {
                            nhan: t('chung.sua'),
                            icon: Pencil,
                            an: !duocSua,
                            onChon: () => moSua(n),
                          },
                          {
                            nhan: t('chung.xoa'),
                            icon: Trash2,
                            nguyHiem: true,
                            ngatNhom: true,
                            an: !duocXoa,
                            onChon: () =>
                              hoi({
                                tieuDe: t('chung.xacNhanXoa'),
                                thongDiep: t('coCau.hoiXoa', { ten: n.ten }),
                                nhanDongY: t('chung.xoa'),
                                nguyHiem: true,
                                onDongY: () => xoa.mutate(n.id),
                              }),
                          },
                        ]}
                      />
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </CardContent>
      </Card>

      {/* ---------- Form thêm/sửa phòng ban ---------- */}
      <Modal
        mo={moForm}
        onDong={() => setMoForm(false)}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('coCau.suaPhongBan') : t('coCau.themPhongBan')}
        moTa={
          dangSua
            ? dangSua.ten
            : chaCuaMoi
              ? t('coCau.duoiPhong', { ten: theoId.get(chaCuaMoi)?.ten ?? '' })
              : t('coCau.phongGoc')
        }
        rong="sm"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('coCau.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="ten">{t('coCau.tenPhongBan')} *</Label>
            <Input id="ten" name="ten" required defaultValue={dangSua?.ten ?? ''} />
          </div>

          <div>
            <Label htmlFor="quanLy">{t('coCau.nguoiQuanLy')}</Label>
            <SelectTimKiem
              id="quanLy"
              luaChon={nhanSus.map((u) => ({ giaTri: u.id, nhan: u.hoTen }))}
              giaTri={quanLy}
              onDoi={setQuanLy}
              placeholder={t('coCau.chuaChonQuanLy')}
              placeholderTimKiem={t('coCau.nguoiQuanLy')}
            />
            {/* Nói rõ ngay trên form: cột này KHÔNG cấp quyền gì (quy tắc #9). */}
            <p className="mt-1 text-xs text-muted-foreground">{t('coCau.quanLyChiLaThongTin')}</p>
          </div>

          {/*
            Ô TAG đặt ngay sau người quản lý, TRƯỚC thứ tự và mô tả: nó quyết định phòng này có
            xuất hiện ở module khác hay không — quan trọng hơn hai trường trình bày bên dưới.
          */}
          <div>
            <Label htmlFor="tagVaiTro">{t('coCau.tagVaiTro')}</Label>
            <SelectTimKiem
              id="tagVaiTro"
              luaChon={CAC_TAG_VAI_TRO.map((x) => ({
                giaTri: x,
                nhan: t(`tagVaiTro.${x}`),
              }))}
              giaTri={tag}
              onDoi={(v) => setTag(v as TagVaiTroPhongBan | null)}
              placeholder={t('coCau.khongTag')}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              {tag ? t(`tagVaiTro.goiY${tag}`) : t('coCau.khongTagGoiY')}
            </p>
          </div>

          <div>
            <Label htmlFor="thuTu">{t('coCau.thuTu')}</Label>
            <Input
              id="thuTu"
              name="thuTu"
              type="number"
              min={0}
              defaultValue={dangSua?.thuTu ?? 0}
            />
          </div>

          <div>
            <Label htmlFor="moTa">{t('coCau.moTa')}</Label>
            <Textarea id="moTa" name="moTa" rows={2} defaultValue={dangSua?.moTa ?? ''} />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setMoForm(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {/* ---------- CÁCH 2: xếp nhân sự đã có vào phòng ---------- */}
      <Modal
        mo={!!xepVao}
        onDong={() => setXepVao(null)}
        chanDoiKhiXuLy={xepNhanSu.isPending}
        tieuDe={t('coCau.themNhanSu')}
        moTa={xepVao?.ten}
        rong="sm"
      >
        {xepVao && (
          <div className="grid gap-3">
            <div>
              <Label htmlFor="nhanSu">{t('coCau.chonNhanSu')}</Label>
              <SelectTimKiemNhieu
                id="nhanSu"
                luaChon={nhanSuCoTheXep}
                giaTri={nhanSuChon}
                onDoi={setNhanSuChon}
                placeholder={t('coCau.goTenNhanSu')}
                placeholderTimKiem={t('coCau.goTenNhanSu')}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                {t('coCau.hocVienKhongVaoCoCau')}
              </p>
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => setXepVao(null)}>
                {t('chung.huy')}
              </Button>
              <Button
                disabled={nhanSuChon.length === 0 || xepNhanSu.isPending}
                onClick={() =>
                  hoi({
                    tieuDe: t('coCau.themNhanSu'),
                    thongDiep: t('coCau.hoiXepNhanSu', {
                      soLuong: nhanSuChon.length,
                      ten: xepVao.ten,
                    }),
                    onDongY: () => xepNhanSu.mutate(),
                  })
                }
              >
                {t('chung.them')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {hop}
    </div>
  )
}
