FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ServidorWeb/ServidorWeb.csproj ServidorWeb/
RUN dotnet restore ServidorWeb/ServidorWeb.csproj
COPY . .
RUN dotnet publish ServidorWeb/ServidorWeb.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet ServidorWeb.dll"]