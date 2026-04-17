# DataCrud.DBOps - NuGet Packaging Script
# This script builds the solution in Release mode and generates NuGet packages.
param(
    [string]$Version = $null
)

$ErrorActionPreference = "Stop"

$solutionFile = "Source\DataCrud.DBOps.sln"
$outputDir = "nupkg"

if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir
}

Write-Host "Cleaning output directory..." -ForegroundColor Cyan
Remove-Item "$outputDir\*" -Force -ErrorAction SilentlyContinue

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore $solutionFile

$buildArgs = @("-c", "Release", "--no-restore")
if ($Version) {
    Write-Host "Overriding version to $Version..." -ForegroundColor Yellow
    $buildArgs += "/p:Version=$Version"
}

Write-Host "Building solution in Release mode..." -ForegroundColor Cyan
dotnet build $solutionFile @buildArgs

Write-Host "Packing projects..." -ForegroundColor Cyan
# List of projects to pack (libs only)
$projects = Get-ChildItem -Path Source\DataCrud.DBOps.* -Filter *.csproj -Recurse

foreach ($project in $projects) {
    if ($project.Name -contains "Tests" -or $project.Name -contains "Sample") {
        continue
    }
    
    Write-Host "Packing $($project.BaseName)..." -ForegroundColor Yellow
    $packArgs = @($project.FullName, "-c", "Release", "-o", $outputDir, "--no-build")
    if ($Version) {
        $packArgs += "/p:Version=$Version"
    }
    dotnet pack @packArgs
}

Write-Host "`nSuccessfully generated NuGet packages in $outputDir directory." -ForegroundColor Green
Write-Host "To push to NuGet.org (after setting up API key):" -ForegroundColor Cyan
Write-Host "dotnet nuget push $outputDir\*.nupkg -k <YOUR_API_KEY> -s https://api.nuget.org/v3/index.json" -ForegroundColor Gray
