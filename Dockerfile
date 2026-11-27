# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "src/Guidevault.Web/Guidevault.Web.csproj"
RUN --mount=type=secret,id=sixlabors_license_b64,required=true \
    base64 -d /run/secrets/sixlabors_license_b64 > /tmp/sixlabors.lic && \
    SixLaborsLicenseFile=/tmp/sixlabors.lic \
    dotnet publish "src/Guidevault.Web/Guidevault.Web.csproj" \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false && \
    rm -f /tmp/sixlabors.lic

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5478
ENV ASPNETCORE_HTTP_PORTS=5478
ENV GUIDEVAULT_DATA=/data
ENV PAGEQUEST__DATA__ROOT=/data
ENV GUIDEVAULT_LIBRARY_PATH=/data/library
ENV PAGEQUEST_LIBRARY_PATH=/data/library

RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils tesseract-ocr tesseract-ocr-eng \
    && command -v pdftoppm \
    && command -v pdftocairo \
    && command -v tesseract \
    && rm -rf /var/lib/apt/lists/*

# CPU-only experimental vision runtime; weights are downloaded only on explicit use.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-venv \
    && python3 -m venv /opt/guidevault-vision \
    && /opt/guidevault-vision/bin/pip install --no-cache-dir torch==2.7.1+cpu torchvision==0.22.1+cpu --index-url https://pypi.org/simple --extra-index-url https://download.pytorch.org/whl/cpu \
    && /opt/guidevault-vision/bin/pip install --no-cache-dir transformers==4.57.2 pillow==11.3.0 \
    && rm -rf /var/lib/apt/lists/*
COPY vision/cover_ocr.py /opt/guidevault-vision/cover_ocr.py
RUN /opt/guidevault-vision/bin/python -m py_compile /opt/guidevault-vision/cover_ocr.py && test "$(/opt/guidevault-vision/bin/python /opt/guidevault-vision/cover_ocr.py --self-test)" = "guidevault-vision-deep-v7.1.3"
RUN mkdir -p /data/library

COPY --from=build /app/publish .

EXPOSE 5478
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Guidevault.Web.dll"]
