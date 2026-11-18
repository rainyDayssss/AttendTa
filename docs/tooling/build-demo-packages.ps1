param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    throw 'Output directory must not exist; existing files are never replaced.'
}
$parent = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'Output parent directory must already exist.'
}
$repoFull = [IO.Path]::GetFullPath($repo).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($output.Equals($repoFull, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($repoFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be outside the source checkout.'
}
$revision = (& git -C $repo rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') {
    throw 'Cannot identify the source revision; package build refused.'
}
$changes = @(& git -C $repo status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect working-tree state; package build refused.' }
$sourceState = if ($changes.Count -eq 0) { 'clean' } else { 'dirty' }
New-Item -ItemType Directory -Path $output | Out-Null
$project = Join-Path $repo 'ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj'
$targets = @(
    @{ Name = 'attendta-win-x64'; Runtime = 'win-x64'; SelfContained = 'true' },
    @{ Name = 'attendta-linux-x64'; Runtime = 'linux-x64'; SelfContained = 'true' },
    @{ Name = 'attendta-portable'; Runtime = ''; SelfContained = 'false' }
)
foreach ($target in $targets) {
    $directory = Join-Path $output $target.Name
    $artifacts = Join-Path $output ("artifacts-" + $target.Name)
    $arguments = @('publish', $project, '--configuration', 'Release', '--artifacts-path', $artifacts, '--output', $directory,
        '--self-contained', $target.SelfContained)
    if ($target.Runtime) { $arguments += @('--runtime', $target.Runtime) }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $($target.Name); output may be incomplete." }
    if ($target.Runtime -eq 'win-x64') {
        $launcherProject = Join-Path $repo 'docs/tooling/DemoLauncher/DemoLauncher.csproj'
        $launcherOutput = Join-Path $output 'launcher-build'
        & dotnet publish $launcherProject --configuration Release --runtime win-x64 --self-contained true --artifacts-path (Join-Path $output 'artifacts-launcher') `
            -p:PublishSingleFile=true --output $launcherOutput
        if ($LASTEXITCODE -ne 0) { throw 'Windows launcher build failed; output may be incomplete.' }
        Copy-Item -LiteralPath (Join-Path $launcherOutput 'AttendTa-Demo.exe') -Destination $directory
    }
    foreach ($required in @('FIRST-TIME-SETUP.md', 'demo/attendta-mvp-schema.sql', 'demo/venue-1.png', 'demo/venue-2.png', 'demo/venue-3.png', 'demo/venue-4.png', 'demo/venue-cebu.png', 'demo/event-1.png', 'demo/event-2.png', 'demo/event-3.png', 'demo/event-4.png', 'demo/event-5.png', 'demo/event-6.png', 'demo/event-7.png', 'demo/event-8.png', 'demo/ASSET-LICENSE.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory $required) -PathType Leaf)) {
            throw "Required demo asset missing for $($target.Name)."
        }
    }
    Compress-Archive -Path (Join-Path $directory '*') -DestinationPath (Join-Path $output ($target.Name + '.zip'))
}
$bundleProject = Join-Path $repo 'docs/tooling/DemoBundle/DemoBundle.csproj'
$bundleOutput = Join-Path $output 'bundle-build'
$windowsArchive = Join-Path $output 'attendta-win-x64.zip'
& dotnet publish $bundleProject --configuration Release --runtime win-x64 --self-contained true --artifacts-path (Join-Path $output 'artifacts-bundle') `
    -p:PublishSingleFile=true "-p:DemoPackagePath=$windowsArchive" --output $bundleOutput
if ($LASTEXITCODE -ne 0) { throw 'Standalone Windows bundle build failed; output may be incomplete.' }
Copy-Item -LiteralPath (Join-Path $bundleOutput 'AttendTa-Demo-Standalone.exe') -Destination $output
@(
    "source_revision=$revision"
    "working_tree=$sourceState"
    'A dirty worktree is not identified by source_revision; preserve its exact diff separately.'
) | Set-Content -LiteralPath (Join-Path $output 'BUILD-INFO.txt') -Encoding utf8NoBOM
$checksums = foreach ($target in $targets) {
    $file = Join-Path $output ($target.Name + '.zip')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
$bundle = Join-Path $output 'AttendTa-Demo-Standalone.exe'
$checksums += '{0}  {1}' -f (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $bundle -Leaf)
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output 'Packages created locally. They are not tested or published releases.'
