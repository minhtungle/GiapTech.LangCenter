#!/usr/bin/env python3
"""Kiểm mọi khoá t('ns.key') trong frontend đều có bản dịch trong i18n.ts.

Vì sao cần script riêng: thiếu bản dịch **không làm gì đỏ cả**. TypeScript không biết `t()`
nhận khoá gì, `npm run build` xanh, oxlint xanh, test API xanh — chỉ người dùng thấy chuỗi
`buoiHoc.linkHoc` ngay trên màn hình. Đã xảy ra hai lần:

- 07/09/2026 (sáng): tab lớp học ghép khoá động `t(`lopHoc.tab_${x}`)` → 4/6 tab hiện khoá thô.
- 07/09/2026 (chiều): tab Thông tin buổi dùng `buoiHoc.phongHoc` / `linkHoc` / `ghiChu` mà khối
  `buoiHoc` chưa có ba khoá đó. Lần này tôi đã "kiểm" bằng `grep -c` nhưng grep bắt được chúng ở
  namespace KHÁC nên kết luận sai — bài học: phải so khoá **theo namespace**, không theo tên trần.

Chỉ quét khoá TĨNH (chuỗi viết thẳng). **Khoá ghép động thì script KHÔNG thấy** — đã kiểm chứng
08/09/2026: xoá `hocPhi.pt.TienMat` (dùng qua `t(`hocPhi.pt.${x}`)`) mà script vẫn xanh.

Đó chính là lý do quy ước của dự án là gắn khoá tường minh vào từng mục (xem `CAC_TAB` trong
ChiTietLopHoc.tsx), và vì sao **enum → nhãn nên đặt ở namespace CẤP MỘT** khớp đúng tên enum:
`t(`phuongThucThanhToan.${x}`)` vẫn là khoá động, nhưng ít nhất một người đọc `i18n.ts` thấy
ngay khối đó ứng với enum nào để kiểm tay.
"""
import pathlib
import re
import sys

GOC = pathlib.Path(__file__).resolve().parent.parent / "frontend"


def khoa_da_dich() -> set[str]:
    """Bộ khoá 'ns.key' khai trong `const vi = {...}` của i18n.ts."""
    src = (GOC / "src/lib/i18n.ts").read_text(encoding="utf-8")
    dau = src.index("const vi = {")

    sau = None
    do_sau = 0
    for i in range(dau, len(src)):
        if src[i] == "{":
            do_sau += 1
        elif src[i] == "}":
            do_sau -= 1
            if do_sau == 0:
                sau = i
                break
    if sau is None:
        sys.exit("Không tìm được điểm kết của khối `const vi` trong i18n.ts")

    khoa: set[str] = set()
    ns = None
    ns2 = None
    for dong in src[dau:sau].splitlines():
        if m := re.match(r"^  ([a-zA-Z]+): \{", dong):
            ns = m.group(1)
            ns2 = None
        elif (m := re.match(r"^    ([a-zA-Z_0-9]+): \{", dong)) and ns:
            # Namespace LỒNG (vd `hocPhi.pt`): ghi nhận tiền tố hai cấp để khoá bên trong
            # cũng được đếm. Không có nhánh này thì `t('hocPhi.pt.TienMat')` bị báo thiếu
            # trong khi nó có thật — và ngược lại, khoá lồng thiếu thật thì script bỏ qua.
            ns2 = f"{ns}.{m.group(1)}"
        elif (m := re.match(r"^      ([a-zA-Z_0-9]+):", dong)) and ns2:
            khoa.add(f"{ns2}.{m.group(1)}")
        elif (m := re.match(r"^    ([a-zA-Z_0-9]+):", dong)) and ns:
            khoa.add(f"{ns}.{m.group(1)}")
    return khoa


def main() -> int:
    khoa = khoa_da_dich()
    thieu: dict[str, set[str]] = {}

    for tep in sorted((GOC / "src").rglob("*.tsx")):
        noi_dung = tep.read_text(encoding="utf-8")
        for m in re.finditer(r"t\(\s*'([a-zA-Z]+(?:\.[a-zA-Z_0-9]+){1,2})'", noi_dung):
            if m.group(1) not in khoa:
                thieu.setdefault(str(tep.relative_to(GOC.parent)), set()).add(m.group(1))

    if thieu:
        print("Khoá i18n KHÔNG có bản dịch — người dùng sẽ thấy chuỗi khoá trên màn hình:\n")
        for tep, ks in sorted(thieu.items()):
            for k in sorted(ks):
                print(f"  {tep}: {k}")
        print(f"\nThêm chúng vào `frontend/src/lib/i18n.ts` (quy tắc #3). "
              f"Đã kiểm {len(khoa)} khoá.")
        return 1

    print(f"Mọi khoá i18n tĩnh đều có bản dịch ({len(khoa)} khoá).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
