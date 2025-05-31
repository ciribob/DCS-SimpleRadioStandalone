#!/bin/bash
set -e

outputPath="./install-build"

# Server Command Line - Linux
echo "Publishing ServerCommandLine for Linux..."

rm -rf "$outputPath/ServerCommandLine-Linux"
dotnet clean "./ServerCommandLine/ServerCommandLine.csproj"
dotnet publish "./ServerCommandLine/ServerCommandLine.csproj" \
    --framework net8.0 \
    --runtime linux-x64 \
    --output "$outputPath/ServerCommandLine-Linux" \
    --self-contained true \
    --configuration Release \
    /p:PublishReadyToRun=true \
    /p:PublishSingleFile=true \
    /p:DebugType=None \
    /p:DebugSymbols=false \
    /p:IncludeSourceRevisionInInformationalVersion=false \
    /p:SourceLinkCreate=false

find "$outputPath/ServerCommandLine-Linux" -name '*.dll' -delete