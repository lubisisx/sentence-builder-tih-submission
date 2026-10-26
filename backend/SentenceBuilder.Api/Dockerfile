FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/SentenceBuilder.Api/SentenceBuilder.Api.csproj ./
RUN dotnet restore SentenceBuilder.Api.csproj
COPY backend/SentenceBuilder.Api/ ./
RUN dotnet publish SentenceBuilder.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SentenceBuilder.Api.dll"]
