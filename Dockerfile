# ==========================================
# Stage 1: Build
# ==========================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src

# نسخ ملف المشروع واستعادة الحزم حسب المعمارية
COPY ["WhatsAppGateway.csproj", "./"]
RUN dotnet restore "WhatsAppGateway.csproj" -a $TARGETARCH

# نسخ باقي الملفات وبناء المشروع
COPY . .
RUN dotnet publish "WhatsAppGateway.csproj" -c Release -o /app/publish -a $TARGETARCH /p:UseAppHost=false

# ==========================================
# Stage 2: Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# نسخ الملفات المنشورة
COPY --from=build /app/publish .

# إعداد المنفذ
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# تشغيل التطبيق
ENTRYPOINT ["dotnet", "WhatsAppGateway.dll"]
