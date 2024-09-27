#!/bin/sh -e
echo Rebuilding Spixi Bot...
echo Cleaning previous build
dotnet clean --configuration Release
echo Restoring packages
dotnet restore
echo Building Spixi Bot
dotnet build --configuration Release
echo Done rebuilding Spixi Bot