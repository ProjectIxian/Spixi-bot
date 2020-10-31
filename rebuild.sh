#!/bin/sh -e
echo Rebuilding Spixi Bot...
echo Cleaning previous build
msbuild SpixiBot.sln /p:Configuration=Release /target:Clean
echo Removing packages
rm -rf packages
echo Restoring packages
nuget restore SpixiBot.sln
echo Building Spixi Bot
msbuild SpixiBot.sln /p:Configuration=Release
echo Done rebuilding Spixi Bot