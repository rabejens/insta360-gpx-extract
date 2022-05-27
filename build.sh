#!/bin/sh

dotnet publish -c Release Insta360GPXExtract
mkdir -p build/fw && rm -rf build/fw/* && cp -Rf Insta360GPXExtract/bin/Release/net6.0/publish/* build/fw/

for n in win-x86 win-x64 linux-x64 osx-x64 osx-arm64; do
	dotnet publish -c Release -r $n --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
	mkdir -p build/$n
	if [ -f Insta360GPXExtract/bin/Release/net6.0/$n/publish/Insta360GPXExtract.exe ]; then
		cp Insta360GPXExtract/bin/Release/net6.0/$n/publish/Insta360GPXExtract.exe build/$n
	else
		Insta360GPXExtract/bin/Release/net6.0/$n/publish/Insta360GPXExtract build/$n
	fi
done
