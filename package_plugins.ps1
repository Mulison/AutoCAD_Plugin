param(
    [string]$ArtifactsDir = "artifacts",
    [string]$BuildNumber = "1"
)

Set-Location $ArtifactsDir
Get-ChildItem -Directory | ForEach-Object {
    $pluginName = $_.Name
    $zipName = "${pluginName}_v${BuildNumber}.zip"
    Write-Host "Creating package for $pluginName..."
    Compress-Archive -Path $pluginName -DestinationPath $zipName -Force
    Write-Host "Created: $zipName"
}
