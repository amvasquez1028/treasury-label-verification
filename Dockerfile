# syntax=docker/dockerfile:1

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0-jammy
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0-jammy

FROM ${DOTNET_SDK_IMAGE} AS backend-build
WORKDIR /src
COPY backend/ ./backend/
COPY tessdata/ ./tessdata/
COPY testdata/colas/ ./testdata/colas/
COPY testdata/layout-annotations/ ./testdata/layout-annotations/
COPY testdata/layout-models/ ./testdata/layout-models/
RUN dotnet publish backend/LabelVerification.Api/LabelVerification.Api.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    --self-contained false \
 && test -f /app/publish/libOpenCvSharpExtern.so \
 || (echo "ERROR: OpenCvSharp native library missing from publish output" && find /app/publish -name '*OpenCv*' && exit 1)

FROM node:20-bookworm AS frontend-build
WORKDIR /frontend
COPY package.json pnpm-lock.yaml ./
RUN npm install -g pnpm@9.15.4 && pnpm install --frozen-lockfile --ignore-scripts
COPY . .
RUN pnpm exec next build

# Ubuntu 22.04 matches OpenCvSharp4.runtime.ubuntu.22.04-x64
FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    tesseract-ocr libtesseract4 liblept5 \
    libjpeg8 libpng16-16 libtiff5 libopenexr25 \
    libglib2.0-0 libsm6 libxext6 libxrender1 libgomp1 \
    libgtk2.0-0 libgdk-pixbuf2.0-0 \
    libavcodec58 libavformat58 libavutil56 libswscale5 libdc1394-25 \
 && rm -rf /var/lib/apt/lists/*
COPY --from=backend-build /app/publish ./
RUN ldd /app/libOpenCvSharpExtern.so | grep 'not found' && exit 1 || true
COPY --from=backend-build /src/tessdata ./tessdata
COPY --from=backend-build /src/testdata/colas ./testdata/colas
COPY --from=backend-build /src/testdata/layout-annotations ./testdata/layout-annotations
COPY --from=backend-build /src/testdata/layout-models ./testdata/layout-models
COPY --from=frontend-build /frontend/backend/LabelVerification.Api/wwwroot ./wwwroot
# Tesseract NuGet expects native libs under /app/x64 — symlink Ubuntu packages after publish copy.
RUN mkdir -p /app/x64 \
 && ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so \
 && ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.4 /app/x64/libtesseract50.so \
 && ln -sf /lib/x86_64-linux-gnu/libdl.so.2 /app/libdl.so
ENV ASPNETCORE_URLS=http://0.0.0.0:8082
ENV Ocr__TessDataPath=/app/tessdata
ENV Cola__ColasDir=/app/testdata/colas
ENV Layout__AnnotationsDir=/app/testdata/layout-annotations
ENV Layout__ModelPath=/app/testdata/layout-models/label-layout-v1.onnx
EXPOSE 8082
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "LabelVerification.Api.dll"]
