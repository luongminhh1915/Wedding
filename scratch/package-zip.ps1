$source = "client/dist"
$targetDir = "artifacts/ui-patch-20261103/wwwroot"
$zipPath = "artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip"

if (Test-Path $targetDir) {
    Remove-Item -Recurse -Force $targetDir
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Recurse -Force "$source/*" $targetDir

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path "$targetDir/*" -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Successfully updated $zipPath with latest build!"

