

dotnet clean -c Release ../Chloe/Chloe.csproj
dotnet build -c Release ../Chloe/Chloe.csproj

dotnet clean -c Release ../Chloe.Extension/Chloe.Extension.csproj
dotnet build -c Release ../Chloe.Extension/Chloe.Extension.csproj

dotnet clean -c Release ../Chloe.Dameng/Chloe.Dameng.csproj
dotnet build -c Release ../Chloe.Dameng/Chloe.Dameng.csproj

dotnet clean -c Release ../Chloe.KingbaseES/Chloe.KingbaseES.csproj
dotnet build -c Release ../Chloe.KingbaseES/Chloe.KingbaseES.csproj

dotnet clean -c Release ../Chloe.MySql/Chloe.MySql.csproj
dotnet build -c Release ../Chloe.MySql/Chloe.MySql.csproj

dotnet clean -c Release ../Chloe.Oracle/Chloe.Oracle.csproj
dotnet build -c Release ../Chloe.Oracle/Chloe.Oracle.csproj

dotnet clean -c Release ../Chloe.PostgreSQL/Chloe.PostgreSQL.csproj
dotnet build -c Release ../Chloe.PostgreSQL/Chloe.PostgreSQL.csproj

dotnet clean -c Release ../Chloe.SQLite/Chloe.SQLite.csproj
dotnet build -c Release ../Chloe.SQLite/Chloe.SQLite.csproj

dotnet clean -c Release ../Chloe.SonnetDB/Chloe.SonnetDB.csproj
dotnet build -c Release ../Chloe.SonnetDB/Chloe.SonnetDB.csproj

dotnet clean -c Release ../Chloe.SqlServer/Chloe.SqlServer.csproj
dotnet build -c Release ../Chloe.SqlServer/Chloe.SqlServer.csproj


del /f /s /q packages\*
rd packages
md packages

.\nuget pack Chloe.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.Extension.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.Dameng.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.KingbaseES.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.MySql.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.Oracle.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.PostgreSQL.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.SQLite.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.SonnetDB.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion
.\nuget pack Chloe.SqlServer.nuspec -OutputDirectory ./packages -OutputFileNamesWithoutVersion

pause
