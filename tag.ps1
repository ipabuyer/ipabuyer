$ErrorActionPreference = "Stop"

$manifest = [xml](Get-Content Package.appxmanifest)
$ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace("d", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$version = $manifest.SelectSingleNode("//d:Identity/@Version", $ns).Value

$tag = "v$version"
Write-Host "Version: $version"
Write-Host "Tag: $tag"

$confirm = Read-Host "Proceed? (y/N)"
if ($confirm -ne 'y') {
    Write-Host "Aborted."
    exit 1
}

git tag -a $tag -m "Release $tag"
git push origin $tag

Write-Host "Done. Tag $tag pushed."
