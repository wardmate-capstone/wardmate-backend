FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj", "src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/"]
COPY ["src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/WardMate.Services.ProcedureCatalog.Application.csproj", "src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/"]
COPY ["src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/WardMate.Services.ProcedureCatalog.Domain.csproj", "src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/"]
COPY ["src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj", "src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/"]
COPY ["src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj", "src/BuildingBlocks/WardMate.SharedKernel/"]
RUN dotnet restore "src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj"
COPY . .
WORKDIR "/src/src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API"
RUN dotnet build "WardMate.Services.ProcedureCatalog.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WardMate.Services.ProcedureCatalog.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WardMate.Services.ProcedureCatalog.API.dll"]
