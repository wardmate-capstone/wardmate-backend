FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]

COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/WardMate.Services.DocumentForm.API.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/"]

COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/WardMate.Services.DocumentForm.Application.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/"]

COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/WardMate.Services.DocumentForm.Domain.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/"]

COPY ["src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/WardMate.Services.DocumentForm.Infrastructure.csproj", "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/"]

COPY ["src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj", "src/BuildingBlocks/WardMate.SharedKernel/"]

RUN dotnet restore "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/WardMate.Services.DocumentForm.API.csproj"

COPY . .

WORKDIR "/src/src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API"

RUN dotnet build "WardMate.Services.DocumentForm.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WardMate.Services.DocumentForm.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "WardMate.Services.DocumentForm.API.dll"]