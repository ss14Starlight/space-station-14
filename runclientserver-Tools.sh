dotnet build Content.Client --configuration Tools
dotnet build Content.Server --configuration Tools

./runclient-Tools.sh&
./runserver-Tools.sh
