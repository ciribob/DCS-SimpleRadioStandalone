# Use Ubuntu 24.04 LTS as the base image
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive

# Update package lists and install required runtime dependencies
# --no-install-recommends: Only install essential packages to keep image size small
# The packages installed are:
#   - libstdc++6: C++ standard library (required for C++ applications)
#   - libgcc-s1: GCC runtime library (required for compiled applications)
#   - libicu74: Unicode support library (for internationalization)
#   - ca-certificates: SSL/TLS certificate authorities (for secure connections)
# Clean up package cache to reduce image size
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libstdc++6 \
        libgcc-s1 \
        libicu74 \
        ca-certificates && \
    apt-get clean && \ 
    rm -rf /var/lib/apt/lists/*

# Set the working directory inside the container
# All subsequent commands will be executed from this directory
WORKDIR /opt/srs

# Copy the SRS server Linux CLI executable and startup script from the build context
# The source binary path was choosed to be future proof if the binary build become part of the same pipeline
# In order to build the container image properly, the docker build or pipeline must run in the context of the root of the repository
COPY ./install-build/ServerCommandLine-Linux/SRS-Server-Commandline .

# Make the copied files executable
# This is necessary because file permissions may not be preserved during COPY
RUN chmod +x SRS-Server-Commandline

# Define the default command to run when the container starts
# The entrypoint script will handle the application startup
# Set the default command to run the server
ENTRYPOINT ["./SRS-Server-Commandline"]
