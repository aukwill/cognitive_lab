param(
    [switch]$Persist
)

$envFile = Join-Path $PSScriptRoot "..\.env.local"

if (-not (Test-Path $envFile)) {
    Write-Error "No .env.local found at $envFile"
    return
}

$scope = if ($Persist) { "User" } else { "Process" }

foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*#' -or $line -notmatch '=') {
        continue
    }

    $name, $value = $line.Split('=', 2)
    [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), $scope)
}

if ($Persist) {
    Write-Host "Saved environment variables from .env.local to your Windows user profile."
    Write-Host "They will be available in new terminal sessions (not this one)."
} else {
    Write-Host "Loaded environment variables from .env.local into this session."
}
