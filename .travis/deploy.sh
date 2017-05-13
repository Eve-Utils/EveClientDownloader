dotnet pack EveClientDownloader.csproj --configuration Release --output . --no-build && \
dotnet nuget -v Verbose push EveClientDownloader.*.nupkg -k "$NUGET_API_KEY" -s "$NUGET_SOURCE"
