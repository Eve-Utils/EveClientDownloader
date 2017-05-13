sed -i -e "s|<Version>1.0.0</Version>|<Version>"$(git describe)"</Version>|" EveClientDownloader.csproj

dotnet restore EveClientDownloader.csproj
dotnet build --configuration Release EveClientDownloader.csproj
