#!/usr/bin/env python3
r"""Kiểm mọi chức năng và thao tác phân quyền đều có nhãn tiếng Việt ở frontend.

Vì sao cần script riêng, `check-i18n-keys.py` không đủ: màn phân quyền dựng khoá **động**
(`t(\`chucNang.${cn}\`)`), nên bộ quét khoá tĩnh không thấy. Thiếu nhãn KHÔNG làm build đỏ —
người quản trị chỉ thấy tên enum thô như `GhiDanhKhoaOnline` giữa bảng tiếng Việt, và phải đoán
ô đó cấp quyền gì. Đúng thứ làm phân quyền mất ý nghĩa.

Chạy: python3 scripts/check-nhan-phan-quyen.py   (mã thoát 1 nếu thiếu)
"""
import re
import sys
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent


def doc(p: Path) -> str:
    if not p.exists():
        sys.exit(f"Không thấy {p} — cấu trúc dự án đã đổi, sửa script này.")
    return p.read_text(encoding="utf-8")


def chuc_nang_backend() -> list[str]:
    s = doc(GOC / "src/GiapTech.LangCenter.Domain/Common/ChucNang.cs")
    m = re.search(r"TatCa\s*=\s*\[(.*?)\];", s, re.S)
    if not m:
        sys.exit("Không đọc được `ChucNang.TatCa` — sửa regex trong script này.")
    return [t for t in (x.strip() for x in re.split(r"[,\n]", m.group(1)))
            if re.fullmatch(r"\w+", t)]


def hanh_dong_backend() -> list[str]:
    s = doc(GOC / "src/GiapTech.LangCenter.Domain/Enums/Enums.cs")
    m = re.search(r"public enum HanhDong\s*\{(.*?)\n\}", s, re.S)
    if not m:
        sys.exit("Không đọc được `enum HanhDong` — sửa regex trong script này.")
    return re.findall(r"^\s*(\w+)\s*=\s*\d+", m.group(1), re.M)


def nhan_frontend(ten: str) -> set[str]:
    # Bảng dịch tách khỏi `i18n.ts` từ 23/09/2026 — `vi.ts` là nguồn chân lý.
    s = doc(GOC / "frontend/src/lib/ngon-ngu/vi.ts")
    m = re.search(r"\n  " + ten + r": \{(.*?)\n  \},", s, re.S)
    if not m:
        sys.exit(f"Không thấy khối `{ten}` trong ngon-ngu/vi.ts — sửa regex trong script này.")
    return set(re.findall(r"^\s{4}(\w+):", m.group(1), re.M))


def kieu_hanh_dong_ts() -> set[str]:
    """Giá trị trong `export type HanhDong` ở frontend/src/lib/quyen.ts.

    Thiếu giá trị ở đây thì `coQuyen('LopHoc', 'HoanTat')` không biên dịch được — lỗi hiện ngay,
    nhưng chỉ ở chỗ nào có người viết lời gọi. Kiểm ở đây để biết TRƯỚC khi ai đó cần dùng.
    """
    s = doc(GOC / "frontend/src/lib/quyen.ts")
    m = re.search(r"export type HanhDong\s*=(.*?)\n\n", s, re.S)
    if not m:
        sys.exit("Không thấy `export type HanhDong` trong quyen.ts — sửa regex trong script này.")
    return set(re.findall(r"'(\w+)'", m.group(1)))


def main() -> int:
    be_cn, be_hd = chuc_nang_backend(), hanh_dong_backend()

    # Tự kiểm: đọc được ít bất thường nghĩa là regex hỏng, không phải "mọi thứ đều ổn".
    if len(be_cn) < 10 or len(be_hd) < 4:
        sys.exit(f"Chỉ đọc được {len(be_cn)} chức năng / {len(be_hd)} thao tác — regex hỏng.")

    loi = []
    for ten, be in (("chucNang", be_cn), ("hanhDong", be_hd)):
        fe = nhan_frontend(ten)
        if thieu := sorted(set(be) - fe):
            loi.append(
                f"  {ten} THIẾU nhãn: {', '.join(thieu)}\n"
                f"    → thêm vào khối `{ten}` trong frontend/src/lib/ngon-ngu/vi.ts"
            )
        if du := sorted(fe - set(be)):
            loi.append(
                f"  {ten} có nhãn DƯ (backend không còn): {', '.join(du)}\n"
                f"    → xoá khỏi khối `{ten}`, nhãn dư làm người sau tưởng chức năng vẫn tồn tại"
            )

    # Kiểu TS phải phủ đủ thao tác: thiếu thì không gọi `coQuyen` với thao tác đó được.
    ts = kieu_hanh_dong_ts()
    if thieu_ts := sorted(set(be_hd) - ts):
        loi.append(
            f"  `export type HanhDong` (quyen.ts) THIẾU: {', '.join(thieu_ts)}\n"
            f"    → thêm vào union kiểu, nếu không `coQuyen` không gọi được với thao tác đó"
        )
    if du_ts := sorted(ts - set(be_hd)):
        loi.append(
            f"  `export type HanhDong` (quyen.ts) có giá trị DƯ: {', '.join(du_ts)}\n"
            f"    → backend không còn thao tác này, xoá khỏi union"
        )

    if loi:
        print("Nhãn phân quyền chưa khớp backend:\n" + "\n".join(loi))
        return 1

    print(f"Đủ nhãn và kiểu phân quyền ({len(be_cn)} chức năng × {len(be_hd)} thao tác).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
