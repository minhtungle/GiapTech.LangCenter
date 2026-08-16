#!/usr/bin/env python3
"""
Quét toàn bộ file .md/.html trong repo, tìm liên kết nội bộ tương đối
([...](...)  hoặc href=/src=) và xác nhận file đích tồn tại.
Chạy sau mỗi đợt sửa nhiều file tài liệu (xem CONTRIBUTING.md mục 5).

Usage:
    python scripts/check-doc-links.py
Exit code 0 nếu không có link hỏng, 1 nếu có (để dùng trong CI).
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD_LINK_RE = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
HTML_LINK_RE = re.compile(r'(?:href|src)="([^"]+)"')

SKIP_DIRS = {".git", "node_modules", "bin", "obj", "dist", ".github", ".agents", ".claude"}


def is_external(link: str) -> bool:
    # Đường dẫn bắt đầu bằng "/" là URL tuyệt đối lúc chạy (vd asset của Vite trong
    # index.html), không phải liên kết tài liệu — bỏ qua.
    return link.startswith(("http://", "https://", "mailto:", "#", "/"))


def find_doc_files():
    for path in ROOT.rglob("*"):
        if path.is_dir():
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.suffix in (".md", ".html"):
            yield path


def check_file(path: Path):
    broken = []
    text = path.read_text(encoding="utf-8", errors="ignore")
    links = MD_LINK_RE.findall(text) + HTML_LINK_RE.findall(text)
    for link in links:
        link = link.split("#")[0].strip()
        if not link or is_external(link):
            continue
        target = (path.parent / link).resolve()
        if not target.exists():
            broken.append(link)
    return broken


def main():
    total_broken = 0
    for f in sorted(find_doc_files()):
        broken = check_file(f)
        if broken:
            total_broken += len(broken)
            rel = f.relative_to(ROOT)
            print(f"[HỎNG] {rel}:")
            for link in broken:
                print(f"    -> {link}")
    if total_broken:
        print(f"\nTổng cộng {total_broken} liên kết hỏng.")
        sys.exit(1)
    print("Không có liên kết nội bộ hỏng.")
    sys.exit(0)


if __name__ == "__main__":
    main()
