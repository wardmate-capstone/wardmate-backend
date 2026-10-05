FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/WardMate.Services.ApplicationWorkflow.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "WardMate.Services.ApplicationWorkflow.API.dll"]
