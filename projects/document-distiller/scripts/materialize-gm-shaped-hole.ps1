[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\outputs')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function Get-Utf8Text {
    param([Parameter(Mandatory)][string]$Uri)

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $client.DefaultRequestHeaders.UserAgent.ParseAdd('DocumentDistiller')
        $bytes = $client.GetByteArrayAsync($Uri).GetAwaiter().GetResult()
        return [System.Text.Encoding]::UTF8.GetString($bytes)
    }
    finally {
        $client.Dispose()
    }
}

function Select-Ranges {
    param(
        [Parameter(Mandatory)][string]$Content,
        [object[]]$Ranges
    )

    if ($null -eq $Ranges -or $Ranges.Count -eq 0) {
        return $Content
    }

    $parts = foreach ($range in $Ranges) {
        $start = $Content.IndexOf(
            [string]$range.Start,
            [System.StringComparison]::Ordinal)
        if ($start -lt 0) {
            throw "Start marker '$($range.Start)' was not found."
        }

        if ([string]::IsNullOrEmpty([string]$range.End)) {
            $end = $Content.Length
        }
        else {
            $end = $Content.IndexOf(
                [string]$range.End,
                $start + ([string]$range.Start).Length,
                [System.StringComparison]::Ordinal)
            if ($end -lt 0) {
                throw "End marker '$($range.End)' was not found."
            }
        }

        $Content.Substring($start, $end - $start).Trim()
    }

    return ($parts -join "`n`n---`n`n")
}

function Repair-Mojibake {
    param([Parameter(Mandatory)][string]$Content)

    $hasMojibake = $Content.IndexOf([char]0x00E2) -ge 0 -or
        $Content.IndexOf([char]0x00C3) -ge 0 -or
        $Content.IndexOf([char]0x00C2) -ge 0
    if (-not $hasMojibake) {
        return $Content
    }

    $windows1252 = [System.Text.Encoding]::GetEncoding(1252)
    return [System.Text.Encoding]::UTF8.GetString(
        $windows1252.GetBytes($Content))
}

$sources = @(
    @{
        File = '01-dnd-rhythm-exploration-and-combat.md'
        Title = 'D&D SRD: Rhythm, Exploration, Combat, and Defeat'
        Url = 'https://www.dndbeyond.com/sources/dnd/br-2024/playing-the-game'
        FetchUrl = 'https://r.jina.ai/http://www.dndbeyond.com/sources/dnd/br-2024/playing-the-game'
        License = 'Material corresponding to SRD 5.2.1, CC-BY-4.0'
        RepairEncoding = $true
        MaxChars = 22000
        Ranges = @(
            @{ Start = '## Playing the Game'; End = '## An Ongoing Game' }
            @{ Start = '## Actions'; End = '## Exploration' }
            @{ Start = '## Exploration'; End = '## Combat' }
            @{ Start = '## Combat'; End = '### Movement and Position' }
            @{ Start = '### Dropping to 0 Hit Points'; End = '### Temporary Hit Points' }
        )
    }
    @{
        File = '02-dnd-encounters-environments-and-traps.md'
        Title = 'D&D SRD: Encounters, Environments, and Traps'
        Url = 'https://www.dndbeyond.com/sources/dnd/br-2024/dms-toolbox'
        FetchUrl = 'https://r.jina.ai/http://www.dndbeyond.com/sources/dnd/br-2024/dms-toolbox'
        License = 'Material corresponding to SRD 5.2.1, CC-BY-4.0'
        RepairEncoding = $true
        MaxChars = 22000
        Ranges = @(
            @{ Start = '## Combat'; End = '## Curses' }
            @{ Start = '## Environmental Effects'; End = '## Fear' }
            @{ Start = '## Traps'; End = '### Example Traps' }
            @{ Start = '#### Hidden Pit'; End = '#### Poisoned Darts' }
        )
    }
    @{
        File = '03-pathfinder-running-encounters.md'
        Title = 'Pathfinder 2e GM Core: Running Encounters'
        Url = 'https://2e.aonprd.com/Rules.aspx?ID=2538&NoRedirect=1'
        FetchUrl = 'https://r.jina.ai/http://2e.aonprd.com/Rules.aspx?ID=2538&NoRedirect=1'
        License = 'Paizo Open RPG Creative (ORC) licensed rules material'
        RepairEncoding = $true
        MaxChars = 24000
        Ranges = @(
            @{ Start = '* * *'; End = '## [Starting the Encounter]' }
            @{ Start = '## [Running the Encounter]'; End = '### [Maps and Miniatures]' }
            @{ Start = '## [Ending the Encounter]'; End = '' }
        )
    }
    @{
        File = '04-pathfinder-encounter-design.md'
        Title = 'Pathfinder 2e GM Core: Encounter Design'
        Url = 'https://2e.aonprd.com/Rules.aspx?ID=2715&NoRedirect=1'
        FetchUrl = 'https://r.jina.ai/http://2e.aonprd.com/Rules.aspx?ID=2715&NoRedirect=1'
        License = 'Paizo Open RPG Creative (ORC) licensed rules material'
        RepairEncoding = $true
        MaxChars = 24000
        Ranges = @(
            @{ Start = '* * *'; End = '## [Combat Threats]' }
            @{ Start = '## [Combat Threats]'; End = '## [Encounter Locations]' }
            @{ Start = '## [Encounter Locations]'; End = '## [Social Encounters]' }
        )
    }
    @{
        File = '05-gemrb-engine-overview.md'
        Title = 'GemRB Engine Overview'
        Url = 'https://gemrb.org/Engine-overview.html'
        FetchUrl = 'https://r.jina.ai/http://gemrb.org/Engine-overview.html'
        License = 'GemRB open-source project documentation'
        RepairEncoding = $true
        MaxChars = 12000
        Ranges = @()
    }
    @{
        File = '06-flare-power-definitions.md'
        Title = 'Flare Engine: Power and Hazard Definitions'
        Url = 'https://github.com/flareteam/flare-engine/wiki/Power-Definitions'
        FetchUrl = 'https://raw.githubusercontent.com/wiki/flareteam/flare-engine/Power-Definitions.md'
        License = 'Flare Engine project documentation, GPL-3.0 project'
        MaxChars = 12000
        Ranges = @()
    }
    @{
        File = '07-flare-map-events.md'
        Title = 'Flare Engine: Maps, Events, Enemies, and Traps'
        Url = 'https://github.com/flareteam/flare-engine/wiki/Map-Files'
        FetchUrl = 'https://raw.githubusercontent.com/wiki/flareteam/flare-engine/Map-Files.md'
        License = 'Flare Engine project documentation, GPL-3.0 project'
        MaxChars = 10000
        Ranges = @()
    }
    @{
        File = '08-opentemple-runtime-notes.md'
        Title = 'OpenTemple Runtime, Testing, and Data Overrides'
        Url = 'https://github.com/GrognardsFromHell/OpenTemple'
        FetchUrl = 'https://raw.githubusercontent.com/GrognardsFromHell/OpenTemple/master/README.md'
        License = 'OpenTemple open-source project documentation'
        MaxChars = 8000
        Ranges = @()
    }
)

$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'")
$discoveryId = [Guid]::NewGuid().ToString('N')
$discoveryDirectory = Join-Path (
    [System.IO.Path]::GetFullPath($OutputRoot)
) "${timestamp}_curated_$($discoveryId.Substring(0, 8))"
$corpusDirectory = Join-Path $discoveryDirectory 'corpus'
[System.IO.Directory]::CreateDirectory($corpusDirectory) | Out-Null

$totalCharacters = 0
$manifestSources = foreach ($source in $sources) {
    $raw = Get-Utf8Text -Uri $source.FetchUrl
    if ($source.RepairEncoding) {
        $raw = Repair-Mojibake -Content $raw
    }
    $selected = Select-Ranges -Content $raw -Ranges $source.Ranges
    $truncated = $selected.Length -gt $source.MaxChars
    if ($truncated) {
        $selected = $selected.Substring(0, $source.MaxChars) +
            "`n`n[Source truncated by curated corpus policy.]"
    }

    $document = @"
# $($source.Title)

- Source URL: $($source.Url)
- Retrieval URL: $($source.FetchUrl)
- License note: $($source.License)
- Selection: curated sections relevant to meaningful danger and adjudication.

---

$($selected.Trim())
"@

    $path = Join-Path $corpusDirectory $source.File
    [System.IO.File]::WriteAllText(
        $path,
        $document,
        [System.Text.UTF8Encoding]::new($false))
    $totalCharacters += $document.Length
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes($document))
    }
    finally {
        $sha256.Dispose()
    }

    [ordered]@{
        title = $source.Title
        sourceUrl = $source.Url
        retrievalUrl = $source.FetchUrl
        licenseNote = $source.License
        localPath = "corpus/$($source.File)"
        contentCharacters = $document.Length
        truncated = $truncated
        sha256 = ([BitConverter]::ToString($hash)).Replace('-', '')
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    discoveryId = $discoveryId
    createdAt = [DateTimeOffset]::UtcNow
    provider = 'curated'
    title = 'The GM-Shaped Hole'
    centralQuestion = 'How do tabletop RPGs and isometric CRPGs create meaningful danger when software must replace human judgment?'
    sources = $manifestSources
}
$manifestPath = Join-Path $discoveryDirectory 'discovery_manifest.json'
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 6),
    [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    DiscoveryDirectory = $discoveryDirectory
    CorpusDirectory = $corpusDirectory
    ManifestPath = $manifestPath
    SourceCount = $manifestSources.Count
    TotalCharacters = $totalCharacters
}
