# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "src/Guidevault.Web/Guidevault.Web.csproj"
RUN dotnet publish "src/Guidevault.Web/Guidevault.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5478
ENV ASPNETCORE_HTTP_PORTS=5478
ENV GUIDEVAULT_DATA=/data
ENV PAGEQUEST__DATA__ROOT=/data
ENV GUIDEVAULT_LIBRARY_PATH=/data/library
ENV PAGEQUEST_LIBRARY_PATH=/data/library

RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils \
    && command -v pdftoppm \
    && command -v pdftocairo \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data/library

COPY --from=build /app/publish .

EXPOSE 5478
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Guidevault.Web.dll"]
