# ─── Stage 1: Base runtime image ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Install ICU libraries for globalization (cần cho tiếng Việt / encoding)
RUN apk add --no-cache icu-libs

# Tạo user non-root để tăng bảo mật
RUN addgroup -S wardmate && adduser -S wardmate -G wardmate
USER wardmate

# ─── Stage 2: Build ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy Directory.Build.props trước (invalidates cache ít hơn)
COPY ["Directory.Build.props", "./"]

# Copy tất cả .csproj để restore trước (tận dụng Docker layer cache)
COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/WardMate.Services.DocumentForm.API.csproj",           "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/"]
COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/WardMate.Services.DocumentForm.Application.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/"]
COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/WardMate.Services.DocumentForm.Domain.csproj",       "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/"]
COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/WardMate.Services.DocumentForm.Infrastructure.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/"]
COPY ["src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj", "src/BuildingBlocks/WardMate.SharedKernel/"]

RUN dotnet restore "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/WardMate.Services.DocumentForm.API.csproj" \
    --runtime linux-musl-x64

# Copy toàn bộ source
COPY . .

WORKDIR "/src/src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API"

RUN dotnet build "WardMate.Services.DocumentForm.API.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    --runtime linux-musl-x64 \
    --self-contained false

# ─── Stage 3: Publish ───────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "WardMate.Services.DocumentForm.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-musl-x64 \
    --self-contained false \
    /p:UseAppHost=false

# ─── Stage 4: Final ─────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check cho Azure Container Apps / Docker Compose
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "WardMate.Services.DocumentForm.API.dll"]
