# Build từ thư mục GỐC solution, không phải từ thư mục này:
#   docker build -f src/GiapTech.LangCenter.LMS.API/Dockerfile .
# Lý do: cần .csproj của cả 4 project để restore.

# ---------- Giai đoạn build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy riêng file .csproj trước rồi mới restore: lớp này chỉ bị dựng lại khi danh sách
# package đổi, nên sửa code thường không phải tải lại toàn bộ NuGet.
COPY Directory.Build.props ./
COPY GiapTech.LangCenter.LMS.sln ./
COPY src/GiapTech.LangCenter.LMS.Domain/*.csproj          src/GiapTech.LangCenter.LMS.Domain/
COPY src/GiapTech.LangCenter.LMS.Application/*.csproj     src/GiapTech.LangCenter.LMS.Application/
COPY src/GiapTech.LangCenter.LMS.Infrastructure/*.csproj  src/GiapTech.LangCenter.LMS.Infrastructure/
COPY src/GiapTech.LangCenter.LMS.API/*.csproj             src/GiapTech.LangCenter.LMS.API/

RUN dotnet restore src/GiapTech.LangCenter.LMS.API/GiapTech.LangCenter.LMS.API.csproj

COPY src/ src/

RUN dotnet publish src/GiapTech.LangCenter.LMS.API/GiapTech.LangCenter.LMS.API.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Giai đoạn chạy ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Chạy bằng user thường, không phải root: nếu ứng dụng bị chiếm quyền thì kẻ tấn công
# cũng không có root trong container.
RUN addgroup -S langcenter-lms && adduser -S langcenter-lms -G langcenter-lms
USER langcenter-lms

COPY --from=build --chown=langcenter-lms:langcenter-lms /app/publish .

# Nghe cổng 8080: cổng <1024 cần quyền root, mà container chạy user thường.
# Caddy proxy tới đây (xem Caddyfile).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Nhận tín hiệu dừng đúng cách để container tắt gọn thay vì bị kill sau timeout.
ENTRYPOINT ["dotnet", "GiapTech.LangCenter.LMS.API.dll"]
