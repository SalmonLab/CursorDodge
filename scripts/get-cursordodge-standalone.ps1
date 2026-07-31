param(
    [string]$TargetDir = (Join-Path $PSScriptRoot '..\portable'),
    [switch]$RunAfterDownload
)

$RepoOwner = 'SalmonLab'
$RepoName = 'CursorDodge'
$ApiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
$Headers = @{ 'User-Agent' = 'CursorDodge-SetupScript' }

function Download-File
{
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    Write-Host "Download: $Url"
    Invoke-WebRequest -Uri $Url -Headers $Headers -OutFile $OutputPath -UseBasicParsing
}

try
{
    $release = Invoke-RestMethod -Uri $ApiUrl -Headers $Headers
}
catch
{
    throw 'Failed to query GitHub Releases API. Confirm network and repository visibility.'
}

$candidates = @(
    'CursorDodge-Portable.zip',
    'CursorDodge.zip',
    'CursorDodge.exe'
)

$asset = $release.assets | Where-Object { $candidates -contains $_.name } | Select-Object -First 1
if (-not $asset)
{
    $asset = $release.assets | Where-Object { $_.name -match 'CursorDodge' -and $_.name -match '\.(exe|zip)$' } | Select-Object -First 1
}

if (-not $asset)
{
    throw 'CursorDodge distribution asset not found in release. Please create a GitHub release containing CursorDodge.exe or CursorDodge-Portable.zip.'
}

$target = Resolve-Path -Path $TargetDir -ErrorAction SilentlyContinue
if (-not $target)
{
    $null = New-Item -ItemType Directory -Path $TargetDir -Force
}
$targetDirPath = (Resolve-Path -Path $TargetDir).Path

$downloadPath = Join-Path $targetDirPath $asset.name
Download-File -Url $asset.browser_download_url -OutputPath $downloadPath

if ($asset.name -like '*.zip')
{
    Write-Host "Expand archive: $downloadPath"
    Expand-Archive -Path $downloadPath -DestinationPath $targetDirPath -Force
    Remove-Item $downloadPath -Force
}

$exePath = Get-ChildItem -Path $targetDirPath -Filter 'CursorDodge.exe' -Recurse -File | Select-Object -First 1 | Select-Object -ExpandProperty FullName
if (-not $exePath)
{
    throw 'CursorDodge.exe was not found in the downloaded package. Check release artifact content.'
}

try
{
    Unblock-File -Path $exePath -ErrorAction SilentlyContinue
}
catch
{
}

Write-Host "Downloaded: $exePath"

if ($RunAfterDownload)
{
    Start-Process -FilePath $exePath
}
