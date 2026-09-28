param(
    [string]$QueuePath = 'data/business-signals-review-queue.json',
    [string]$LabelsPath = 'data/business-signals-reviewed.json',
    [string]$OutputPath = '.tmp/business-review-sources.json',
    [switch]$PendingOnly
)

$ErrorActionPreference = 'Stop'
$queue = Get-Content -LiteralPath $QueuePath -Raw | ConvertFrom-Json
$labels = Get-Content -LiteralPath $LabelsPath -Raw | ConvertFrom-Json
$reviewed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($label in $labels.labels) { [void]$reviewed.Add($label.externalId) }
$selected = if ($PendingOnly) {
    @($queue.entries | Where-Object { -not $reviewed.Contains($_.externalId) })
} else {
    @($queue.entries)
}

$results = $selected | ForEach-Object -Parallel {
    $item = $_
    try {
        $response = Invoke-WebRequest -Uri $item.officialUrl -TimeoutSec 30 -UseBasicParsing
        $html = $response.Content
        $start = $html.IndexOf('<div id="textoxslt">')
        if ($start -lt 0) { throw 'Falta el bloque de texto oficial.' }
        $end = $html.IndexOf('<!--', $start)
        if ($end -lt 0) { throw 'Falta el cierre del bloque de texto oficial.' }
        $text = $html.Substring($start, $end - $start)
        $text = [regex]::Replace($text, '<[^>]+>', ' ')
        $text = [System.Net.WebUtility]::HtmlDecode($text)
        $text = [regex]::Replace($text, '\s+', ' ').Trim()
        if ($text.Length -lt 30) { throw 'Texto oficial anormalmente corto.' }
        $digest = [System.Security.Cryptography.SHA256]::HashData(
            [System.Text.Encoding]::UTF8.GetBytes($text))
        [pscustomobject]@{
            externalId = $item.externalId
            publicationDate = $item.publicationDate
            sectionCode = $item.sectionCode
            stratum = $item.stratum
            title = $item.title
            officialUrl = $item.officialUrl
            textSha256 = [Convert]::ToHexString($digest).ToLowerInvariant()
            text = $text
            error = $null
        }
    } catch {
        [pscustomobject]@{
            externalId = $item.externalId
            publicationDate = $item.publicationDate
            sectionCode = $item.sectionCode
            stratum = $item.stratum
            title = $item.title
            officialUrl = $item.officialUrl
            textSha256 = $null
            text = $null
            error = $_.Exception.Message
        }
    }
} -ThrottleLimit 4

$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$failed = @($results | Where-Object { $_.error }).Count
Write-Output "Fuentes consultadas: $($results.Count); con texto: $($results.Count - $failed); fallidas: $failed"
Write-Output "Copia temporal: $OutputPath"
