dotnet restore

dotnet build --configuration Debug
dotnet build --configuration Release

dotnet test -c Debug .\test\TauCode.Jobs.Tests\TauCode.Jobs.Tests.csproj
dotnet test -c Release .\test\TauCode.Jobs.Tests\TauCode.Jobs.Tests.csproj

nuget pack nuget\TauCode.Jobs.nuspec