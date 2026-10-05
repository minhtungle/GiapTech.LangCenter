"""Dựng DDL PostgreSQL cho các bảng liên quan từ AppDbContextModelSnapshot.cs (chỉ để TEST script)."""
import re, sys
s = open(sys.argv[1]).read()
CAN = {'Tenant','NguoiDung','TaiKhoan','Quyen','QuyenChucNang','NguoiDungQuyen','PhongBan','ChucVu',
       'HoSoGiaoVien','HoSoNhanVien','LienKetMxh','KhachHang','KhoaHoc','SanPham','DangKyKhoaHoc','ThuTienDangKy'}
blocks = re.split(r'\n\s{12}modelBuilder\.Entity\("', s)
tables, cols, ddl_extra, fks = {}, {}, [], []
def snake(n): return re.sub(r'(?<!^)(?=[A-Z])', '_', n).lower()
for b in blocks[1:]:
    full = b.split('"', 1)[0]; name = full.split('.')[-1]
    if name not in CAN: continue
    m = re.search(r'ToTable\("([^"]+)"', b)
    if m: tables[name] = m.group(1)
for b in blocks[1:]:
    full = b.split('"', 1)[0]; name = full.split('.')[-1]
    if name not in CAN or name not in tables: continue
    t = tables[name]
    if 'b.Property<' in b:
        cs = []
        for m in re.finditer(r'b\.Property<([^>]+)>\("(\w+)"\)(.*?);', b, re.S):
            typ, prop, rest = m.groups()
            col = re.search(r'HasColumnName\("(\w+)"\)', rest).group(1)
            ct = re.search(r'HasColumnType\("([^"]+)"\)', rest).group(1)
            nn = ('IsRequired' in rest) or (not typ.endswith('?') and typ != 'string')
            cs.append(f'  {col} {ct}{" NOT NULL" if nn else ""}')
        cols[t] = cs
        for m in re.finditer(r'b\.HasIndex\(([^)]*)\)(.*?);', b, re.S):
            props = [snake(p.strip().strip('"')) for p in m.group(1).split(',')]
            r = m.group(2)
            if 'IsUnique' not in r: continue
            nm = re.search(r'HasDatabaseName\("(\w+)"\)', r).group(1)
            flt = re.search(r'HasFilter\("((?:[^"\\]|\\.)*)"\)', r)
            nnd = ' NULLS NOT DISTINCT' if 'AreNullsDistinct(false)' in r else ''
            ddl_extra.append(f'CREATE UNIQUE INDEX {nm} ON "{t}" ({", ".join(props)}){nnd}' + (f' WHERE {flt.group(1)}' if flt else '') + ';')
        for m in re.finditer(r'HasCheckConstraint\("(\w+)",\s*"((?:[^"\\]|\\.)*)"', b):
            ddl_extra.append(f'ALTER TABLE "{t}" ADD CONSTRAINT {m.group(1)} CHECK ({m.group(2)});')
    for m in re.finditer(r'b\.HasOne\("([^"]+)"[^;]*?\.HasForeignKey\("(\w+)"\)(.*?);', b, re.S):
        tgt = m.group(1).split('.')[-1]
        if tgt not in tables: continue
        ond = re.search(r'OnDelete\(DeleteBehavior\.(\w+)\)', m.group(3))
        od = {'Cascade': 'CASCADE', 'SetNull': 'SET NULL', 'Restrict': 'RESTRICT'}.get(ond.group(1) if ond else '', 'NO ACTION')
        fks.append((t, snake(m.group(2)), tables[tgt], od))
out = []
for t, cs in cols.items():
    out.append(f'CREATE TABLE "{t}" (\n' + ',\n'.join(cs) + ',\n  PRIMARY KEY (id)\n);')
out += ddl_extra
for t, c, tt, od in fks:
    out.append(f'ALTER TABLE "{t}" ADD FOREIGN KEY ({c}) REFERENCES "{tt}"(id) ON DELETE {od};')
print('\n'.join(out))
