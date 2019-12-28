cd /d %~dp0

dotnet publish -r win-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -o:.\Publish\SelfContained\win
dotnet publish -r win-x64 -c Release -p:PublishSingleFile=true --no-self-contained -o:.\Publish\FrameworkDependentSingleFile\win

dotnet publish -r linux-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -o:.\Publish\SelfContained\linux
dotnet publish -r linux-x64 -c Release -p:PublishSingleFile=true --no-self-contained -o:.\Publish\FrameworkDependentSingleFile\linux

dotnet publish -r osx-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -o:.\Publish\SelfContained\osx
dotnet publish -r osx-x64 -c Release -p:PublishSingleFile=true --no-self-contained -o:.\Publish\FrameworkDependentSingleFile\osx

dotnet publish -c Release -o:.\Publish\CrossPlatform
pause