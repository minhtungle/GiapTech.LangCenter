/**
 * Nhận diện trung tâm (logo, tên viết tắt) — dùng chung cho **màn đăng nhập** và **sidebar**
 * (22/09/2026).
 *
 * Tách khỏi `Layout.tsx` khi màn đăng nhập cần cùng một cách viết tắt: để nguyên chỗ cũ thì
 * hoặc phải import từ một component (kéo theo cả cây phụ thuộc của nó), hoặc chép lại — mà hai
 * bản chép sẽ trôi khỏi nhau và cùng một trung tâm hiện hai chữ khác nhau ở hai màn.
 */

/**
 * Chữ viết tắt cho ô logo: lấy chữ cái đầu của 2 từ cuối, bỏ các từ chung ("trung tâm",
 * "ngoại ngữ") vì gần như tên nào cũng có — để lại thì mọi ô đều hiện "TT".
 */
export function vietTat(tenTrungTam?: string | null) {
  if (!tenTrungTam) return 'TT'
  const tu = tenTrungTam
    .trim()
    .split(/\s+/)
    .filter((t) => !['trung', 'tam', 'tt', 'ngoai', 'ngu'].includes(t.toLowerCase()))
  const lay = tu.slice(-2)
  return (lay.map((t) => t[0]).join('') || tenTrungTam[0]).toUpperCase()
}
