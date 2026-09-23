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
import json
import pathlib
import re
import subprocess
import sys

GOC = pathlib.Path(__file__).resolve().parent.parent / "frontend"


def khoa_da_dich() -> set[str]:
    """Bộ khoá 'ns.key' khai trong `const vi = {...}` của `ngon-ngu/vi.ts`."""
    src = (GOC / "src/lib/ngon-ngu/vi.ts").read_text(encoding="utf-8")
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
        sys.exit("Không tìm được điểm kết của khối `const vi` trong ngon-ngu/vi.ts")

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


def khoa_cua(ma: str) -> set[str]:
    """
    Bộ khoá lá của MỘT tệp ngôn ngữ, đọc qua Node.

    Vì sao không dùng regex như `khoa_da_dich`: cách xuống dòng khác nhau giữa các tệp làm
    regex đọc ra kết quả khác nhau cho **cùng một dữ liệu**. `vi.ts` viết
    `vaiTro: { GiaoVien: '…' }` trên một dòng nên regex không thấy khoá con, còn tệp sinh tự
    động thì xuống dòng nên regex thấy — báo "thừa khoá" trong khi hai bên giống hệt.

    Đọc bằng Node thì so đúng dữ liệu, không so cách trình bày.
    """
    tep = GOC / f"src/lib/ngon-ngu/{ma}.ts"
    if not tep.exists():
        return set()

    src = tep.read_text(encoding="utf-8")
    if f"const {ma} = {{" not in src:
        # Tệp tạm kiểu `const en = vi` — chưa dịch.
        return set()

    js = pathlib.Path("/tmp/_doc_ngonngu.mjs")
    js.write_text(
        "import { readFileSync } from 'node:fs'\n"
        "const [,, tep, ma] = process.argv\n"
        "const src = readFileSync(tep, 'utf8')\n"
        "const than = src.slice(src.indexOf(`const ${ma} = {`) + `const ${ma} = `.length,\n"
        "                       src.lastIndexOf(`export default ${ma}`)).trim()\n"
        "const o = eval('(' + than + ')')\n"
        "const ra = []\n"
        "const di = (n, p = []) => {\n"
        "  for (const [k, v] of Object.entries(n)) {\n"
        "    const key = [...p, k]\n"
        "    if (Array.isArray(v)) v.forEach((x, i) => ra.push([...key, i].join('.')))\n"
        "    else if (v && typeof v === 'object') di(v, key)\n"
        "    else ra.push(key.join('.'))\n"
        "  }\n"
        "}\n"
        "di(o)\n"
        "process.stdout.write(JSON.stringify(ra))\n"
    )
    kq = subprocess.run(["node", str(js), str(tep), ma], capture_output=True, text=True)
    if kq.returncode != 0:
        print(f"  ✗ {ma}.ts: không đọc được ({kq.stderr.strip()[:120]})")
        return set()
    return set(json.loads(kq.stdout))


def kiem_cheo_ngon_ngu(_bo_qua: set[str]) -> int:
    """
    So MỌI tệp ngôn ngữ với `vi.ts` — bắt khoá thiếu và khoá thừa.

    Vì sao cần: thiếu bản dịch ở một ngôn ngữ **không làm gì đỏ**. i18next lặng lẽ rơi về
    tiếng Việt, nên người dùng tiếng Nhật thấy một câu tiếng Việt lẫn giữa màn hình tiếng
    Nhật — và không ai báo lỗi vì trông như "chưa dịch xong", không như "hỏng".

    Khoá THỪA cũng báo: nó nghĩa là ai đó đổi tên khoá ở `vi.ts` mà quên đổi ở đây, nên bản
    dịch cũ thành mồ côi còn khoá mới thì rơi về tiếng Việt.
    """
    khoa_vi = khoa_cua("vi")
    ds = json.loads((GOC / "src/lib/ngon-ngu/danhSach.ts").read_text(encoding="utf-8")
                    .split("NGON_NGU: readonly NgonNgu[] = [")[1].split("]")[0]
                    .replace("ma:", '"ma":').replace("ten:", '"ten":').replace("co:", '"co":')
                    .replace("'", '"').replace(",\n]", "\n]").strip().rstrip(",")
                    .join(["[", "]"]))
    loi = 0
    for n in ds:
        ma = n["ma"]
        if ma == "vi":
            continue
        k = khoa_cua(ma)
        if not k:
            print(f"  ⚠ {ma}.ts: CHƯA dịch (đang dùng lại tiếng Việt)")
            continue
        thieu = khoa_vi - k
        thua = k - khoa_vi
        if thieu or thua:
            loi = 1
            print(f"  ✗ {ma}.ts: thiếu {len(thieu)} khoá, thừa {len(thua)} khoá")
            for x in sorted(thieu)[:5]:
                print(f"      thiếu: {x}")
            for x in sorted(thua)[:5]:
                print(f"      thừa:  {x}")
        else:
            print(f"  ✓ {ma}.ts: đủ {len(k)} khoá")
    return loi


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

    print(f"Mọi khoá i18n tĩnh đều có bản dịch ({len(khoa)} khoá trong vi.ts).")
    print("\nĐối chiếu các ngôn ngữ khác với vi.ts:")
    return kiem_cheo_ngon_ngu(khoa)


if __name__ == "__main__":
    sys.exit(main())
