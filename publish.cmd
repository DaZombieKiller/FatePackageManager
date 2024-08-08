@echo off
dotnet publish -c Release -p:PublishAOT=true -p:DebugType=none
pause
