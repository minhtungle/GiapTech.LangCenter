#!/usr/bin/env python3
r"""Kiểm tài liệu còn khớp với mã nguồn — bắt chỗ tài liệu đã lạc hậu.

## Vì sao cần script này

Quy tắc #4 nói "đổi schema/API → cập nhật tài liệu trong cùng PR", nhưng **không có gì canh**.
Rà 18/10/2026 phát hiện: bảng `NGON_NGU` thêm từ 11/10 mà ERD không có dòng nào, CLAUDE.md
ghi 53 bảng trong khi code có 54, và số test lệch 16 đơn vị. Không có lỗi nào báo — tài liệu
cứ âm thầm trôi khỏi mã, cho tới khi người đọc tin vào con số sai.

Bốn thứ script canh, chọn vì chúng **đếm được** và **hay đổi**:

1. Mọi entity có `IEntityTypeConfiguration` phải xuất hiện trong ERD.
2. Số bảng CLAUDE.md và ERD công bố phải khớp số `DbSet` thật.
3. Mọi mã FR nhắc trong CLAUDE.md phải có trong `docs/06-nghiep-vu/`.
4. Mọi script trong `scripts/` phải được nhắc trong tài liệu (kẻo không ai biết nó tồn tại).

**Không** canh số test: nó đổi mỗi lần thêm test, và sai số đó vô hại. Canh thứ vô hại chỉ
làm script hay đỏ oan rồi bị bỏ qua.

Chạy: python3 scripts/check-tai-lieu-khop-ma.py   (mã thoát 1 nếu lệch)
"""
import re
import sys
from pathlib import Path

GOC = Path(__file__).resolve().parent.parent
loi: list[str] = []


def doc(p: Path) -> str:
    if not p.exists():
        sys.exit(f"Không thấy {p} — cấu trúc dự án đã đổi, sửa script này.")
    return p.read_text(encoding="utf-8")


# ---------- 1 + 2. Bảng trong ERD ----------

def ten_bang_trong_ma() -> set[str]:
    """Tên bảng lấy từ `ToTable("...")` trong các file cấu hình EF."""
    ten = set()
    for f in (GOC / "src/GiapTech.LangCenter.Infrastructure/Persistence/Configurations").glob("*.cs"):
        ten |= set(re.findall(r'ToTable\(\s*"([A-Z0-9_]+)"', f.read_text(encoding="utf-8")))
    return ten


def kiem_erd() -> None:
    erd = doc(GOC / "docs/05-database/erd.md")
    thieu = sorted(t for t in ten_bang_trong_ma() if t not in erd)
    if thieu:
        loi.append(
            "ERD thiếu bảng: " + ", ".join(thieu)
            + "\n    → thêm vào docs/05-database/erd.md (quy tắc #4)"
        )


def so_dbset() -> int:
    s = doc(GOC / "src/GiapTech.LangCenter.Infrastructure/Persistence/AppDbContext.cs")
    return len(re.findall(r"public DbSet<", s))


def kiem_so_bang() -> None:
    """Con số "N bảng" công bố ở CLAUDE.md và ERD phải khớp số DbSet thật.

    Chỉ xét con số ĐẦU TIÊN của mỗi file: đó là con số tổng; các con số sau là số bảng của
    từng nhóm ("15 bảng hệ thống"…), không phải tổng.
    """
    that = so_dbset()
    for ten_file, p in (
        ("CLAUDE.md", GOC / "CLAUDE.md"),
        ("docs/05-database/erd.md", GOC / "docs/05-database/erd.md"),
    ):
        m = re.search(r"(\d+)\s+bảng", doc(p))
        if m and int(m.group(1)) != that:
            loi.append(
                f"{ten_file} ghi {m.group(1)} bảng, nhưng AppDbContext có {that} DbSet"
                f"\n    → sửa con số, hoặc kiểm lại xem DbSet mới có đúng là bảng nghiệp vụ không"
            )


# ---------- 3. Mã FR ----------

def kiem_fr() -> None:
    claude = doc(GOC / "CLAUDE.md")
    nghiep_vu = "\n".join(
        f.read_text(encoding="utf-8") for f in (GOC / "docs/06-nghiep-vu").glob("*.md")
    )
    thieu = sorted(
        {fr for fr in re.findall(r"FR-\d+", claude) if fr not in nghiep_vu},
        key=lambda x: int(x.split("-")[1]),
    )
    if thieu:
        loi.append(
            "CLAUDE.md nhắc mã FR chưa có trong docs/06-nghiep-vu/: " + ", ".join(thieu)
            + "\n    → mô tả FR trước khi code (mục 5 CLAUDE.md), hoặc bỏ mã khỏi CLAUDE.md"
        )


# ---------- 4. Script phải được nhắc trong tài liệu ----------

def kiem_script() -> None:
    """Script không ai biết là script không ai chạy.

    Bỏ qua script dùng một lần của đợt chuyển dữ liệu — chúng nằm trong thư mục riêng và
    có README của chính nó.
    """
    tai_lieu = "\n".join(
        f.read_text(encoding="utf-8") for f in GOC.glob("docs/**/*.md")
    ) + doc(GOC / "CLAUDE.md")

    thieu = sorted(
        f.name
        for f in (GOC / "scripts").iterdir()
        if f.is_file() and f.suffix in (".py", ".sh") and f.name not in tai_lieu
    )
    if thieu:
        loi.append(
            "Script không được nhắc ở tài liệu nào: " + ", ".join(thieu)
            + "\n    → thêm vào CLAUDE.md mục 7 hoặc docs/07-ha-tang/"
        )


def main() -> int:
    kiem_erd()
    kiem_so_bang()
    kiem_fr()
    kiem_script()

    if loi:
        print("Tài liệu đã lạc hậu so với mã nguồn:\n")
        for x in loi:
            print(f"  ✗ {x}\n")
        return 1

    print(f"Tài liệu khớp mã nguồn ({so_dbset()} bảng, ERD đầy đủ, FR và script đều có mô tả).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
