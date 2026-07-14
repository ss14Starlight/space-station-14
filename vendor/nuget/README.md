# Vendored NuGet packages

Committed local feed for packages that are not (yet) on nuget.org.

## SS14.Starlight.NullLink

SolLink fork of the Starlight NullLink contracts (adds `ServerInfo.RoundId`).

Refresh from the SolLink repo:

```bash
# from SolLink
./scripts/install-nulllink-game-pkg.sh /path/to/space-station-14
```

Or manually:

```bash
dotnet pack Starlight.NullLink/src/Starlight.NullLink.csproj -c Release -o artifacts/nuget
cp artifacts/nuget/SS14.Starlight.NullLink.*.nupkg /path/to/space-station-14/vendor/nuget/
# bump Version in Directory.Packages.props to match
```

`nuget.config` maps `SS14.Starlight.NullLink` exclusively to this folder so CI does not need nuget.org for it.
