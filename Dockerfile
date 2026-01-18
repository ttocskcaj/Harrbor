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
WORKDIR /app

# Install rclone
RUN apk add --no-cache rclone

# Create non-root user
RUN addgroup -g 1002 harrbor && \
    adduser -u 998 -G harrbor -s /bin/sh -D harrbor

# Create directories
RUN mkdir -p /app/data /app/logs /app/staging && \
    chown -R harrbor:harrbor /app

# Copy published app
COPY --from=build /app/publish .

# Switch to non-root user
USER harrbor

# Expose health check port
EXPOSE 8080

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Harrbor.dll"]
