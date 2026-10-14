@echo off
dotnet build Content.Client --configuration Tools
dotnet build Content.Server --configuration Tools

Start "Client" "runclient-Tools.bat"
Start "Server" "runserver-Tools.bat"
