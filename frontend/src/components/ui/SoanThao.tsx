import { useEffect } from 'react'
import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Link from '@tiptap/extension-link'
import Underline from '@tiptap/extension-underline'
import TextAlign from '@tiptap/extension-text-align'
import { TextStyleKit } from '@tiptap/extension-text-style'
import { TableKit } from '@tiptap/extension-table'
import { useTranslation } from 'react-i18next'
import {
  AlignCenter, AlignJustify, AlignLeft, AlignRight, Link2, Redo2,
  Table as TableIcon, Trash2, Undo2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import './soan-thao.css'

/**
 * Ô soạn thảo văn bản có định dạng, dùng **Tiptap** (MIT).
 *
 * ## Vì sao Tiptap chứ không CKEditor
 *
 * CKEditor 5 phát hành kép GPL-2.0-or-later / thương mại. Dự án này không mở mã theo GPL, nên
 * dùng bản miễn phí là sai giấy phép — bản thương mại từ ~160 USD/tháng. Tiptap là MIT, không
 * ràng buộc gì.
 *
 * ## Tiêu chí chọn nút: Outlook có dựng được không
 *
 * Đây là ô soạn **email**, không phải soạn trang web. Outlook trên Windows dựng HTML bằng
 * engine của Word — không flexbox, không grid, không `border-radius`. Mỗi nút thừa ở đây là
 * một cách để người dùng tạo ra email vỡ trên máy người nhận mà họ không bao giờ nhìn thấy.
 *
 * Mở rộng 09/10/2026 theo yêu cầu chủ sản phẩm. Những thứ THÊM đều là thuộc tính HTML cơ bản
 * mà engine của Word dựng đúng: gạch chân, gạch ngang, căn lề, màu chữ/nền, cỡ chữ, bảng.
 *
 * Vẫn cố ý KHÔNG có:
 *
 * - **Ảnh** — ảnh tải lên MinIO đọc qua API *sau đăng nhập*, dán vào email thì người nhận
 *   thấy ô vỡ. Làm đúng phải nhúng CID vào thư; chủ sản phẩm chốt dùng đính kèm tệp thay thế.
 * - **Màu tự do** — bảng màu chọn sẵn, không có bộ chọn tuỳ ý. Chữ xám nhạt trên nền tối của
 *   trình đọc mail là không đọc được, và người soạn không bao giờ thấy điều đó.
 * - **Cỡ chữ tuỳ ý** — ba mức cố định. Người dùng gõ `8px` thì thư không ai đọc nổi.
 *
 * `StarterKit` có sẵn vài extension ta không muốn, nên tắt tường minh ở dưới thay vì để mặc
 * định — người sau đọc thấy ngay cái gì bị bỏ và vì sao.
 */
export function SoanThao({
  giaTri,
  onDoi,
  className,
  doCao = 'min-h-[18rem]',
}: {
  giaTri: string
  onDoi: (html: string) => void
  className?: string
  doCao?: string
}) {
  const { t } = useTranslation()

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        // Outlook không dựng được `<hr>` nhất quán, và trích dẫn thì không hợp văn cảnh email
        // giao dịch. Tắt để không ai bấm nhầm.
        horizontalRule: false,
        blockquote: false,
        codeBlock: false,
        // Chỉ H2/H3: H1 trong thân email đấu với tiêu đề thư, nhìn như hai tiêu đề.
        heading: { levels: [2, 3] },
      }),
      Link.configure({
        openOnClick: false,
        // `target="_blank"` cho liên kết trong email: người đọc đang ở trong trình đọc mail,
        // điều hướng ngay trong đó sẽ mất ngữ cảnh thư.
        HTMLAttributes: { target: '_blank', rel: 'noopener noreferrer' },
      }),
      Underline,
      TextAlign.configure({
        // Chỉ đoạn văn và tiêu đề. Căn lề cho ô bảng là việc của bảng, không phải của nút này.
        types: ['heading', 'paragraph'],
      }),
      // Màu chữ, màu nền chữ và cỡ chữ. `TextStyleKit` gộp sẵn cả ba nên không cần
      // `@tiptap/extension-color` riêng — đã gỡ gói đó.
      TextStyleKit,
      TableKit.configure({
        table: {
          resizable: false,
          // Outlook bỏ qua `width` dạng % trên `<table>` lồng nhau ở vài phiên bản, nhưng
          // dựng đúng khi có thuộc tính HTML. Đặt sẵn để bảng không co lại thành một cột hẹp.
          HTMLAttributes: { border: '1', cellpadding: '6', cellspacing: '0', width: '100%' },
        },
      }),
    ],
    content: giaTri,
    onUpdate: ({ editor }) => onDoi(editor.getHTML()),
    editorProps: {
      attributes: {
        // `soan-thao` chứ không phải `prose`: dự án KHÔNG cài `@tailwindcss/typography`, mà
        // Tailwind im lặng bỏ qua class nó không biết — dùng `prose` thì tiêu đề và danh
        // sách hiện ra không định dạng gì, và không có lỗi nào chỉ ra vì sao. Kiểu nằm ở
        // `soan-thao.css`.
        class: cn('soan-thao px-3 py-2 focus:outline-none', doCao),
      },
    },
  })

  // Nạp lại khi nguồn đổi từ BÊN NGOÀI (đổi loại mẫu, bấm "khôi phục mặc định").
  //
  // Phải so với nội dung hiện tại trước khi ghi: `setContent` đặt lại con trỏ về đầu, nên gọi
  // vô điều kiện sẽ làm con trỏ nhảy về đầu sau MỖI ký tự người dùng gõ — ô soạn thảo không
  // dùng được, mà không có lỗi nào báo.
  useEffect(() => {
    if (editor && giaTri !== editor.getHTML()) {
      editor.commands.setContent(giaTri, { emitUpdate: false })
    }
  }, [giaTri, editor])

  if (!editor) return null

  return (
    <div className={cn('rounded-md border border-input bg-background', className)}>
      <ThanhCongCu editor={editor} />
      <EditorContent editor={editor} />
      <p className="border-t border-input px-3 py-1.5 text-xs text-muted-foreground">
        {t('email.ghiChuSoanThao')}
      </p>
    </div>
  )
}

/**
 * Bảng màu chọn sẵn, KHÔNG có bộ chọn tuỳ ý.
 *
 * Mỗi màu ở đây đọc được trên cả nền trắng và nền tối của trình đọc mail. Cho chọn tự do thì
 * người soạn đặt chữ xám nhạt — họ thấy đẹp trên màn của họ, người nhận dùng chế độ tối thì
 * không đọc được, và không ai biết cho tới khi khách phàn nàn.
 */
const MAU_CHU = [
  { ma: '#1f2937', ten: 'mauDen' },
  { ma: '#b91c1c', ten: 'mauDo' },
  { ma: '#15803d', ten: 'mauXanhLa' },
  { ma: '#1d4ed8', ten: 'mauXanhDuong' },
  { ma: '#a16207', ten: 'mauNau' },
] as const

/** Màu nền chữ (highlight) — chỉ màu nhạt, để chữ đen trên đó vẫn đọc được. */
const MAU_NEN = [
  { ma: '#fef08a', ten: 'nenVang' },
  { ma: '#bbf7d0', ten: 'nenXanh' },
  { ma: '#fecaca', ten: 'nenDo' },
  { ma: '#e5e7eb', ten: 'nenXam' },
] as const

/**
 * Ba cỡ chữ cố định, đơn vị px.
 *
 * `em`/`rem` bị Outlook tính sai khi lồng nhiều cấp; px thì mọi trình đọc đều dựng đúng.
 * Không cho nhập tự do: `8px` là thư không ai đọc nổi, `40px` là vỡ bố cục trên điện thoại.
 */
const CO_CHU = [
  { ma: '13px', ten: 'coNho' },
  { ma: '15px', ten: 'coThuong' },
  { ma: '20px', ten: 'coLon' },
] as const

function ThanhCongCu({ editor }: { editor: Editor }) {
  const { t } = useTranslation()

  const nut = (
    nhan: React.ReactNode,
    dangBat: boolean,
    bam: () => void,
    tieuDe: string,
  ) => (
    <button
      type="button"
      title={tieuDe}
      aria-label={tieuDe}
      aria-pressed={dangBat}
      onClick={bam}
      className={cn(
        'flex h-7 min-w-7 items-center justify-center rounded px-1.5 text-xs',
        'font-medium transition-colors',
        dangBat ? 'bg-primary text-primary-foreground' : 'hover:bg-muted',
      )}
    >
      {nhan}
    </button>
  )

  const vach = () => <span className="mx-1 h-4 w-px bg-border" />

  const datLienKet = () => {
    const dangCo = editor.getAttributes('link').href as string | undefined
    // `window.prompt` chứ không phải modal riêng: đặt liên kết là thao tác hiếm, một dòng nhập
    // là đủ. Dựng modal cho nó là thêm một luồng trạng thái phải bảo trì mà không đổi được gì.
    const url = window.prompt(t('email.nhapLienKet'), dangCo ?? 'https://')
    if (url === null) return // bấm Huỷ — khác với xoá liên kết

    if (url.trim() === '') {
      editor.chain().focus().extendMarkRange('link').unsetLink().run()
      return
    }
    editor.chain().focus().extendMarkRange('link').setLink({ href: url.trim() }).run()
  }

  return (
    <div className="flex flex-wrap items-center gap-0.5 border-b border-input px-2 py-1">
      {/* Hoàn tác / làm lại: Ctrl+Z có sẵn, nhưng người dùng không quen phím tắt vẫn cần nút. */}
      {nut(<Undo2 className="h-3.5 w-3.5" />, false,
        () => editor.chain().focus().undo().run(), t('email.hoanTac'))}
      {nut(<Redo2 className="h-3.5 w-3.5" />, false,
        () => editor.chain().focus().redo().run(), t('email.lamLai'))}

      {vach()}

      {nut(<strong>B</strong>, editor.isActive('bold'),
        () => editor.chain().focus().toggleBold().run(), t('email.dam'))}
      {nut(<em>I</em>, editor.isActive('italic'),
        () => editor.chain().focus().toggleItalic().run(), t('email.nghieng'))}
      {nut(<u>U</u>, editor.isActive('underline'),
        () => editor.chain().focus().toggleUnderline().run(), t('email.gachChan'))}
      {nut(<s>S</s>, editor.isActive('strike'),
        () => editor.chain().focus().toggleStrike().run(), t('email.gachNgang'))}

      {vach()}

      {nut('H2', editor.isActive('heading', { level: 2 }),
        () => editor.chain().focus().toggleHeading({ level: 2 }).run(), t('email.tieuDe2'))}
      {nut('H3', editor.isActive('heading', { level: 3 }),
        () => editor.chain().focus().toggleHeading({ level: 3 }).run(), t('email.tieuDe3'))}

      {vach()}

      {nut('•', editor.isActive('bulletList'),
        () => editor.chain().focus().toggleBulletList().run(), t('email.danhSachCham'))}
      {nut('1.', editor.isActive('orderedList'),
        () => editor.chain().focus().toggleOrderedList().run(), t('email.danhSachSo'))}

      {vach()}

      {nut(<AlignLeft className="h-3.5 w-3.5" />, editor.isActive({ textAlign: 'left' }),
        () => editor.chain().focus().setTextAlign('left').run(), t('email.canTrai'))}
      {nut(<AlignCenter className="h-3.5 w-3.5" />, editor.isActive({ textAlign: 'center' }),
        () => editor.chain().focus().setTextAlign('center').run(), t('email.canGiua'))}
      {nut(<AlignRight className="h-3.5 w-3.5" />, editor.isActive({ textAlign: 'right' }),
        () => editor.chain().focus().setTextAlign('right').run(), t('email.canPhai'))}
      {nut(<AlignJustify className="h-3.5 w-3.5" />, editor.isActive({ textAlign: 'justify' }),
        () => editor.chain().focus().setTextAlign('justify').run(), t('email.canDeu'))}

      {vach()}

      {/* Cỡ chữ: ô chọn thay vì ba nút — ba nút chiếm chỗ mà vẫn phải đoán nút nào đang bật. */}
      <select
        aria-label={t('email.coChu')}
        title={t('email.coChu')}
        className="h-7 rounded border border-input bg-background px-1 text-xs"
        value={(editor.getAttributes('textStyle').fontSize as string) ?? ''}
        onChange={(e) => {
          const v = e.target.value
          if (v) editor.chain().focus().setFontSize(v).run()
          else editor.chain().focus().unsetFontSize().run()
        }}
      >
        <option value="">{t('email.coMacDinh')}</option>
        {CO_CHU.map((c) => (
          <option key={c.ma} value={c.ma}>{t(`email.${c.ten}`)}</option>
        ))}
      </select>

      {vach()}

      {/* Màu chữ và màu nền: ô vuông màu, bấm là áp. Nút ✕ gỡ màu về mặc định. */}
      <span className="flex items-center gap-0.5" title={t('email.mauChu')}>
        {MAU_CHU.map((m) => (
          <button
            key={m.ma}
            type="button"
            aria-label={t(`email.${m.ten}`)}
            title={t(`email.${m.ten}`)}
            onClick={() => editor.chain().focus().setColor(m.ma).run()}
            className={cn(
              'h-5 w-5 rounded border transition-transform hover:scale-110',
              editor.isActive('textStyle', { color: m.ma })
                ? 'border-foreground ring-1 ring-foreground'
                : 'border-border',
            )}
            style={{ backgroundColor: m.ma }}
          />
        ))}
        {nut('✕', false, () => editor.chain().focus().unsetColor().run(), t('email.boMauChu'))}
      </span>

      {vach()}

      <span className="flex items-center gap-0.5" title={t('email.mauNen')}>
        {MAU_NEN.map((m) => (
          <button
            key={m.ma}
            type="button"
            aria-label={t(`email.${m.ten}`)}
            title={t(`email.${m.ten}`)}
            onClick={() => editor.chain().focus().setBackgroundColor(m.ma).run()}
            className={cn(
              'h-5 w-5 rounded border transition-transform hover:scale-110',
              editor.isActive('textStyle', { backgroundColor: m.ma })
                ? 'border-foreground ring-1 ring-foreground'
                : 'border-border',
            )}
            style={{ backgroundColor: m.ma }}
          />
        ))}
        {nut('✕', false,
          () => editor.chain().focus().unsetBackgroundColor().run(), t('email.boMauNen'))}
      </span>

      {vach()}

      {nut(<Link2 className="h-3.5 w-3.5" />, editor.isActive('link'), datLienKet,
        t('email.lienKet'))}

      {vach()}

      {/* Bảng: chèn 3×3 có dòng tiêu đề. Khi con trỏ đang trong bảng thì hiện thêm nút
          thêm/xoá dòng cột — giấu lúc không cần để thanh công cụ không dài vô ích. */}
      {nut(<TableIcon className="h-3.5 w-3.5" />, editor.isActive('table'),
        () => editor.chain().focus()
          .insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(),
        t('email.chenBang'))}

      {editor.isActive('table') && (
        <>
          {nut('+↓', false,
            () => editor.chain().focus().addRowAfter().run(), t('email.themDong'))}
          {nut('+→', false,
            () => editor.chain().focus().addColumnAfter().run(), t('email.themCot'))}
          {nut('−↓', false,
            () => editor.chain().focus().deleteRow().run(), t('email.xoaDong'))}
          {nut('−→', false,
            () => editor.chain().focus().deleteColumn().run(), t('email.xoaCot'))}
          {nut(<Trash2 className="h-3.5 w-3.5" />, false,
            () => editor.chain().focus().deleteTable().run(), t('email.xoaBang'))}
        </>
      )}
    </div>
  )
}
