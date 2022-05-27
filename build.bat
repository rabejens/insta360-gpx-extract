@echo off

if not exist build md build
if not exist build\fw md build\fw
if not exist build\win-x86 md build\win-x86
if not exist build\win-x64 md build\win-x64
if not exist build\linux-x64 md build\linux-x64
if not exist build\osx-x64 md build\osx-x64
if not exist build\osx-arm64 md build\osx-arm64

dotnet publish -c Release Insta360GPXExtract
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract

robocopy /mir Insta360GPXExtract\bin\Release\net6.0\publish\ build\fw\
copy Insta360GPXExtract\bin\Release\net6.0\win-x86\publish\*.exe build\win-x86
copy Insta360GPXExtract\bin\Release\net6.0\win-x64\publish\*.exe build\win-x64
copy Insta360GPXExtract\bin\Release\net6.0\linux-x64\publish\Insta360GPXExtract build\linux-x64
copy Insta360GPXExtract\bin\Release\net6.0\osx-x64\publish\Insta360GPXExtract build\osx-x64
copy Insta360GPXExtract\bin\Release\net6.0\osx-arm64\publish\Insta360GPXExtract build\osx-arm64
