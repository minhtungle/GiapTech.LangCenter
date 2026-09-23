#!/usr/bin/env python3
"""Sinh `frontend/src/lib/ngon-ngu/<ma>.ts` từ tệp JSON phẳng đã dịch.

Vì sao có script này thay vì sửa tay: 1197 khoá × 4 ngôn ngữ. Gõ tay thì chắc chắn có chỗ
lệch cấu trúc so với `vi.ts`, mà lệch cấu trúc nghĩa là khoá đó **âm thầm rơi về tiếng Việt**
— không lỗi, không cảnh báo, chỉ một câu tiếng Việt lạc giữa màn hình tiếng Nhật.

Script đọc `vi.ts` để lấy **thứ tự và hình dạng** khoá, rồi đổ bản dịch vào đúng khuôn đó. Nhờ
vậy hai tệp luôn cùng cấu trúc, và `check-i18n-keys.py` chỉ còn phải kiểm nội dung.

Dùng: python3 scripts/sinh-tep-ngon-ngu.py en /đường/dẫn/en-phang.json
"""
import json
import pathlib
import re
import subprocess
import sys

GOC = pathlib.Path(__file__).resolve().parent.parent / "frontend"

MO_TA = {
    "en": "English — dịch từ `vi.ts`",
    "zh": "简体中文 (tiếng Trung giản thể) — dịch từ `vi.ts`",
    "ko": "한국어 (tiếng Hàn) — dịch từ `vi.ts`",
    "ja": "日本語 (tiếng Nhật) — dịch từ `vi.ts`",
}


def doc_vi_phang() -> dict:
    """Lấy `vi.ts` dưới dạng dict phẳng, qua Node để không tự parse TypeScript bằng regex."""
    js = r"""
import { readFileSync } from 'node:fs'
const src = readFileSync(process.argv[2], 'utf8')
const than = src.slice(src.indexOf('const vi = {') + 'const vi = '.length,
                       src.lastIndexOf('export default vi')).trim()
const vi = eval('(' + than + ')')
const ra = {}
const di = (o, p = []) => {
  for (const [k, v] of Object.entries(o)) {
    const key = [...p, k]
    if (Array.isArray(v)) v.forEach((x, i) => (ra[[...key, i].join('.')] = x))
    else if (v && typeof v === 'object') di(v, key)
    else ra[key.join('.')] = v
  }
}
di(vi)
process.stdout.write(JSON.stringify(ra))
"""
    tam = pathlib.Path("/tmp/_doc_vi.mjs")
    tam.write_text(js)
    kq = subprocess.run(
        ["node", str(tam), str(GOC / "src/lib/ngon-ngu/vi.ts")],
        capture_output=True, text=True, check=True,
    )
    return json.loads(kq.stdout)


def lam_to(o) -> dict:
    """Dựng lại cây lồng nhau từ dict phẳng; khoá toàn số liên tiếp thì thành mảng."""
    cay: dict = {}
    for k, v in o.items():
        cho = cay
        phan = k.split(".")
        for p in phan[:-1]:
            cho = cho.setdefault(p, {})
        cho[phan[-1]] = v

    def doi_mang(n):
        if not isinstance(n, dict):
            return n
        n = {k: doi_mang(v) for k, v in n.items()}
        if n and all(k.isdigit() for k in n):
            return [n[str(i)] for i in range(len(n))]
        return n

    return doi_mang(cay)


def ra_ts(n, cap: int = 0) -> str:
    """In cây thành literal TypeScript. Dùng nháy đơn và escape đúng kiểu JS."""
    lom = "  " * cap
    if isinstance(n, list):
        muc = [f"{lom}  {ra_ts(x, cap + 1)}," for x in n]
        return "[\n" + "\n".join(muc) + f"\n{lom}]"
    if isinstance(n, dict):
        dong = []
        for k, v in n.items():
            # Khoá không phải định danh JS hợp lệ thì phải bọc nháy.
            kh = k if re.fullmatch(r"[A-Za-z_$][A-Za-z0-9_$]*", k) else f"'{k}'"
            dong.append(f"{lom}  {kh}: {ra_ts(v, cap + 1)},")
        return "{\n" + "\n".join(dong) + f"\n{lom}}}"
    s = str(n).replace("\\", "\\\\").replace("'", "\\'").replace("\n", "\\n")
    return f"'{s}'"


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2

    ma, nguon = sys.argv[1], pathlib.Path(sys.argv[2])
    dich = json.loads(nguon.read_text(encoding="utf-8"))
    goc = doc_vi_phang()

    thieu = set(goc) - set(dich)
    thua = set(dich) - set(goc)
    if thieu or thua:
        print(f"✗ {ma}: lệch khoá so với vi.ts — thiếu {len(thieu)}, thừa {len(thua)}")
        for k in sorted(thieu)[:8]:
            print(f"    thiếu: {k}")
        for k in sorted(thua)[:8]:
            print(f"    thừa:  {k}")
        return 1

    # Đổ theo THỨ TỰ của vi.ts để hai tệp đọc song song được, dễ soi khi rà bản dịch.
    theo_thu_tu = {k: dich[k] for k in goc}

    noi_dung = (
        f"/**\n"
        f" * **{MO_TA.get(ma, ma)}** (23/09/2026).\n"
        f" *\n"
        f" * Sinh bằng `scripts/sinh-tep-ngon-ngu.py` để cấu trúc khoá **luôn khớp `vi.ts`**.\n"
        f" * Sửa tay được, nhưng đừng thêm/bớt khoá ở đây — thêm ở `vi.ts` trước, vì\n"
        f" * `check-i18n-keys.py` so mọi ngôn ngữ với tệp đó.\n"
        f" *\n"
        f" * Chỉ dịch GIAO DIỆN. Dữ liệu người dùng nhập (tên lớp, tên học viên, ghi chú)\n"
        f" * giữ nguyên — xem `danhSach.ts`.\n"
        f" */\n"
        f"const {ma} = {ra_ts(lam_to(theo_thu_tu))}\n\n"
        f"export default {ma}\n"
    )
    (GOC / f"src/lib/ngon-ngu/{ma}.ts").write_text(noi_dung, encoding="utf-8")
    print(f"✓ {ma}.ts — {len(goc)} khoá")
    return 0


if __name__ == "__main__":
    sys.exit(main())
