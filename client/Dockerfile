# Unity Headless Build for Linux Server
FROM ubuntu:22.04

# Install dependencies
RUN apt-get update && apt-get install -y \
    libglib2.0-0 \
    libnss3 \
    libnspr4 \
    libdbus-1-3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpulse0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy build output
COPY build/StandaloneLinux64/MegaGame_Data ./MegaGame_Data
COPY build/StandaloneLinux64/MegaGame ./MegaGame

# Make executable
RUN chmod +x ./MegaGame

# Create non-root user
RUN adduser -D -g '' appuser
USER appuser

# Expose ports (if needed for multiplayer)
EXPOSE 7777 7778

# Run headless
ENTRYPOINT ["./MegaGame"]
CMD ["-batchmode", "-nographics", "-logFile", "-"]