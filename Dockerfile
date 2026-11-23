# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY Harrbor.slnx ./
COPY src/Harrbor/Harrbor.csproj src/Harrbor/

# Restore dependencies
RUN dotnet restore src/Harrbor/Harrbor.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/Harrbor/Harrbor.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Build-time metadata (set via --build-arg)
ARG VERSION=dev
ARG COMMIT_SHA=unknown

# OCI image labels
LABEL org.opencontainers.image.title="Harrbor" \
      org.opencontainers.image.description="Media orchestration service for seedbox-to-homelab transfers" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${COMMIT_SHA}" \
      org.opencontainers.image.source="https://github.com/ttocskcaj/harrbor" \
      org.opencontainers.image.licenses="MIT"

WORKDIR /app

# dovi_tool (Dolby Vision RPU tool used by the DoviP7ToP81 converter): pinned static musl build
# from https://github.com/quietvoid/dovi_tool/releases, verified by sha256.
ARG TARGETARCH
ARG DOVI_TOOL_VERSION=2.3.4
ARG DOVI_TOOL_SHA256_AMD64=1844258e13c26607b32224bf1fa82b595d3b35949f5467405fda560daad32b3f
ARG DOVI_TOOL_SHA256_ARM64=b4f22a7db56954efe4ed8d02276d0f991799e84602f3584711be6c9df950cb15
RUN set -eu; \
    case "${TARGETARCH:-amd64}" in \
      amd64) triple=x86_64-unknown-linux-musl;  sha="${DOVI_TOOL_SHA256_AMD64}" ;; \
      arm64) triple=aarch64-unknown-linux-musl; sha="${DOVI_TOOL_SHA256_ARM64}" ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    asset="dovi_tool-${DOVI_TOOL_VERSION}-${triple}.tar.gz"; \
    wget -q -O "/tmp/${asset}" "https://github.com/quietvoid/dovi_tool/releases/download/${DOVI_TOOL_VERSION}/${asset}"; \
    echo "${sha}  /tmp/${asset}" | sha256sum -c -; \
    mkdir /tmp/dovi_tool; \
    tar -xzf "/tmp/${asset}" -C /tmp/dovi_tool; \
    install -m 755 /tmp/dovi_tool/dovi_tool /usr/local/bin/dovi_tool; \
    rm -rf "/tmp/${asset}" /tmp/dovi_tool; \
    dovi_tool --version

# Install dependencies and configure user in single layer
# (ffmpeg/ffprobe + mkvtoolnix are used by media converters)
RUN apk add --no-cache rclone shadow su-exec tzdata ffmpeg mkvtoolnix \
    && addgroup -g 1000 harrbor \
    && adduser -u 1000 -G harrbor -h /home/harrbor -s /bin/sh -D harrbor \
    # Create .ssh directory for mounted SSH keys
    && mkdir -p /home/harrbor/.ssh \
    && chmod 700 /home/harrbor/.ssh \
    # Configure SSH to skip host key verification (keys mounted at runtime)
    && echo -e "Host *\n    StrictHostKeyChecking accept-new\n    UserKnownHostsFile /home/harrbor/.ssh/known_hosts" \
       > /home/harrbor/.ssh/config \
    && chmod 600 /home/harrbor/.ssh/config \
    && chown -R harrbor:harrbor /home/harrbor \
    # Create app directories
    && mkdir -p /app/data /app/logs \
    && chown -R harrbor:harrbor /app

# Copy published app
COPY --from=build /app/publish .

# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Expose health check port
EXPOSE 8080

# Configure environment (PUID/PGID/TZ like linuxserver images)
ENV PUID=1000
ENV PGID=1000
ENV TZ=UTC
ENV HOME=/home/harrbor
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

# Graceful shutdown signal
STOPSIGNAL SIGTERM

ENTRYPOINT ["/entrypoint.sh"]
