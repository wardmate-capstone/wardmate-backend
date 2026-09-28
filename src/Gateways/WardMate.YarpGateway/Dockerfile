FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["src/Gateways/WardMate.YarpGateway/WardMate.YarpGateway.csproj", "src/Gateways/WardMate.YarpGateway/"]
COPY ["src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj", "src/BuildingBlocks/WardMate.SharedKernel/"]
RUN dotnet restore "src/Gateways/WardMate.YarpGateway/WardMate.YarpGateway.csproj"
COPY . .
WORKDIR "/src/src/Gateways/WardMate.YarpGateway"
RUN dotnet build "WardMate.YarpGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WardMate.YarpGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WardMate.YarpGateway.dll"]
