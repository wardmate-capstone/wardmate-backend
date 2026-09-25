FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/WardMate.Services.IAM.API.csproj", "src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/"]
COPY ["src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/WardMate.Services.IAM.Application.csproj", "src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/"]
COPY ["src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/WardMate.Services.IAM.Domain.csproj", "src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/"]
COPY ["src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/WardMate.Services.IAM.Infrastructure.csproj", "src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/"]
COPY ["src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj", "src/BuildingBlocks/WardMate.SharedKernel/"]
RUN dotnet restore "src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/WardMate.Services.IAM.API.csproj"
COPY . .
WORKDIR "/src/src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API"
RUN dotnet build "WardMate.Services.IAM.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WardMate.Services.IAM.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WardMate.Services.IAM.API.dll"]
