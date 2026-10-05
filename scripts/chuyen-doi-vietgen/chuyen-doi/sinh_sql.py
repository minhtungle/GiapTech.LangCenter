#!/usr/bin/env python3
"""
Sinh script chuyển dữ liệu VIETGEN (hệ cũ, export JSON) → GiapTech.LangCenter.

Tuân thủ tuyệt đối LangCenter:
  - Đúng tên bảng/cột/kiểu theo AppDbContextModelSnapshot (migration 20261005133522_ThemDuoiTenDangNhap).
  - Chỉ INSERT vào MỘT tenant mới tạo, kiểm tenant còn "trắng" trước khi chạy (quy tắc #1).
  - Mọi hàng mang tenant_id (quy tắc #2).
  - Quyền chỉ dùng cặp (chức năng, thao tác) có trong ChucNang.ThaoTacTheoChucNang (quy tắc #9).
  - Ràng buộc UNIQUE/CHECK/độ dài cột của LangCenter được ép ngay khi sinh, vi phạm thì sửa
    theo luật đã chốt và ghi vào báo cáo làm sạch.
  - Không ghi hash mật khẩu tay: password_hash là chuỗi giữ chỗ không hợp lệ (đăng nhập bị từ
    chối), mật khẩu thật đặt qua API POST /tai-khoan/{id}/dat-lai-mat-khau.

Chỉ lấy đơn vị thật VIETGEN Academy (F4B89D3A-…); bỏ toàn bộ tenant LOCALHOST.

Dùng:  python3 sinh_sql.py            (mặc định đọc ../du-lieu, ghi vào chính thư mục này)
      python3 sinh_sql.py <thư mục JSON> <thư mục xuất>
"""
import csv
import json
import os
import re
import sys
import unicodedata
import uuid
from collections import Counter, defaultdict
from decimal import Decimal

_DAY = os.path.dirname(os.path.abspath(__file__))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(_DAY, '..', 'du-lieu')
OUT = sys.argv[2] if len(sys.argv) > 2 else _DAY
os.makedirs(OUT, exist_ok=True)

DON_VI_THAT = 'F4B89D3A-5246-4D90-83C2-2DB9C3E4D9B7'   # VIETGEN Academy
GUID_RONG = '00000000-0000-0000-0000-000000000000'
MUI_GIO = '+07'                                         # dữ liệu cũ lưu giờ VN, không múi giờ
NGAY_MAC_DINH = '2024-08-01 00:00:00'                   # khi bản ghi cũ thiếu NgayTao
HASH_GIU_CHO = 'CHUYEN-DOI:CHUA-DAT-MAT-KHAU'           # không phải base64 PBKDF2 → KiemTra = false
NS = uuid.UUID('6f1d7a52-3b0e-4c55-9a8e-5f2c1d0b9e11')  # sinh id ổn định (chạy lại ra cùng id)

# ---------- Danh mục quyền của LangCenter (chép từ Domain/Common/ChucNang.cs) ----------
X, T, S, XO = 0, 1, 2, 3
DUYET, HUY, SINH_LICH, THU_TIEN, HOAN_TAT, GUI_XEP_LOP = 10, 12, 13, 14, 21, 23
CRUD = [X, T, S, XO]
THAO_TAC_HOP_LE = {
    'KhachHang': CRUD, 'ChamSocKhachHang': CRUD, 'KhoaHoc': CRUD, 'SanPham': CRUD,
    'DoanhThu': [X, T, S, XO, THU_TIEN, GUI_XEP_LOP], 'ThongKeDoanhThu': [X],
    'TaiKhoan': CRUD, 'HoSoNguoiDung': CRUD, 'NhatKyHeThong': [X],
    'LopHoc': [X, T, S, XO, HOAN_TAT, HUY, SINH_LICH], 'GhiDanhLop': [X, T, XO],
    'XepLop': [X, DUYET], 'BuoiHoc': [X, T, S, XO, HUY], 'LopHocToanTrungTam': [X, S],
}

NVKD = {'KhachHang': [X, T], 'ChamSocKhachHang': [X, T, S],
        'DoanhThu': [X, T, THU_TIEN, GUI_XEP_LOP], 'KhoaHoc': [X], 'SanPham': [X], 'LopHoc': [X]}
DPL = {'KhoaHoc': [X, T, S], 'LopHoc': [X, T, S, SINH_LICH], 'GhiDanhLop': [X, T, XO],
       'XepLop': [X, DUYET], 'BuoiHoc': [X, T, S], 'LopHocToanTrungTam': [X, S],
       'NhatKyHeThong': [X]}


def gop(*ms):
    kq = defaultdict(set)
    for m in ms:
        for cn, hds in m.items():
            kq[cn] |= set(hds)
    return {cn: sorted(v) for cn, v in kq.items()}


NHOM_MOI = {   # ten_quyen → (mô tả, ma trận)
    'Ban giám đốc': ('Chuyển từ vai trò "Tổng giám đốc" (hệ cũ)', {
        'KhachHang': CRUD, 'ChamSocKhachHang': [X, T, S], 'DoanhThu': [X, T, S],
        'ThongKeDoanhThu': [X], 'KhoaHoc': [X], 'SanPham': [X]}),
    'Nhân viên kinh doanh': ('Chuyển từ vai trò "Nhân viên kinh doanh" (hệ cũ)', NVKD),
    'Nhân viên kinh doanh - master': ('Chuyển từ vai trò "Nhân viên kinh doanh - master" (hệ cũ)',
                                      gop(NVKD, {'KhachHang': [S, XO], 'DoanhThu': [S]})),
    'Trưởng phòng kinh doanh': ('Chuyển từ vai trò "Trưởng phòng kinh doanh" (hệ cũ)',
                                gop(NVKD, {'ThongKeDoanhThu': [X], 'TaiKhoan': [X],
                                           'HoSoNguoiDung': [X]})),
    'Điều phối lớp': ('Chuyển từ vai trò "Điều phối lớp" (hệ cũ)', DPL),
    'Điều phối lớp - master': ('Chuyển từ vai trò "Điều phối lớp - master" (hệ cũ)',
                               gop(DPL, {'KhoaHoc': [XO], 'LopHoc': [XO, HUY, HOAN_TAT],
                                         'BuoiHoc': [XO, HUY]})),
}
for ten, (_, mt) in NHOM_MOI.items():
    for cn, hds in mt.items():
        sai = set(hds) - set(THAO_TAC_HOP_LE[cn])
        assert not sai, f'{ten}: {cn} có thao tác không khai trong LangCenter: {sai}'

# Vai trò cũ → (nhóm quyền LangCenter, loai_nguoi_dung). Nhóm có dấu * là nhóm seed sẵn.
VAI_TRO = {
    'SUPER ADMIN': ('*Quản trị viên', 0),
    'Tổng giám đốc': ('Ban giám đốc', 0),
    'Nhân viên kinh doanh': ('Nhân viên kinh doanh', 4),
    'Nhân viên kinh doanh - master': ('Nhân viên kinh doanh - master', 4),
    'Trưởng phòng kinh doanh': ('Trưởng phòng kinh doanh', 4),
    'Giáo viên': ('*Giáo viên', 1),
    'Điều phối lớp': ('Điều phối lớp', 0),
    'Điều phối lớp - master': ('Điều phối lớp - master', 0),
}
VAI_TRO_BO = {'Khách'}   # tài khoản dùng thử, không thao tác nào, không tham chiếu nào

# Phương thức thanh toán cũ → enum PhuongThucThanhToan (0 TienMat, 1 ChuyenKhoan, 2 Khac)
PTTT = {'Tiền mặt': 0, 'Chưa đóng': 2}  # còn lại (ngân hàng, Paypal, Remitly…) → 1

# ---------- Đọc dữ liệu ----------
def doc(ten):
    with open(os.path.join(SRC, ten + '.json'), encoding='utf-8-sig') as f:
        return json.load(f)


def that(rows):
    return [r for r in rows if (r.get('MaDonViSuDung') or '').upper() == DON_VI_THAT]


bao_cao = []   # (bảng, id cũ, vấn đề, xử lý)


def ghi(bang, id_cu, van_de, xu_ly):
    bao_cao.append((bang, id_cu, van_de, xu_ly))


def g(v):
    """GUID → chuỗi thường; GUID rỗng/None → None."""
    if not v:
        return None
    v = str(v).strip().lower()
    return None if v == GUID_RONG else v


def chuoi(v, max_len=None, bang=None, id_cu=None, cot=None):
    """LangCenter: Trim, rỗng → NULL. Cắt theo HasMaxLength (có ghi báo cáo)."""
    if v is None:
        return None
    v = str(v).strip()
    if not v:
        return None
    if max_len and len(v) > max_len:
        ghi(bang, id_cu, f'{cot} dài {len(v)} > {max_len}', f'cắt còn {max_len} ký tự')
        v = v[:max_len - 1] + '…'
    return v


def ngay(v):
    if not v:
        return None
    return str(v)[:23] + MUI_GIO


def stable_id(*parts):
    return str(uuid.uuid5(NS, '|'.join(parts)))


# ---------- SQL helpers ----------
def q(v, kieu=None):
    if v is None:
        return 'NULL' + (f'::{kieu}' if kieu else '')
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, (int, Decimal)):
        return str(v) + (f'::{kieu}' if kieu else '')
    s = "'" + str(v).replace("'", "''") + "'"
    return s + (f'::{kieu}' if kieu else '')


def insert(bang, cot_kieu, dong, ghi_chu=''):
    """INSERT … SELECT v.*, tenant FROM (VALUES …) — tenant_id lấy từ bảng tạm _ctx."""
    if not dong:
        return f'-- {bang}: không có dòng nào\n'
    cots = [c for c, _ in cot_kieu]
    vals = []
    for d in dong:
        vals.append('  (' + ', '.join(q(d.get(c), k) for c, k in cot_kieu) + ')')
    return (f'-- {bang}: {len(dong)} dòng {ghi_chu}\n'
            f'INSERT INTO "{bang}" ({", ".join(cots)}, tenant_id)\n'
            f'SELECT v.*, c.tenant_id FROM (VALUES\n' + ',\n'.join(vals) +
            f'\n) AS v({", ".join(cots)}) CROSS JOIN _ctx c;\n\n')


AUDIT = [('created_at', 'timestamptz'), ('updated_at', 'timestamptz')]

# =====================================================================
# 1. PHONG_BAN ← tbCoCauToChuc
# =====================================================================
co_cau = [r for r in that(doc('tbCoCauToChuc')) if r['TrangThai'] == 1]
pb_ids = {g(r['IdCoCauToChuc']) for r in co_cau}
for r in that(doc('tbCoCauToChuc')):
    if r['TrangThai'] != 1:
        ghi('PHONG_BAN', r['IdCoCauToChuc'], 'đơn vị đã xoá mềm (TrangThai=0)', 'không chuyển')


def tag_vai_tro(ten):
    t = ten.lower()
    if 'kinh doanh' in t or t.startswith('leader'):
        return 0          # KinhDoanh
    if t in ('tiếng anh', 'tiếng đức'):
        return 1          # GiaoVien
    return None


phong_ban = []
for i, r in enumerate(sorted(co_cau, key=lambda r: (r['CapDo'], r['Stt']))):
    cha = g(r['IdCha'])
    if cha and cha not in pb_ids:
        ghi('PHONG_BAN', r['IdCoCauToChuc'], 'phòng cha không chuyển', 'đưa lên làm gốc')
        cha = None
    ql = [g(x) for x in (r['IdQuanLy'] or '').split(',') if g(x)]
    if len(ql) > 1:
        ghi('PHONG_BAN', r['IdCoCauToChuc'], f'{len(ql)} người quản lý', 'LangCenter chỉ 1 người: lấy người đầu')
    phong_ban.append(dict(
        id=g(r['IdCoCauToChuc']), ten=chuoi(r['TenCoCauToChuc'], 200), phong_ban_cha_id=cha,
        thu_tu=i + 1, tag_vai_tro=tag_vai_tro(r['TenCoCauToChuc'].strip()),
        mo_ta='Chuyển từ hệ cũ', created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH),
        updated_at=ngay(r['NgaySua']),
        _ql=ql[0] if ql else None, _tao=g(r['IdNguoiTao']), _sua=g(r['IdNguoiSua'])))
ten_pb = Counter((p['phong_ban_cha_id'], p['ten'].lower()) for p in phong_ban)
assert all(v == 1 for v in ten_pb.values()), 'Trùng tên phòng ban trong cùng phòng cha'
# cha phải nạp trước con
phong_ban.sort(key=lambda p: p['phong_ban_cha_id'] is not None)

# =====================================================================
# 2. NGUOI_DUNG + TAI_KHOAN ← tbNguoiDung
# =====================================================================
kieu = {r['IdKieuNguoiDung']: r['TenKieuNguoiDung'] for r in doc('tbKieuNguoiDung')}
nguoi, tai_khoan, ho_so_gv, mxh, gan_quyen = [], [], [], [], []
nd_ids = set()
# Validator LangCenter (TaoTaiKhoanValidator): phần tên + đuôi @tên-miền tuỳ chọn. Đăng nhập so
# khớp CHÍNH XÁC chuỗi lưu (phân biệt hoa/thường).
USERNAME_HOP_LE = re.compile(r'^[a-zA-Z0-9._-]+(@[a-zA-Z0-9.-]+)?$')
PHAN_TEN_HOP_LE = re.compile(r'^[a-zA-Z0-9._-]+$')
username_da_dung = {'admin'}   # admin do seeder tạo

# Đuôi tên đăng nhập MỚI (chủ sản phẩm chốt 06/10/2026). Hệ cũ dùng @vietgenacademy.edu.vn cho
# cả 174 tài khoản; trung tâm đổi sang tên miền này, nên MỌI tài khoản chuyển sang đều mang đuôi
# mới. Phần tên giữ nguyên như cũ.
#
# Hệ quả phải báo cho người dùng: họ đăng nhập bằng đuôi MỚI, khác email họ quen ở hệ cũ.
DUOI_MOI = '@vietgeneducation.edu.vn'


def ten_khong_dau(s):
    """Họ tên → chữ thường không dấu, bỏ mọi ký tự không phải chữ cái.

    Dùng khi username cũ hỏng mã hoá nên không lấy lại được phần tên. Quy tắc này là quy tắc
    sẵn có của hệ cũ: 137/174 username đang đúng dạng `hotenvietthuongkhongdau`.
    """
    b = unicodedata.normalize('NFD', s or '').replace('đ', 'd').replace('Đ', 'D')
    b = ''.join(c for c in b if unicodedata.category(c) != 'Mn')
    return re.sub(r'[^A-Za-z]', '', b).lower()

for r in sorted(that(doc('tbNguoiDung')), key=lambda r: r['NgayTao'] or ''):
    vt = kieu[r['IdKieuNguoiDung']]
    if vt in VAI_TRO_BO:
        ghi('NGUOI_DUNG', r['IdNguoiDung'], f'vai trò "{vt}" (tài khoản dùng thử)', 'không chuyển')
        continue
    nhom, loai = VAI_TRO[vt]
    nid = g(r['IdNguoiDung'])
    nd_ids.add(nid)

    # --- username: giữ PHẦN TÊN cũ, thay đuôi bằng DUOI_MOI ---
    cu = r['TenDangNhap'].strip()
    phan_ten = cu.split('@', 1)[0].strip()
    if not PHAN_TEN_HOP_LE.match(phan_ten or '-?'):
        # Hỏng mã hoá (có khoảng trắng, dấu '?'): sinh lại từ HỌ TÊN theo quy tắc của hệ cũ.
        # Không lấy từ Email: email có thể trống hoặc là địa chỉ cá nhân (gmail), không theo
        # quy tắc nào — và lấy nhầm sẽ ra username không ai đoán được.
        phan_ten = ten_khong_dau(r['TenNguoiDung'])
        ghi('TAI_KHOAN', r['IdNguoiDung'], f'username "{cu}" không qua validator LangCenter',
            f'sinh lại từ họ tên: "{phan_ten}{DUOI_MOI}"')
    un = (phan_ten + DUOI_MOI)[:100]
    if cu.split('@', 1)[-1].lower() != DUOI_MOI[1:].lower():
        ghi('TAI_KHOAN', r['IdNguoiDung'], f'đuôi cũ "@{cu.split("@", 1)[-1]}"',
            f'đổi sang "{DUOI_MOI}" — chủ sản phẩm chốt 06/10/2026')
    goc, k = un, 2
    while un.lower() in {x.lower() for x in username_da_dung}:
        ten, _, duoi = goc.partition('@')
        un = f'{ten}{k}' + (f'@{duoi}' if duoi else ''); k += 1
    if un != goc:
        ghi('TAI_KHOAN', r['IdNguoiDung'], f'username "{goc}" bị trùng', f'đổi thành "{un}"')
    username_da_dung.add(un)

    # --- ngày sinh ---
    ns = r['NgaySinh']
    if ns and ns.startswith('1900-01-01'):
        ghi('NGUOI_DUNG', r['IdNguoiDung'], 'ngày sinh 1900-01-01 (giá trị giả)', 'để NULL')
        ns = None

    # --- STK ngân hàng: text tự do → so_tai_khoan (50) + ten_ngan_hang (100) ---
    stk_goc = chuoi(r['SoTaiKhoanNganHang'])
    so_tk = ten_nh = None
    if stk_goc:
        m = re.search(r'\d[\d\s.\-]{5,}\d', stk_goc)
        if m:
            so_tk = re.sub(r'[\s.\-]', '', m.group(0))[:50]
            ten_nh = re.sub(r'^[\s\-/,:|]+|[\s\-/,:|]+$', '', (stk_goc[:m.start()] + ' ' + stk_goc[m.end():]).strip()) or None
            if ten_nh:
                ten_nh = re.sub(r'(\s*[-/|,:]\s*)+', ' - ', ten_nh).strip(' -')[:100] or None
        else:
            ten_nh = None
        if not m or len(stk_goc) > 50:
            ghi('NGUOI_DUNG', r['IdNguoiDung'], f'STK tự do "{stk_goc}"',
                f'so_tai_khoan="{so_tk}", ten_ngan_hang="{ten_nh}"; bản gốc ghi vào ghi_chu — KIỂM TRA LẠI')

    gc_nd = [x for x in [chuoi(r['GhiChu'])] if x]
    if stk_goc and (not so_tk or len(stk_goc) > 50):
        gc_nd.append(f'STK ngân hàng (hệ cũ): {stk_goc}')   # không mất thông tin gốc
    ghi_chu = chuoi('\n'.join(gc_nd), 1000, 'NGUOI_DUNG', r['IdNguoiDung'], 'ghi_chu')
    pb = g(r['IdCoCauToChuc'])
    if pb and pb not in pb_ids:
        ghi('NGUOI_DUNG', r['IdNguoiDung'], 'phòng ban không chuyển', 'phong_ban_id NULL')
        pb = None

    nguoi.append(dict(
        id=nid, ho_ten=chuoi(r['TenNguoiDung'], 200), email=chuoi(r['Email'], 256),
        so_dien_thoai=chuoi(r['SoDienThoai'], 20, 'NGUOI_DUNG', r['IdNguoiDung'], 'so_dien_thoai'),
        ngay_sinh=ngay(ns), loai_nguoi_dung=loai, trang_thai_nhan_su=0, phong_ban_id=pb,
        so_tai_khoan=so_tk, ten_ngan_hang=ten_nh, ghi_chu=ghi_chu,
        created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH), updated_at=ngay(r['NgaySua']),
        _tao=g(r['IdNguoiTao']), _sua=g(r['IdNguoiSua'])))
    if not r['NgayTao']:
        ghi('NGUOI_DUNG', r['IdNguoiDung'], 'thiếu NgayTao', f'created_at = {NGAY_MAC_DINH}')

    if loai == 1:
        ho_so_gv.append(dict(id=stable_id('hsgv', nid), nguoi_dung_id=nid,
                             created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH), updated_at=None))
    link = chuoi(r['LinkLienHe'], 500)
    if link:
        mxh.append(dict(id=stable_id('mxh', nid), nguoi_dung_id=nid,
                        loai=0 if 'facebook' in link.lower() or 'fb.com' in link.lower() else 4,
                        duong_dan=link, ghi_chu=None,
                        created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH), updated_at=None))

    khoa = r['KichHoat'] != 1 or r['TrangThai'] != 1
    tkid = stable_id('tk', nid)
    tai_khoan.append(dict(
        id=tkid, nguoi_dung_id=nid, username=un, password_hash=HASH_GIU_CHO,
        phai_doi_mat_khau=True, trang_thai=1 if khoa else 0,
        created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH), updated_at=ngay(r['NgaySua']),
        _ten_cu=cu, _ho_ten=r['TenNguoiDung'], _vai_tro=vt, _nhom=nhom))
    gan_quyen.append(dict(id=stable_id('ndq', tkid), tai_khoan_id=tkid, _nhom=nhom,
                          created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH)))


def nd(v):
    """FK sang NGUOI_DUNG: chỉ giữ nếu người đó được chuyển."""
    v = g(v)
    return v if v in nd_ids else None


# =====================================================================
# 3. KHOA_HOC ← tbSanPham
# =====================================================================
loai_sp = {r['IdLoaiSanPham']: r['TenLoaiSanPham'] for r in doc('tbSanPham_LoaiSanPham')}
san_pham = that(doc('tbSanPham'))
khoa_hoc, ten_kh = [], Counter()
sp_gia = {}
for r in sorted(san_pham, key=lambda r: r['NgayTao'] or ''):
    ten = chuoi(r['TenSanPham'], 200)
    ten_kh[ten.lower()] += 1
    if ten_kh[ten.lower()] > 1:
        moi = f'{ten} ({ten_kh[ten.lower()]})'
        ghi('KHOA_HOC', r['IdSanPham'], f'tên "{ten}" trùng (UNIQUE tenant, ten)', f'đổi thành "{moi}"')
        ten = moi
    phan = [loai_sp.get(r['IdLoaiSanPham'])]
    if r['ThoiGianBuoiHoc']:
        phan.append(f'{r["ThoiGianBuoiHoc"]} phút/buổi')
    if r['GiaTienTungBuoi']:
        phan.append(f'{r["GiaTienTungBuoi"]:,}đ/buổi'.replace(',', '.'))
    gc = ' · '.join(p for p in phan if p)
    if chuoi(r['GhiChu']):
        gc += '\n' + r['GhiChu'].strip()
    khoa_hoc.append(dict(
        id=g(r['IdSanPham']), ten=ten, gia_tien=Decimal(r['GiaTien']), don_vi_tien=0,
        so_buoi=r['SoBuoi'], dang_ban=r['TrangThai'] == 1,
        ghi_chu=chuoi(gc, 1000, 'KHOA_HOC', r['IdSanPham'], 'ghi_chu'),
        created_at=ngay(r['NgayTao'] or NGAY_MAC_DINH), updated_at=ngay(r['NgaySua']),
        _tao=nd(r['IdNguoiTao']), _sua=nd(r['IdNguoiSua'])))
    sp_gia[g(r['IdSanPham'])] = Decimal(r['GiaTien'])

# =====================================================================
# 4. KHACH_HANG ← tbKhachHang
# =====================================================================
pttt_ten = {g(r['IdPhuongThucThanhToan']): r['TenPhuongThucThanhToan'] for r in doc('tbPhuongThucThanhToan')}
khach_cu = that(doc('tbKhachHang'))
khach, kh_ids, kh_pttt = [], set(), {}
sdt_da_dung = {}

# --- Gộp khách trùng SĐT khi CHẮC CHẮN là cùng một người (chủ sản phẩm chốt 06/10/2026) ---
#
# `UNIQUE(tenant_id, so_dien_thoai)` tồn tại để "chặn hai người bán nhập cùng một khách". Khi
# hai hồ sơ cùng số VÀ cùng tên thì đó đúng là ca nó muốn chặn — gộp mới là làm đúng ý ràng
# buộc, chứ không phải lách nó.
#
# CHỈ gộp khi tên khớp sau khi bỏ dấu và bỏ hậu tố số: "Tran Dinh Canh" ≡ "Tran Dinh Canh 1",
# "Nguyen Van Huy" ≡ "Nguyễn Văn Huy". Tên khác hẳn thì GIỮ RIÊNG hai hồ sơ — số điện thoại
# dùng chung là chuyện thật (vợ chồng, phụ huynh đăng ký cho con, số rác "00000"), gộp nhầm
# là trộn hai người thành một và không tách lại được.
#
# Hồ sơ giữ lại = tạo SỚM NHẤT (người mang khách về). Đơn của hồ sơ bị gộp dồn sang hồ sơ đó,
# nên doanh số quy về người tạo hồ sơ sớm nhất — đúng định nghĩa `KHACH_HANG.created_by_id`.


def _ten_gop(s):
    """Tên khách → chữ thường không dấu, bỏ hậu tố số (để so "Canh" với "Canh 1")."""
    b = unicodedata.normalize('NFD', s or '').replace('đ', 'd').replace('Đ', 'D')
    b = ''.join(c for c in b if unicodedata.category(c) != 'Mn')
    return re.sub(r'\d+$', '', re.sub(r'[^A-Za-z0-9]', '', b).lower())


_theo_sdt = defaultdict(list)
for _r in sorted((x for x in khach_cu if x['TrangThai'] != 0), key=lambda x: x['NgayTao']):
    _sdt = re.sub(r'\D', '', _r['SoDienThoai'] or '')
    if _sdt:
        _theo_sdt[_sdt].append(_r)

gop_ve = {}        # id khách bị gộp (GUID chuẩn hoá) → id khách giữ lại
for _sdt, _ds in _theo_sdt.items():
    if len(_ds) < 2:
        continue
    _giu = _ds[0]
    for _bo in _ds[1:]:
        if _ten_gop(_bo['TenKhachHang']) == _ten_gop(_giu['TenKhachHang']):
            gop_ve[g(_bo['IdKhachHang'])] = g(_giu['IdKhachHang'])
            ghi('KHACH_HANG', _bo['IdKhachHang'],
                f'trùng SĐT "{_sdt}" và cùng tên với khách tạo trước "{_giu["TenKhachHang"]}"',
                f'GỘP vào khách {_giu["IdKhachHang"]}; đơn dồn sang hồ sơ đó')
for r in sorted(khach_cu, key=lambda r: r['NgayTao']):
    if r['TrangThai'] == 0:
        ghi('KHACH_HANG', r['IdKhachHang'], 'khách đã xoá mềm', 'không chuyển')
        continue
    kid = g(r['IdKhachHang'])
    if kid in gop_ve:
        continue          # đã gộp vào hồ sơ tạo trước — xem khối gộp ở trên
    kh_ids.add(kid)
    pt_ten = pttt_ten.get(g(r['IdPhuongThucThanhToan']))
    pt = PTTT.get(pt_ten, 1)
    kh_pttt[kid] = (pt, pt_ten)

    phan = []
    if chuoi(r['GhiChu']):
        phan.append(r['GhiChu'].strip())
    for nhan, cot in (('Nghề nghiệp', 'NgheNghiep'), ('Tuổi', 'DoTuoi'), ('Địa chỉ', 'DiaChi'),
                      ('Nguồn', 'NguonKhachHang'), ('Link sale', 'LienKetSale')):
        if r.get(cot) not in (None, ''):
            phan.append(f'{nhan}: {str(r[cot]).strip()}')
    if pt_ten:
        phan.append(f'Hình thức thanh toán (hệ cũ): {pt_ten}')

    sdt = chuoi(r['SoDienThoai'], 30, 'KHACH_HANG', r['IdKhachHang'], 'so_dien_thoai')
    if sdt and sdt in sdt_da_dung:
        ghi('KHACH_HANG', r['IdKhachHang'],
            f'SĐT "{sdt}" trùng khách {sdt_da_dung[sdt]} (UNIQUE tenant, so_dien_thoai)',
            'giữ SĐT ở khách tạo trước; khách này để NULL, ghi SĐT vào ghi_chu')
        phan.insert(0, f'SĐT: {sdt} (trùng với khách tạo trước)')
        sdt = None
    elif sdt:
        sdt_da_dung[sdt] = r['TenKhachHang']

    tao = nd(r['IdNguoiTao'])
    if not tao:
        ghi('KHACH_HANG', r['IdKhachHang'], 'người tạo không được chuyển', 'created_by_id NULL (không tính doanh số cá nhân)')
    khach.append(dict(
        id=kid, ho_ten=chuoi(r['TenKhachHang'], 200), email=chuoi(r['Email'], 200),
        so_dien_thoai=sdt, link_facebook=chuoi(r['LienKet'], 500,'KHACH_HANG', r['IdKhachHang'], 'link_facebook'),
        ghi_chu=chuoi('\n'.join(phan), 1000, 'KHACH_HANG', r['IdKhachHang'], 'ghi_chu'),
        phuong_thuc_thanh_toan=pt, nguoi_dung_id=None, nguon=0,
        created_by_id=tao, updated_by_id=nd(r['IdNguoiSua']),
        created_at=ngay(r['NgayTao']), updated_at=ngay(r['NgaySua'])))

# =====================================================================
# 5. DANG_KY_KHOA_HOC ← tbKhachHang_DonHang
# =====================================================================
trinh_do = {g(r['IdTrinhDo']): r['TenTrinhDo'] for r in doc('tbSanPham_LoaiSanPham_TrinhDo')}
kh_ids_khoa = {k['id'] for k in khoa_hoc}
dang_ky, dk_ids, dk_tong = [], set(), {}
for r in sorted(that(doc('tbKhachHang_DonHang')), key=lambda r: r['NgayTao']):
    if r['TrangThai'] == 0:
        ghi('DANG_KY_KHOA_HOC', r['IdDonHang'], 'đơn đã xoá mềm', 'không chuyển')
        continue
    kid, khid = g(r['IdKhachHang']), g(r['IdSanPham'])
    kid = gop_ve.get(kid, kid)        # khách đã gộp → đơn thuộc hồ sơ giữ lại
    assert kid in kh_ids and khid in kh_ids_khoa, r['IdDonHang']
    did = g(r['IdDonHang'])
    dk_ids.add(did)
    gc = []
    if chuoi(r['GhiChu']):
        gc.append(r['GhiChu'].strip())
    tv, tr = trinh_do.get(g(r['IdTrinhDoDauVao'])), trinh_do.get(g(r['IdTrinhDoDauRa']))
    if tv or tr:
        gc.append(f'Trình độ (hệ cũ): {tv or "?"} → {tr or "?"}')
    so_tien = Decimal(r['TongSoTien'])
    dk_tong[did] = so_tien
    dang_ky.append(dict(
        id=did, khach_hang_id=kid, khoa_hoc_id=khid, san_pham_id=None, so_luong=1,
        gia_goc=sp_gia[khid], so_tien=so_tien, don_vi_tien=0, ty_gia_ve_vnd=Decimal(1),
        ngay_dang_ky=ngay(r['NgayTao']), phuong_thuc=kh_pttt[kid][0],
        ghi_chu=chuoi('\n'.join(gc), 1000, 'DANG_KY_KHOA_HOC', r['IdDonHang'], 'ghi_chu'),
        created_by_id=nd(r['IdNguoiTao']), updated_by_id=nd(r['IdNguoiSua']),
        created_at=ngay(r['NgayTao']), updated_at=ngay(r['NgaySua'])))

# =====================================================================
# 6. THU_TIEN_DANG_KY ← tbKhachHang_DonHang_ThanhToan
# =====================================================================
ty_gia = {r['GiaTriQuyDoiVND']: r['MaDonViTien'].upper() for r in doc('tbDonViTien')}
thu_tien = []
for r in sorted(that(doc('tbKhachHang_DonHang_ThanhToan')), key=lambda r: r['NgayTao']):
    did = g(r['IdDonHang'])
    if r['TrangThai'] == 0:
        ghi('THU_TIEN_DANG_KY', r['IdThanhToan'], 'lần thu đã xoá mềm', 'không chuyển'); continue
    if did not in dk_ids:
        ghi('THU_TIEN_DANG_KY', r['IdThanhToan'], 'đơn không được chuyển', 'không chuyển'); continue
    if r['SoTienDaDong'] <= 0:
        ghi('THU_TIEN_DANG_KY', r['IdThanhToan'], f'số tiền {r["SoTienDaDong"]} ≤ 0 (CHECK so_tien > 0)', 'không chuyển'); continue
    gc = []
    if chuoi(r['GhiChu']):
        gc.append(r['GhiChu'].strip())
    goc = r['SoTienDaDong_ChuaQuyDoi']
    if goc and goc != r['SoTienDaDong']:
        tg = round(r['SoTienDaDong'] / goc)
        dv = ty_gia.get(tg, '?')
        gc.append(f'Tiền gốc (hệ cũ): {goc:,} {dv} × {tg:,}'.replace(',', '.'))
    # Khách đã gộp: phương thức lấy theo hồ sơ GIỮ LẠI, vì hồ sơ bị gộp không được chèn.
    pt, pt_ten = kh_pttt[gop_ve.get(g(r['IdKhachHang']), g(r['IdKhachHang']))]
    thu_tien.append(dict(
        id=g(r['IdThanhToan']), dang_ky_id=did, so_tien=Decimal(r['SoTienDaDong']),
        ngay_thu=ngay(r['NgayTao']), phuong_thuc=pt,
        ghi_chu=chuoi('\n'.join(gc), 500, 'THU_TIEN_DANG_KY', r['IdThanhToan'], 'ghi_chu'),
        xac_nhan_du_tien=False, nguoi_thu_id=nd(r['IdNguoiTao']),
        created_by_id=nd(r['IdNguoiTao']), updated_by_id=nd(r['IdNguoiSua']),
        created_at=ngay(r['NgayTao']), updated_at=ngay(r['NgaySua'])))

# =====================================================================
# Số kỳ vọng (dùng cho khối kiểm DO $$ và file đối soát)
# =====================================================================
nhom_moi_rows = [(ten, mt) for ten, (_, mt) in NHOM_MOI.items()]
so_qcn = sum(len(h) for _, mt in nhom_moi_rows for h in mt.values())
tong_cam_ket = sum(d['so_tien'] for d in dang_ky)
tong_da_thu = sum(t['so_tien'] for t in thu_tien)
KY_VONG = dict(phong_ban=len(phong_ban), nguoi_dung=len(nguoi), tai_khoan=len(tai_khoan),
               tai_khoan_hoat_dong=sum(t['trang_thai'] == 0 for t in tai_khoan),
               ho_so_gv=len(ho_so_gv), mxh=len(mxh), quyen_moi=len(NHOM_MOI), qcn_moi=so_qcn,
               khoa_hoc=len(khoa_hoc), khach_hang=len(khach), dang_ky=len(dang_ky),
               thu_tien=len(thu_tien), tong_cam_ket=tong_cam_ket, tong_da_thu=tong_da_thu)

# =====================================================================
# Ghi file SQL
# =====================================================================
DAU = f"""-- Chuyển dữ liệu VIETGEN Academy (hệ cũ) → GiapTech.LangCenter.
-- SINH TỰ ĐỘNG bởi sinh_sql.py — đừng sửa tay, sửa script sinh rồi chạy lại.
--
-- Chỉ đơn vị thật VIETGEN Academy ({DON_VI_THAT}); không lấy LOCALHOST.
-- Chỉ INSERT vào MỘT trung tâm mới tạo (quy tắc #1). Một giao dịch: lỗi ở đâu là ROLLBACK cả.
--
-- Chạy (sau khi đã tạo trung tâm qua màn chủ hệ thống):
--   Dev: docker exec -i lms-pg psql -U langcenter -d langcenter -v ON_ERROR_STOP=1 \\
--          -v ma_trung_tam=XXXXXXX < 01-chuyen-doi.sql
--   VPS: cd /opt/langcenter && set -a && . ./.env && set +a
--        docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \\
--          psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \\
--          -v ma_trung_tam=XXXXXXX < 01-chuyen-doi.sql

\\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT id AS tenant_id FROM "TENANT" WHERE ma_trung_tam = :'ma_trung_tam';

-- Chốt an toàn: đúng một trung tâm, và trung tâm đó còn TRẮNG (chỉ có dữ liệu seeder tạo).
DO $$
DECLARE tid uuid; n int;
BEGIN
  SELECT count(*) INTO n FROM _ctx;
  IF n <> 1 THEN RAISE EXCEPTION 'Không tìm thấy trung tâm theo ma_trung_tam (% dòng). Huỷ.', n; END IF;
  SELECT tenant_id INTO tid FROM _ctx;
  SELECT count(*) INTO n FROM "NGUOI_DUNG" WHERE tenant_id = tid;
  IF n <> 1 THEN RAISE EXCEPTION 'Trung tâm đã có % người dùng (chỉ được có admin của seeder). Huỷ — quy tắc #1.', n; END IF;
  SELECT count(*) INTO n FROM "KHACH_HANG" WHERE tenant_id = tid;
  IF n <> 0 THEN RAISE EXCEPTION 'Trung tâm đã có % khách hàng. Huỷ — quy tắc #1.', n; END IF;
  SELECT count(*) INTO n FROM "KHOA_HOC" WHERE tenant_id = tid;
  IF n <> 0 THEN RAISE EXCEPTION 'Trung tâm đã có % khoá học. Huỷ — quy tắc #1.', n; END IF;
  SELECT count(*) INTO n FROM "PHONG_BAN" WHERE tenant_id = tid;
  IF n <> 0 THEN RAISE EXCEPTION 'Trung tâm đã có % phòng ban. Huỷ — quy tắc #1.', n; END IF;
  SELECT count(*) INTO n FROM "QUYEN" WHERE tenant_id = tid
    AND ten_quyen IN ('Quản trị viên', 'Giáo viên');
  IF n <> 2 THEN RAISE EXCEPTION 'Thiếu nhóm quyền seed "Quản trị viên"/"Giáo viên". Huỷ.'; END IF;
END $$;

\\echo '--- TRƯỚC ---'
SELECT (SELECT count(*) FROM "NGUOI_DUNG" n JOIN _ctx c USING (tenant_id)) AS nguoi_dung,
       (SELECT count(*) FROM "TAI_KHOAN" n JOIN _ctx c USING (tenant_id)) AS tai_khoan,
       (SELECT count(*) FROM "QUYEN" n JOIN _ctx c USING (tenant_id)) AS quyen;

"""

phan = [DAU]

phan.append('-- ===================== 1. PHONG_BAN =====================\n'
            '-- Người quản lý và cột audit gán ở bước 7 (cần NGUOI_DUNG có trước).\n')
phan.append(insert('PHONG_BAN', [('id', 'uuid'), ('ten', None), ('phong_ban_cha_id', 'uuid'),
                                 ('thu_tu', 'int'), ('tag_vai_tro', 'int'), ('mo_ta', None)] + AUDIT,
                   phong_ban, '(cha trước con)'))

phan.append('-- ===================== 2. NGUOI_DUNG =====================\n')
phan.append(insert('NGUOI_DUNG', [('id', 'uuid'), ('ho_ten', None), ('email', None),
                                  ('so_dien_thoai', None), ('ngay_sinh', 'timestamptz'),
                                  ('loai_nguoi_dung', 'int'), ('trang_thai_nhan_su', 'int'),
                                  ('phong_ban_id', 'uuid'), ('so_tai_khoan', None),
                                  ('ten_ngan_hang', None), ('ghi_chu', None)] + AUDIT, nguoi))
phan.append(insert('HO_SO_GIAO_VIEN', [('id', 'uuid'), ('nguoi_dung_id', 'uuid')] + AUDIT, ho_so_gv))
phan.append(insert('LIEN_KET_MXH', [('id', 'uuid'), ('nguoi_dung_id', 'uuid'), ('loai', 'int'),
                                    ('duong_dan', None), ('ghi_chu', None)] + AUDIT, mxh))

phan.append('-- ===================== 3. TAI_KHOAN =====================\n'
            f"-- password_hash = '{HASH_GIU_CHO}' (KHÔNG phải hash): đăng nhập bị từ chối cho tới khi\n"
            '-- quản trị đặt mật khẩu qua API (dat-mat-khau-tam.sh). Không ghi hash tay.\n')
phan.append(insert('TAI_KHOAN', [('id', 'uuid'), ('nguoi_dung_id', 'uuid'), ('username', None),
                                 ('password_hash', None), ('phai_doi_mat_khau', 'boolean'),
                                 ('trang_thai', 'int')] + AUDIT, tai_khoan))

phan.append('-- ===================== 4. QUYEN + QUYEN_CHUC_NANG (nhóm mới) =====================\n'
            '-- Chỉ cặp (chức năng, thao tác) có trong ChucNang.ThaoTacTheoChucNang.\n')
quyen_rows, qcn_rows = [], []
for ten, (mo_ta, mt) in NHOM_MOI.items():
    qid = stable_id('quyen', ten)
    quyen_rows.append(dict(id=qid, ten_quyen=ten, mo_ta=mo_ta))
    for cn, hds in sorted(mt.items()):
        for hd in hds:
            qcn_rows.append(dict(id=stable_id('qcn', ten, cn, str(hd)), quyen_id=qid,
                                 ten_chuc_nang=cn, hanh_dong=hd))
phan.append(insert('QUYEN', [('id', 'uuid'), ('ten_quyen', None), ('mo_ta', None)],
                   quyen_rows).replace('SELECT v.*, c.tenant_id', 'SELECT v.*, now(), c.tenant_id')
            .replace(', tenant_id)\n', ', created_at, tenant_id)\n', 1))
phan.append(insert('QUYEN_CHUC_NANG', [('id', 'uuid'), ('quyen_id', 'uuid'), ('ten_chuc_nang', None),
                                       ('hanh_dong', 'int')], qcn_rows)
            .replace('SELECT v.*, c.tenant_id', 'SELECT v.*, now(), c.tenant_id')
            .replace(', tenant_id)\n', ', created_at, tenant_id)\n', 1))

phan.append('-- ===================== 5. NGUOIDUNG_QUYEN =====================\n'
            '-- Nhóm seed ("Quản trị viên", "Giáo viên") tra theo tên trong trung tâm này.\n')
vals = []
for gq in gan_quyen:
    nhom = gq['_nhom'].lstrip('*')
    vals.append(f"  ({q(gq['id'], 'uuid')}, {q(gq['tai_khoan_id'], 'uuid')}, {q(nhom)}, {q(gq['created_at'], 'timestamptz')})")
phan.append(f'-- NGUOIDUNG_QUYEN: {len(vals)} dòng\n'
            'INSERT INTO "NGUOIDUNG_QUYEN" (id, tai_khoan_id, quyen_id, created_at, tenant_id)\n'
            'SELECT v.id, v.tai_khoan_id, qu.id, v.created_at, c.tenant_id FROM (VALUES\n' +
            ',\n'.join(vals) + '\n) AS v(id, tai_khoan_id, ten_quyen, created_at)\n'
            'CROSS JOIN _ctx c\nJOIN "QUYEN" qu ON qu.tenant_id = c.tenant_id AND qu.ten_quyen = v.ten_quyen;\n\n')

phan.append('-- ===================== 6. CRM: KHOA_HOC · KHACH_HANG · DANG_KY · THU_TIEN =====================\n')
phan.append(insert('KHOA_HOC', [('id', 'uuid'), ('ten', None), ('gia_tien', 'numeric'),
                                ('don_vi_tien', 'int'), ('so_buoi', 'int'), ('dang_ban', 'boolean'),
                                ('ghi_chu', None)] + AUDIT, khoa_hoc))
phan.append(insert('KHACH_HANG', [('id', 'uuid'), ('ho_ten', None), ('email', None),
                                  ('so_dien_thoai', None), ('link_facebook', None), ('ghi_chu', None),
                                  ('phuong_thuc_thanh_toan', 'int'), ('nguoi_dung_id', 'uuid'),
                                  ('nguon', 'int'), ('created_by_id', 'uuid'),
                                  ('updated_by_id', 'uuid')] + AUDIT, khach,
                   '(created_by_id = nhân viên mang khách về = mốc doanh số)'))
phan.append(insert('DANG_KY_KHOA_HOC', [('id', 'uuid'), ('khach_hang_id', 'uuid'), ('khoa_hoc_id', 'uuid'),
                                        ('san_pham_id', 'uuid'), ('so_luong', 'int'), ('gia_goc', 'numeric'),
                                        ('so_tien', 'numeric'), ('don_vi_tien', 'int'),
                                        ('ty_gia_ve_vnd', 'numeric'), ('ngay_dang_ky', 'timestamptz'),
                                        ('phuong_thuc', 'int'), ('ghi_chu', None),
                                        ('created_by_id', 'uuid'), ('updated_by_id', 'uuid')] + AUDIT,
                   dang_ky, '(so_tien = CAM KẾT = cơ sở doanh thu)'))
phan.append(insert('THU_TIEN_DANG_KY', [('id', 'uuid'), ('dang_ky_id', 'uuid'), ('so_tien', 'numeric'),
                                        ('ngay_thu', 'timestamptz'), ('phuong_thuc', 'int'),
                                        ('ghi_chu', None), ('xac_nhan_du_tien', 'boolean'),
                                        ('nguoi_thu_id', 'uuid'), ('created_by_id', 'uuid'),
                                        ('updated_by_id', 'uuid')] + AUDIT, thu_tien,
                   '(xac_nhan_du_tien = false: người thu tự quyết sau)'))

# 7. Cột tự tham chiếu NGUOI_DUNG (chỉ các hàng vừa nạp trong giao dịch này)
phan.append('-- ===================== 7. Người quản lý phòng & cột audit trỏ NGUOI_DUNG =====================\n'
            '-- Chỉ cập nhật hàng VỪA NẠP ở trên (lọc theo danh sách id), không đụng hàng có sẵn.\n')


def cap_nhat(bang, rows, cot_map):
    vals = []
    for r in rows:
        v = [r[k] for k in cot_map.values()]
        if any(v):
            vals.append('  (' + q(r['id'], 'uuid') + ', ' + ', '.join(q(nd(x) if x else None, 'uuid') for x in v) + ')')
    if not vals:
        return ''
    cots = list(cot_map.keys())
    set_ = ', '.join(f'{c} = v.{c}' for c in cots)
    return (f'UPDATE "{bang}" t SET {set_}\nFROM (VALUES\n' + ',\n'.join(vals) +
            f'\n) AS v(id, {", ".join(cots)})\nWHERE t.id = v.id AND t.tenant_id = (SELECT tenant_id FROM _ctx);\n\n')


phan.append(cap_nhat('PHONG_BAN', phong_ban, {'nguoi_quan_ly_id': '_ql', 'created_by_id': '_tao', 'updated_by_id': '_sua'}))
phan.append(cap_nhat('NGUOI_DUNG', nguoi, {'created_by_id': '_tao', 'updated_by_id': '_sua'}))
phan.append(cap_nhat('KHOA_HOC', khoa_hoc, {'created_by_id': '_tao', 'updated_by_id': '_sua'}))

# 8. Kiểm kết quả
kv = KY_VONG
phan.append(f"""-- ===================== 8. Chốt kết quả =====================
DO $$
DECLARE tid uuid; n int; s numeric;
BEGIN
  SELECT tenant_id INTO tid FROM _ctx;
  SELECT count(*) INTO n FROM "PHONG_BAN" WHERE tenant_id = tid;
  IF n <> {kv['phong_ban']} THEN RAISE EXCEPTION 'PHONG_BAN: % ≠ {kv['phong_ban']}', n; END IF;
  SELECT count(*) INTO n FROM "NGUOI_DUNG" WHERE tenant_id = tid;
  IF n <> {kv['nguoi_dung'] + 1} THEN RAISE EXCEPTION 'NGUOI_DUNG: % ≠ {kv['nguoi_dung'] + 1} (kể cả admin)', n; END IF;
  SELECT count(*) INTO n FROM "TAI_KHOAN" WHERE tenant_id = tid AND password_hash = '{HASH_GIU_CHO}';
  IF n <> {kv['tai_khoan']} THEN RAISE EXCEPTION 'TAI_KHOAN chuyển đổi: % ≠ {kv['tai_khoan']}', n; END IF;
  SELECT count(*) INTO n FROM "NGUOIDUNG_QUYEN" nq JOIN "TAI_KHOAN" tk ON tk.id = nq.tai_khoan_id
    WHERE tk.tenant_id = tid AND tk.password_hash = '{HASH_GIU_CHO}';
  IF n <> {kv['tai_khoan']} THEN RAISE EXCEPTION 'NGUOIDUNG_QUYEN: % ≠ {kv['tai_khoan']} (thiếu nhóm quyền?)', n; END IF;
  SELECT count(*) INTO n FROM "QUYEN_CHUC_NANG" qc JOIN "QUYEN" qu ON qu.id = qc.quyen_id
    WHERE qu.tenant_id = tid AND qu.mo_ta LIKE 'Chuyển từ vai trò%';
  IF n <> {kv['qcn_moi']} THEN RAISE EXCEPTION 'QUYEN_CHUC_NANG nhóm mới: % ≠ {kv['qcn_moi']}', n; END IF;
  SELECT count(*) INTO n FROM "KHOA_HOC" WHERE tenant_id = tid;
  IF n <> {kv['khoa_hoc']} THEN RAISE EXCEPTION 'KHOA_HOC: % ≠ {kv['khoa_hoc']}', n; END IF;
  SELECT count(*) INTO n FROM "KHACH_HANG" WHERE tenant_id = tid;
  IF n <> {kv['khach_hang']} THEN RAISE EXCEPTION 'KHACH_HANG: % ≠ {kv['khach_hang']}', n; END IF;
  SELECT count(*), sum(so_tien * ty_gia_ve_vnd) INTO n, s FROM "DANG_KY_KHOA_HOC" WHERE tenant_id = tid;
  IF n <> {kv['dang_ky']} OR s <> {kv['tong_cam_ket']} THEN
    RAISE EXCEPTION 'DANG_KY_KHOA_HOC: % đơn / % ≠ {kv['dang_ky']} / {kv['tong_cam_ket']}', n, s; END IF;
  SELECT count(*), sum(so_tien) INTO n, s FROM "THU_TIEN_DANG_KY" WHERE tenant_id = tid;
  IF n <> {kv['thu_tien']} OR s <> {kv['tong_da_thu']} THEN
    RAISE EXCEPTION 'THU_TIEN_DANG_KY: % lần / % ≠ {kv['thu_tien']} / {kv['tong_da_thu']}', n, s; END IF;
END $$;

\\echo '--- SAU ---'
SELECT (SELECT count(*) FROM "NGUOI_DUNG" n JOIN _ctx c USING (tenant_id)) AS nguoi_dung,
       (SELECT count(*) FROM "TAI_KHOAN" n JOIN _ctx c USING (tenant_id)) AS tai_khoan,
       (SELECT count(*) FROM "QUYEN" n JOIN _ctx c USING (tenant_id)) AS quyen,
       (SELECT count(*) FROM "KHACH_HANG" n JOIN _ctx c USING (tenant_id)) AS khach_hang,
       (SELECT count(*) FROM "DANG_KY_KHOA_HOC" n JOIN _ctx c USING (tenant_id)) AS don_hang,
       (SELECT sum(so_tien * ty_gia_ve_vnd) FROM "DANG_KY_KHOA_HOC" n JOIN _ctx c USING (tenant_id)) AS doanh_thu,
       (SELECT sum(so_tien) FROM "THU_TIEN_DANG_KY" n JOIN _ctx c USING (tenant_id)) AS da_thu;

COMMIT;
\\echo 'XONG. Restart API (cache quyền 5 phút) rồi chạy dat-mat-khau-tam.sh.'
""")

with open(os.path.join(OUT, '01-chuyen-doi.sql'), 'w', encoding='utf-8') as f:
    f.write(''.join(phan))

# ---------- Báo cáo làm sạch ----------
with open(os.path.join(OUT, 'bao-cao-lam-sach.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.writer(f)
    w.writerow(['Bảng đích', 'Id cũ', 'Vấn đề', 'Xử lý'])
    w.writerows(bao_cao)

# ---------- Danh sách tài khoản (để đối chiếu và gửi mật khẩu) ----------
with open(os.path.join(OUT, 'danh-sach-tai-khoan.csv'), 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.writer(f)
    w.writerow(['tai_khoan_id', 'username_moi', 'username_cu', 'ho_ten', 'vai_tro_cu',
                'nhom_quyen', 'trang_thai'])
    for t in sorted(tai_khoan, key=lambda t: (t['_nhom'], t['username'])):
        w.writerow([t['id'], t['username'], t['_ten_cu'], t['_ho_ten'], t['_vai_tro'],
                    t['_nhom'].lstrip('*'), 'VoHieuHoa' if t['trang_thai'] else 'HoatDong'])

with open(os.path.join(OUT, 'ky-vong.json'), 'w', encoding='utf-8') as f:
    json.dump({k: str(v) for k, v in KY_VONG.items()}, f, ensure_ascii=False, indent=2)

print(json.dumps({k: str(v) for k, v in KY_VONG.items()}, ensure_ascii=False, indent=1))
print('Báo cáo làm sạch:', Counter(b[2].split(' "')[0].split(' (')[0] for b in bao_cao).most_common(30))
