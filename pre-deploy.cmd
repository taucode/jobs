dotnet restore

dotnet build TauCode.Jobs.sln -c Debug
dotnet build TauCode.Jobs.sln -c Release

dotnet test TauCode.Jobs.sln -c Debug
dotnet test TauCode.Jobs.sln -c Release

nuget pack nuget\TauCode.Jobs.nuspec