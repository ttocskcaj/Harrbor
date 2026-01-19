#!/bin/sh
set -e

PUID=${PUID:-1000}
PGID=${PGID:-1000}

# Adjust group and user IDs to match environment
echo "Setting harrbor GID:$PGID UID:$PUID"
groupmod -o -g "$PGID" harrbor
usermod -o -u "$PUID" harrbor

# Fix ownership of app directories
chown -R harrbor:harrbor /app/data /app/logs /home/harrbor

# Drop privileges and run the app
exec su-exec harrbor:harrbor dotnet Harrbor.dll "$@"
