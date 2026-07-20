[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$chineseResourcePath = Join-Path $repositoryRoot 'Strings\zh-Hans\Resources.resw'
$englishResourcePath = Join-Path $repositoryRoot 'Strings\en-US\Resources.resw'
$storefrontCatalogPath = Join-Path $repositoryRoot 'IPAbuyer.Core\Configuration\AppleStorefront.cs'

function Get-ResourceMap {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($Path)
    $resourceMap = @{}

    foreach ($data in $xml.root.data) {
        if ($resourceMap.ContainsKey($data.name)) {
            throw "Duplicate resource key '$($data.name)' in $Path."
        }

        $resourceMap[$data.name] = [string]$data.value
    }

    return $resourceMap
}

function Get-FormatTokenSignature {
    param(
        [string]$Value
    )

    return [string]::Join('|', @([regex]::Matches($Value, '\{\d+(?:[^}]*)\}') | ForEach-Object Value | Sort-Object))
}

$chineseResources = Get-ResourceMap -Path $chineseResourcePath
$englishResources = Get-ResourceMap -Path $englishResourcePath

$missingEnglishKeys = @($chineseResources.Keys | Where-Object { -not $englishResources.ContainsKey($_) })
$extraEnglishKeys = @($englishResources.Keys | Where-Object { -not $chineseResources.ContainsKey($_) })
if ($missingEnglishKeys.Count -gt 0 -or $extraEnglishKeys.Count -gt 0) {
    throw "Resource key mismatch. Missing English: $($missingEnglishKeys -join ', '). Extra English: $($extraEnglishKeys -join ', ')."
}

foreach ($key in $chineseResources.Keys) {
    $chineseTokens = Get-FormatTokenSignature -Value $chineseResources[$key]
    $englishTokens = Get-FormatTokenSignature -Value $englishResources[$key]
    if ($chineseTokens -ne $englishTokens) {
        throw "Format placeholder mismatch for '$key'."
    }
}

$storefrontCodes = @(
    Get-Content -LiteralPath $storefrontCatalogPath |
        ForEach-Object {
            if ($_ -match '^([A-Z]{2})\|') {
                $Matches[1]
            }
        }
)

if (($storefrontCodes | Select-Object -Unique).Count -ne $storefrontCodes.Count) {
    throw 'Duplicate storefront code in AppleStorefrontCatalog.'
}

foreach ($code in $storefrontCodes) {
    foreach ($resourceMap in @($chineseResources, $englishResources)) {
        if (-not $resourceMap.ContainsKey("Storefront/$code")) {
            throw "Missing Storefront/$code resource."
        }
    }
}

Write-Output "Localization resource validation passed: $($chineseResources.Count) keys, $($storefrontCodes.Count) storefronts."
