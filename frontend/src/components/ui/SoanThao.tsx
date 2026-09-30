import { useEffect } from 'react'
import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Link from '@tiptap/extension-link'
import { useTranslation } from 'react-i18next'
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
 * ## Vì sao thanh công cụ ít nút
 *
 * Đây là ô soạn **email**, không phải soạn trang web. Outlook trên Windows dựng HTML bằng
 * engine của Word — không có flexbox, không grid, không `border-radius`, bảng mới là thứ
 * chạy được. Mỗi nút thừa ở đây là một cách để người dùng tạo ra email vỡ trên máy người
 * nhận mà họ không bao giờ nhìn thấy.
 *
 * Nên bộ nút chỉ gồm những thẻ Outlook dựng đúng: đậm, nghiêng, tiêu đề, danh sách, liên kết.
 * Cố ý KHÔNG có: bảng (người dùng tự dựng bảng sẽ hỏng bố cục khung bao ngoài), ảnh (phải
 * là URL tuyệt đối công khai, mà ảnh tải lên MinIO thì API làm proxy sau đăng nhập — dán
 * vào email sẽ hiện ô vỡ), màu chữ và cỡ chữ (dễ tạo ra chữ không đọc được trên nền tối của
 * trình đọc mail).
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

function ThanhCongCu({ editor }: { editor: Editor }) {
  const { t } = useTranslation()

  const nut = (
    nhan: string,
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
        'h-7 min-w-7 rounded px-1.5 text-xs font-medium transition-colors',
        dangBat ? 'bg-primary text-primary-foreground' : 'hover:bg-muted',
      )}
    >
      {nhan}
    </button>
  )

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
      {nut('B', editor.isActive('bold'),
        () => editor.chain().focus().toggleBold().run(), t('email.dam'))}
      {nut('I', editor.isActive('italic'),
        () => editor.chain().focus().toggleItalic().run(), t('email.nghieng'))}

      <span className="mx-1 h-4 w-px bg-border" />

      {nut('H2', editor.isActive('heading', { level: 2 }),
        () => editor.chain().focus().toggleHeading({ level: 2 }).run(), t('email.tieuDe2'))}
      {nut('H3', editor.isActive('heading', { level: 3 }),
        () => editor.chain().focus().toggleHeading({ level: 3 }).run(), t('email.tieuDe3'))}

      <span className="mx-1 h-4 w-px bg-border" />

      {nut('•', editor.isActive('bulletList'),
        () => editor.chain().focus().toggleBulletList().run(), t('email.danhSachCham'))}
      {nut('1.', editor.isActive('orderedList'),
        () => editor.chain().focus().toggleOrderedList().run(), t('email.danhSachSo'))}

      <span className="mx-1 h-4 w-px bg-border" />

      {nut('🔗', editor.isActive('link'), datLienKet, t('email.lienKet'))}
    </div>
  )
}
