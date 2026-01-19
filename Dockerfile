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

# Install dependencies and configure user in single layer
RUN apk add --no-cache rclone shadow su-exec tzdata \
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
