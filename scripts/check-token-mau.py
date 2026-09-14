#!/usr/bin/env python3
r"""Kiểm mọi token màu dùng trong frontend đều thật sự tồn tại.

## Vì sao cần script riêng

Tên token màu là **chuỗi trong className** hoặc trong `hsl(var(--...))`, nên:

- `tsc` không kiểm (chỉ là string),
- `oxlint` không kiểm (không biết Tailwind config),
- và Tailwind **im lặng bỏ qua** class không khớp config.

Hệ quả: token sai không làm gì đỏ, chỉ làm **màu biến mất** — nền trong suốt, viền về xám mặc
định. Đúng thứ chỉ ảnh chụp màn hình mới bắt được, mà không ai chụp mọi trạng thái.

Đã xảy ra ba lần trong dự án này:

1. `hsl(var(--cho))` — token thật là `--status-cho`. Ô "cần cân nhắc" ở màn phân quyền khi BẬT
   mất hẳn màu lẫn dấu tích (14/09/2026).
2. `border-status-warn` / `bg-status-warn` ở cảnh báo trùng số điện thoại — nền trong suốt
   hoàn toàn, nên cảnh báo cần nổi bật nhất lại hiện như văn bản thường (phát hiện 15/09/2026
   khi rà bản v1).
3. `text-status-warn` ở FR-15 Tổng quan — cùng lỗi, cùng ngày.

Chạy: python3 scripts/check-token-mau.py   (mã thoát 1 nếu có token lạ)
"""
import re
import sys
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent
FE = GOC / "frontend"

# Tiền tố Tailwind có thể đứng trước tên màu. Không cần đủ hết — chỉ cần những cái dự án dùng.
TIEN_TO = r"(?:bg|text|border|ring|fill|stroke|from|via|to|decoration|outline|shadow|accent|caret|divide|placeholder)"


def doc(p: Path) -> str:
    if not p.exists():
        sys.exit(f"Không thấy {p} — cấu trúc dự án đã đổi, sửa script này.")
    return p.read_text(encoding="utf-8")


def mau_khai_trong_tailwind() -> set[str]:
    """Tên màu Tailwind nhận, gồm cả dạng lồng (`status.ok` -> `status-ok`)."""
    s = doc(FE / "tailwind.config.js")
    m = re.search(r"colors:\s*\{(.*?)\n      \},", s, re.S)
    if not m:
        sys.exit("Không đọc được khối `colors` trong tailwind.config.js — sửa regex script này.")
    blok = m.group(1)

    ten: set[str] = set()
    nhom_hien_tai = None
    for dong in blok.splitlines():
        if re.match(r"\s*//", dong):
            continue
        # `status: {` mở một nhóm lồng
        mo = re.match(r"\s*([\w-]+):\s*\{\s*$", dong)
        if mo:
            nhom_hien_tai = mo.group(1)
            ten.add(nhom_hien_tai)          # `bg-status` (DEFAULT) cũng hợp lệ nếu có
            continue
        if re.match(r"\s*\},?\s*$", dong):
            nhom_hien_tai = None
            continue
        # `ok: 'hsl(...)'` hoặc `primary: 'hsl(...)'`
        la = re.match(r"\s*([\w-]+):\s*['\"]", dong)
        if la:
            khoa = la.group(1)
            if khoa == "DEFAULT":
                continue
            ten.add(f"{nhom_hien_tai}-{khoa}" if nhom_hien_tai else khoa)
    return ten


def bien_css_khai() -> set[str]:
    """Biến CSS khai trong index.css, ví dụ `--status-cho`."""
    s = doc(FE / "src" / "index.css")
    return set(re.findall(r"^\s*--([\w-]+):", s, re.M))


def quet_nguon() -> tuple[list[tuple[str, int, str]], list[tuple[str, int, str]]]:
    """Trả (class màu đang dùng, biến css đang dùng) kèm vị trí."""
    class_mau: list[tuple[str, int, str]] = []
    bien: list[tuple[str, int, str]] = []
    for f in sorted((FE / "src").rglob("*")):
        if f.suffix not in {".tsx", ".ts", ".css"} or f.name.endswith(".test.ts"):
            continue
        for i, dong in enumerate(f.read_text(encoding="utf-8").splitlines(), 1):
            rel = str(f.relative_to(GOC))
            # `bg-status-warn`, `text-status-cho/40`, `border-primary`
            for m in re.finditer(TIEN_TO + r"-(status-[\w-]+)", dong):
                class_mau.append((rel, i, m.group(1)))
            # `hsl(var(--cho))`
            for m in re.finditer(r"var\(--([\w-]+)", dong):
                bien.append((rel, i, m.group(1)))
    return class_mau, bien


def main() -> int:
    mau = mau_khai_trong_tailwind()
    css = bien_css_khai()
    class_mau, bien = quet_nguon()

    # Tự kiểm: quét ra quá ít nghĩa là regex hỏng, không phải "mọi thứ đều ổn".
    if len(mau) < 5 or not class_mau:
        sys.exit(
            f"Chỉ đọc được {len(mau)} tên màu và {len(class_mau)} class màu — regex hỏng "
            "hoặc cấu trúc đã đổi. Sửa script, đừng bỏ qua."
        )

    loi = []
    for f, ln, ten in class_mau:
        if ten not in mau:
            gan = ", ".join(sorted(x for x in mau if x.startswith("status"))) or "—"
            loi.append(f"  {f}:{ln}  class màu `{ten}` KHÔNG có trong tailwind.config.js\n"
                       f"      → token trạng thái hợp lệ: {gan}")
    for f, ln, ten in bien:
        if ten not in css:
            loi.append(f"  {f}:{ln}  biến CSS `--{ten}` KHÔNG khai trong index.css\n"
                       f"      → có phải bạn muốn `--status-{ten}`?")

    if loi:
        print("Token màu không tồn tại (Tailwind im lặng bỏ qua ⇒ MẤT MÀU, không có lỗi nào):")
        print("\n".join(loi))
        return 1

    print(f"Token màu hợp lệ ({len(mau)} tên màu, {len(css)} biến CSS, "
          f"{len(class_mau)} chỗ dùng class trạng thái).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
