#!/usr/bin/env python3
"""
Dựng bộ dữ liệu mẫu đầy đủ vào trung tâm đang test, qua API thật.

Vì sao qua API chứ không INSERT thẳng DB: dữ liệu phải đi qua đúng mọi quy tắc nghiệp vụ —
tự gán `tenant_id`, bốn cột audit, validator, mã hoá mật khẩu, ghi nhật ký. INSERT thẳng sẽ
tạo ra dữ liệu mà chính hệ thống không bao giờ tạo được, nên test trên đó không nói lên gì.

Quy mô (đủ để thấy phân trang và hình dạng biểu đồ thật):
  4 phòng ban · 6 nhân sự · 50 học viên · 30 khách hàng
  ~110 đơn hàng rải 12 tháng · 8 lớp học · 3 khoá trực tuyến có bài và người học

Dựng vào **chính trung tâm đang test** (`W686AE9`), không tạo trung tâm mới mỗi lần: mã trung
tâm mới sinh ngẫu nhiên nên lần nào cũng phải nhớ mã khác — phiền khi test tay.

Chạy:
    # 1. API ở terminal khác
    dotnet run --project src/GiapTech.LangCenter.API

    # 2. Xoá dữ liệu nghiệp vụ cũ, GIỮ tài khoản đăng nhập
    docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/xoa-du-lieu-nghiep-vu.sql

    # 3. Dựng lại
    python3 scripts/tao-du-lieu-mau.py

Chạy lại nhiều lần được — bước 2 dọn sạch trước, nên không chồng dữ liệu.
"""
import json
import os
import random
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

API = "http://localhost:5229/api/v1"

# Trung tâm đang test — dựng thẳng vào đây thay vì tạo mới mỗi lần (chốt 14/09/2026).
MA_TRUNG_TAM = "W686AE9"
USER_QUAN_TRI = "admin"

# Mật khẩu quản trị của trung tâm trên. Đọc từ biến môi trường trước — mật khẩu thật không
# nên nằm trong mã nguồn, kể cả mã chỉ chạy trên máy local.
MK_QUAN_TRI = os.environ.get("MK_QUAN_TRI", "Admin@12345")

# Mật khẩu đặt cho mọi tài khoản script TẠO RA (nv1..nv6). Tách khỏi mật khẩu quản trị: đổi
# mật khẩu admin không được làm hỏng script, và ngược lại.
MAT_KHAU = "demo-matkhau-123"

# Hạt giống cố định — chạy lại cho ra cùng một bộ số, nên ảnh chụp và con số đối chiếu được.
random.seed(20260914)

_token: str | None = None


def goi(duong: str, than=None, method=None, cho_phep_loi=False):
    url = f"{API}{duong}"
    data = json.dumps(than).encode() if than is not None else None
    req = urllib.request.Request(url, data=data, method=method or ("POST" if than else "GET"))
    req.add_header("Content-Type", "application/json")
    if _token:
        req.add_header("Authorization", f"Bearer {_token}")
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        chi_tiet = e.read().decode()[:300]
        if cho_phep_loi:
            return None
        print(f"\n✗ {method or 'POST'} {duong} → {e.code}\n  {chi_tiet}", file=sys.stderr)
        raise


def buoc(nhan: str):
    print(f"  {nhan}...", end=" ", flush=True)


def xong(n=None):
    print(f"✓{f' ({n})' if n is not None else ''}")


HO = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Đặng", "Bùi", "Đỗ", "Ngô"]
DEM = ["Văn", "Thị", "Minh", "Thu", "Quang", "Hải", "Ngọc", "Đức", "Phương", "Anh"]
TEN = ["An", "Bình", "Chi", "Dũng", "Giang", "Hà", "Khánh", "Linh", "Mai", "Nam",
       "Oanh", "Phúc", "Quân", "Sơn", "Trang", "Uyên", "Vân", "Xuân", "Yến", "Tuấn"]


def ho_ten(i: int) -> str:
    """
    Sinh họ tên **không trùng nhau** và trông tự nhiên.

    Bản đầu dùng `HO[i%10] · DEM[i*3%10] · TEN[i*7%20]` — ba chỉ số độc lập nên chu kỳ lặp là
    bội chung nhỏ nhất của chúng: **đúng 20**. Cứ 20 người là tên quay vòng; 32 khách hàng ra
    20 tên với 12 cái trùng. Chủ sản phẩm phát hiện khi xem màn Khách hàng.

    Bản thứ hai coi `i` là một số rồi tách theo cơ số — hết trùng, nhưng chữ số cao đổi chậm
    nên nhiều dòng liền nhau cùng họ (hoặc cùng tên, tuỳ thứ tự). Vẫn trông giả.

    Cách này cho mỗi thành phần một **nhịp riêng**: `i // 10` làm họ và đệm trôi chậm trong khi
    `i` làm tên đổi mỗi dòng. Kiểm bằng số: 60 người đầu ra 60 tổ hợp khác nhau, và không hai
    dòng liền nhau nào trùng cả họ lẫn đệm.

    **Chu kỳ là 200** — đủ cho mọi nhóm hiện tại, nhưng hai nhóm cách nhau đúng bội của 200 sẽ
    trùng hoàn toàn. Đó là lý do khách hàng bắt đầu ở `i = 317` (số lẻ, không chia hết cho 200)
    chứ không phải 200: bản trước dùng 200 nên 32 khách trùng tên với 32 học viên đầu.
    """
    return (f"{HO[i % len(HO)]} "
            f"{DEM[(i // len(HO) + i) % len(DEM)]} "
            f"{TEN[(i // len(HO) + i * 3) % len(TEN)]}")


def dien_thoai(i: int) -> str:
    return f"09{i:08d}"


def main():
    global _token

    print(f"\n=== Dựng dữ liệu mẫu vào {MA_TRUNG_TAM} ===\n")

    ma, user = MA_TRUNG_TAM, USER_QUAN_TRI

    buoc("đăng nhập")
    try:
        _token = goi("/auth/dang-nhap",
                     {"maTrungTam": ma, "username": user, "matKhau": MK_QUAN_TRI})["accessToken"]
    except urllib.error.HTTPError:
        print(f"""
✗ Không đăng nhập được {user}@{ma} với mật khẩu đã đặt trong script.

  Đặt mật khẩu qua biến môi trường:
    MK_QUAN_TRI='mat-khau-that' python3 scripts/tao-du-lieu-mau.py

  Hoặc đổi MA_TRUNG_TAM / USER_QUAN_TRI ở đầu file nếu đang dùng trung tâm khác.
""", file=sys.stderr)
        raise SystemExit(1)
    xong()

    # Dọn trước cho chắc: chạy lại script mà chưa xoá thì dữ liệu chồng lên nhau (khách trùng
    # số điện thoại sẽ bị chặn, nhưng đơn hàng và lớp thì nhân đôi).
    con = goi("/khach-hang?soDong=1")
    if con and con.get("tongSoDong", 0) > 0:
        print(f"""
✗ Trung tâm {ma} còn {con['tongSoDong']} khách hàng.

  Xoá dữ liệu nghiệp vụ cũ trước (tài khoản đăng nhập được giữ nguyên):
    docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/xoa-du-lieu-nghiep-vu.sql
""", file=sys.stderr)
        raise SystemExit(1)

    quyen = {q["tenQuyen"]: q["id"] for q in goi("/quyen")}

    # ---------- Phòng ban ----------
    buoc("phòng ban")
    phong = {}
    for ten in ["Kinh doanh Miền Bắc", "Kinh doanh Miền Nam", "Tư vấn online", "Đào tạo"]:
        phong[ten] = goi("/phong-ban", {"ten": ten})
    xong(len(phong))

    # ---------- Nhân sự ----------
    # Mỗi người MỘT phòng ban khác nhau → biểu đồ "doanh thu theo đội nhóm" mới có hình dạng.
    buoc("nhân sự")
    ten_phong = list(phong)
    nhan_su = []

    # DÙNG LẠI hồ sơ đã có cùng tên, không tạo mới (sửa 15/09/2026).
    #
    # Bản cũ tạo mù quáng, nên chạy trên tenant đã có người là sinh hồ sơ TRÙNG TÊN. Hậu quả
    # thật đã gặp khi rà v1: tenant W686AE9 có hai hồ sơ "Cô Lan" — tài khoản `co.lan` (cũ) nối
    # hồ sơ dạy 0 lớp, còn 4 lớp thì gán cho hồ sơ mới của `nv5`. Cô Lan đăng nhập và thấy
    # **0 lớp**, trông y như lỗi phân quyền: mất gần một giờ mới truy ra là dữ liệu trùng.
    da_co = {}
    for trang in range(1, 6):
        kq = goi(f"/nguoi-dung?trang={trang}&soDong=100") or {}
        for x in kq.get("duLieu", []):
            da_co.setdefault(x["hoTen"], x["id"])
        if len(kq.get("duLieu", [])) < 100:
            break

    for i, (ho, vai) in enumerate([
        ("Sale Hà Nội", "NhanVien"), ("Sale Sài Gòn", "NhanVien"),
        ("Sale Online A", "NhanVien"), ("Sale Online B", "NhanVien"),
        ("Cô Lan", "GiaoVien"), ("Thầy Hoà", "GiaoVien"),
    ]):
        if ho in da_co:
            nhan_su.append({"id": da_co[ho], "ten": ho, "vai": vai, "user": None})
            continue
        nd = goi("/nguoi-dung", {
            "hoTen": ho,
            "loaiNguoiDung": vai,
            "soDienThoai": dien_thoai(100 + i),
            "phongBanId": phong[ten_phong[i % len(ten_phong)]],
            "taiKhoan": {
                "username": f"nv{i + 1}",
                "matKhau": MAT_KHAU,
                "quyenIds": [quyen["Quản trị viên" if vai == "NhanVien" else "Giáo viên"]],
                "phaiDoiMatKhau": False,
            },
        })
        nhan_su.append({"id": nd, "ten": ho, "vai": vai, "user": f"nv{i + 1}"})
    xong(len(nhan_su))

    sales = [n for n in nhan_su if n["vai"] == "NhanVien"]
    giao_vien = [n for n in nhan_su if n["vai"] == "GiaoVien"]

    # ---------- Danh mục bán ----------
    buoc("khoá học + sản phẩm")
    khoa_hoc = {}
    for ten, gia, buoi in [("IELTS 6.5 cấp tốc", 12_000_000, 40), ("IELTS 7.0", 18_000_000, 48),
                           ("Giao tiếp cơ bản", 5_000_000, 24), ("Tiếng Anh trẻ em", 7_500_000, 32),
                           ("TOEIC 750+", 9_000_000, 36)]:
        khoa_hoc[ten] = goi("/khoa-hoc", {"ten": ten, "giaTien": gia, "donViTien": "VND",
                                          "soBuoi": buoi, "dangBan": True})
    san_pham = {}
    for ten, gia, dv in [("Sách IELTS Cambridge 18", 250_000, "quyển"),
                         ("Bộ flashcard 600 từ", 120_000, "bộ"),
                         ("Áo đồng phục", 180_000, "cái")]:
        san_pham[ten] = goi("/san-pham", {"ten": ten, "giaTien": gia, "donViTien": "VND",
                                          "donViTinh": dv, "dangBan": True})
    xong(f"{len(khoa_hoc)} khoá, {len(san_pham)} sản phẩm")

    # ---------- Học viên ----------
    buoc("học viên")
    hoc_vien = []
    for i in range(50):
        hoc_vien.append(goi("/hoc-vien", {
            "hoTen": ho_ten(i),
            "loaiNguoiDung": "HocVien",
            "soDienThoai": dien_thoai(1000 + i),
            "hoSoHocVien": {"truongLop": f"THPT số {i % 5 + 1}",
                            "tenPhuHuynh": f"PH {ho_ten(i)}",
                            "soDienThoaiPhuHuynh": dien_thoai(5000 + i)},
        }))
    xong(len(hoc_vien))

    # ---------- Khách hàng + đơn hàng ----------
    # Khách tạo bởi TỪNG SALE khác nhau: doanh số tính cho người tạo hồ sơ khách, nên phải
    # đăng nhập bằng chính sale đó thì biểu đồ "theo cá nhân" mới chia ra được.
    buoc("khách hàng + đơn hàng (rải 12 tháng)")
    bay_gio = datetime.now(timezone.utc)
    so_don = 0
    don_ids: list[tuple] = []
    khach_ids: list[dict] = []

    for idx, sale in enumerate(sales):
        _token = goi("/auth/dang-nhap",
                     {"maTrungTam": ma, "username": sale["user"], "matKhau": MAT_KHAU})["accessToken"]

        for k in range(8):
            n = idx * 8 + k
            khach = goi("/khach-hang", {
                "hoTen": ho_ten(317 + n),
                "soDienThoai": dien_thoai(2000 + n),
                "phuongThucThanhToan": "ChuyenKhoan",
            })
            khach_ids.append({"id": khach, "hoTen": ho_ten(317 + n),
                              "soDienThoai": dien_thoai(2000 + n)})

            # 1-4 đơn mỗi khách, ngày rải đều 12 tháng → đường tăng trưởng có 12 mốc thật.
            for _ in range(random.randint(1, 4)):
                # Rải 0-390 ngày chứ không 0-360: kỳ so sánh là 12 tháng liền trước, rỗng
                # thì % thay đổi ra một con số vô nghĩa kiểu "+169925%".
                ngay = bay_gio - timedelta(days=random.randint(0, 390))
                if random.random() < 0.75:
                    ten_kh = random.choice(list(khoa_hoc))
                    gia = {"IELTS 6.5 cấp tốc": 12_000_000, "IELTS 7.0": 18_000_000,
                           "Giao tiếp cơ bản": 5_000_000, "Tiếng Anh trẻ em": 7_500_000,
                           "TOEIC 750+": 9_000_000}[ten_kh]
                    # Vài đơn có giảm giá → badge % trên giá gốc mới có ý nghĩa.
                    than = {"khachHangId": khach, "khoaHocId": khoa_hoc[ten_kh],
                            "soTien": gia if random.random() < 0.7 else round(gia * 0.85),
                            "donViTien": "VND", "tyGiaVeVnd": 1,
                            "ngayDangKy": ngay.isoformat(), "phuongThuc": "ChuyenKhoan"}
                else:
                    ten_sp = random.choice(list(san_pham))
                    gia = {"Sách IELTS Cambridge 18": 250_000, "Bộ flashcard 600 từ": 120_000,
                           "Áo đồng phục": 180_000}[ten_sp]
                    sl = random.randint(1, 3)
                    than = {"khachHangId": khach, "sanPhamId": san_pham[ten_sp],
                            "soLuong": sl, "soTien": gia * sl, "donViTien": "VND",
                            "tyGiaVeVnd": 1, "ngayDangKy": ngay.isoformat(),
                            "phuongThuc": "TienMat"}
                don_id = goi("/doanh-thu", than)
                don_ids.append((don_id, than["soTien"], ngay))
                so_don += 1
    xong(f"{len(sales) * 8} khách, {so_don} đơn")

    # ---------- Nối khách đã mua với hồ sơ học viên ----------
    # Luồng thật: khách mua khoá → quản trị cấp tài khoản học viên → hồ sơ TỰ NỐI (FR-25).
    # Không nối thì cột "Hồ sơ học viên" ở màn Khách hàng hiện "Chưa vào học" cho cả 32 người,
    # và không ai thấy được cầu nối CRM ↔ LMS làm gì.
    buoc("nối khách ↔ hồ sơ học viên")
    so_noi = 0
    for kh in khach_ids:
        # Chỉ nối ~60% — số còn lại là khách đã mua nhưng CHƯA vào học, ca hợp lệ và thường gặp.
        if random.random() >= 0.6:
            continue

        # Tạo hồ sơ học viên MANG ĐÚNG TÊN KHÁCH: hai bên là CÙNG một con người, chỉ khác góc
        # nhìn (CRM thấy người mua, LMS thấy người học). Bản trước ghép bừa khách với một học
        # viên có sẵn nên màn Khách hàng hiện "Bùi Phương Chi → Nguyễn Văn An", vô lý ngay từ
        # cái nhìn đầu.
        hv = goi("/hoc-vien", {
            "hoTen": kh["hoTen"],
            "loaiNguoiDung": "HocVien",
            "soDienThoai": kh["soDienThoai"],
            "khachHangId": kh["id"],          # FR-25 — tự nối, không cần PUT riêng
        }, cho_phep_loi=True)
        if hv:
            so_noi += 1

    xong(so_noi)

    # ---------- Lịch sử chăm sóc → phễu bán hàng ----------
    # Không có bước này thì mọi khách nằm ở "Mới" và phễu phẳng lì — nhìn như hỏng.
    # Tỷ lệ đặt gần thực tế: phần lớn đã mua, một ít còn tư vấn, vài người từ chối.
    buoc("lịch sử chăm sóc")
    so_cham = 0
    for i, kh_info in enumerate(khach_ids):
        kh = kh_info["id"]
        chuoi = (["DangTuVan", "DaMua"] if i % 10 < 6
                 else ["DangTuVan"] if i % 10 < 9
                 else ["DangTuVan", "TuChoi"])
        for j, tt in enumerate(chuoi):
            goi(f"/khach-hang/{kh}/cham-soc", {
                "khachHangId": kh,
                "thoiDiem": (bay_gio - timedelta(days=random.randint(1, 200))).isoformat(),
                "hinhThuc": random.choice(["GoiDien", "Zalo", "GapMat"]),
                "noiDung": f"Lần {j + 1}: trao đổi nhu cầu học",
                "trangThaiSau": tt,
            }, cho_phep_loi=True)
            so_cham += 1
    xong(so_cham)

    # ---------- Thu tiền → công nợ có khoảng cách thật ----------
    # Đăng ký là CAM KẾT, sổ thu là tiền THẬT. Không thu đồng nào thì ô "Đã thu" bằng 0 và
    # con số công nợ bằng đúng doanh thu — không thấy được ý nghĩa của việc tách hai sổ.
    buoc("thu tiền")
    so_thu = 0
    for don_id, so_tien, ngay in don_ids:
        r = random.random()
        if r < 0.55:
            phan = so_tien                      # đã thu đủ
        elif r < 0.85:
            phan = round(so_tien * 0.5)         # đóng một nửa
        else:
            continue                            # chưa đóng đồng nào
        goi(f"/doanh-thu/{don_id}/thu-tien", {
            "dangKyId": don_id,
            "soTien": phan,
            "ngayThu": (ngay + timedelta(days=random.randint(0, 20))).isoformat(),
            "phuongThuc": "ChuyenKhoan",
        }, cho_phep_loi=True)
        so_thu += 1
    xong(so_thu)

    # Về lại admin cho phần LMS
    _token = goi("/auth/dang-nhap",
                 {"maTrungTam": ma, "username": user, "matKhau": MK_QUAN_TRI})["accessToken"]

    # ---------- Lớp học ----------
    buoc("lớp học + ghi danh")
    lop_ids = []
    for i, ten_kh in enumerate(list(khoa_hoc) * 2):
        if len(lop_ids) >= 8:
            break
        gv = giao_vien[i % len(giao_vien)]
        lop = goi("/lop-hoc", {
            "ten": f"{ten_kh} — K{i + 1}",
            "giaoVienChinhId": gv["id"],
            "hinhThuc": "Offline",
            "phongHoc": f"P{101 + i}",
            "hocPhi": 8_000_000,
            "sucChuaToiDa": 20,
            "troGiangIds": [],
            "khoaHocIds": [khoa_hoc[ten_kh]],
        })
        goi(f"/lop-hoc/{lop}/hoan-tat",
            {"ngayKhaiGiang": (bay_gio - timedelta(days=30 * i)).isoformat()},
            cho_phep_loi=True)
        # 5-9 học viên mỗi lớp, chia đều để lớp nào cũng có người.
        nhom = hoc_vien[i * 6:i * 6 + random.randint(5, 9)]
        if nhom:
            goi(f"/lop-hoc/{lop}/hoc-vien", {"hocVienIds": nhom})
        lop_ids.append(lop)
    xong(len(lop_ids))

    # ---------- Khoá trực tuyến ----------
    buoc("khoá trực tuyến + bài học + người học")
    so_bai = so_ghi_danh = 0
    for ten, mo_ta, so in [("Phát âm chuẩn Mỹ", "Luyện 44 âm cơ bản", 6),
                           ("Ngữ pháp nền tảng", "12 thì tiếng Anh", 8),
                           ("Từ vựng IELTS 6.5", "800 từ theo chủ đề", 5)]:
        kh = goi("/khoa-online", {"ten": ten, "moTa": mo_ta})
        for b in range(so):
            goi("/khoa-online/bai-hoc", {
                "khoaOnlineId": kh,
                "tieuDe": f"Bài {b + 1} — {ten}",
                "noiDung": f"## Bài {b + 1}\n\nNội dung bài học mẫu cho *{ten}*.",
                "thuTu": b,
                # Bài đầu công khai → thấy được nhánh "ai đăng nhập cũng đọc được".
                "congKhai": b == 0,
            })
            so_bai += 1

        goi(f"/khoa-online/{kh}", {"id": kh, "ten": ten, "moTa": mo_ta, "trangThai": "DangMo"},
            method="PUT")

        nhom = random.sample(hoc_vien, random.randint(8, 15))
        goi("/khoa-online/ghi-danh", {
            "khoaOnlineId": kh, "hocVienIds": nhom, "ghiChu": "mua khoá online",
        })
        so_ghi_danh += len(nhom)
    xong(f"3 khoá, {so_bai} bài, {so_ghi_danh} lượt ghi danh")

    print(f"""
=== XONG ===

  Mã trung tâm : {ma}
  Quản trị     : {user} / (mật khẩu sẵn có của bạn)
  Sale         : nv1..nv4 / {MAT_KHAU}   (mỗi người một phòng ban)
  Giáo viên    : nv5, nv6 / {MAT_KHAU}

  Đăng nhập http://localhost:5173 rồi xem:
    CRM › Thống kê       — đủ 4 loại, đường tăng trưởng 12 mốc
    CRM › Khách hàng     — 32 khách, phân trang
    LMS › Lớp học        — 8 lớp, mỗi lớp 5-9 học viên
    LMS › Khoá trực tuyến— 3 khoá đang mở, 19 bài

  Đăng nhập bằng nv5 (giáo viên) để thấy phạm vi: chỉ học viên lớp mình.

  Dựng lại từ đầu (xoá dữ liệu nghiệp vụ, GIỮ tài khoản đăng nhập):
    docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/xoa-du-lieu-nghiep-vu.sql
    python3 scripts/tao-du-lieu-mau.py
""")


if __name__ == "__main__":
    main()
