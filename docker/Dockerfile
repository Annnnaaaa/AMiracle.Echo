# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AMiracle.Echo.Host/AMiracle.Echo.Host.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV AMiracle__Echo__BlobStore__RootPath=/data/blobs
ENV AMiracle__Echo__Database__ConnectionString="Data Source=/data/echo.db"
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "AMiracle.Echo.Host.dll"]
