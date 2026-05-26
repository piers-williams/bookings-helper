## Populate warden duty schedule from the 2026/27 Warden Calendar
## Usage: .\populate-warden-schedule.ps1 [-ApiBase "http://localhost:8099"]
##
## Rules:
##   - Every Friday from Apr 3 2026, teams rotate: C, A, B, D, C, A, ...
##   - Normal weekends: Fri 19:00 → Sun 16:00
##   - School holidays (Jul 24 – Aug 30): team also covers Sun 16:00 → Fri 19:00
##     i.e. full week Fri 19:00 → next Fri 19:00
##
## Review the output table, then re-run with -Confirm to POST to the API.

param(
    [string]$ApiBase = "http://localhost:5000",
    [switch]$Confirm
)

$sequence = @("C Team", "A Team", "B Team", "D Team")
$holidayStart = [datetime]"2026-07-24"
$holidayEnd   = [datetime]"2026-08-30"
$firstFriday  = [datetime]"2026-04-03"
$lastFriday   = [datetime]"2027-03-26"

$duties = @()
$friday = $firstFriday
$week = 0

while ($friday -le $lastFriday) {
    $team = $sequence[$week % 4]
    $start = $friday.AddHours(19)             # Fri 19:00

    $inHoliday = $friday -ge $holidayStart -and $friday -lt $holidayEnd
    if ($inHoliday) {
        $end = $friday.AddDays(7).AddHours(19)  # next Fri 19:00
    } else {
        $end = $friday.AddDays(2).AddHours(16)  # Sun 16:00
    }

    $duties += [pscustomobject]@{
        Start = $start
        End   = $end
        Team  = $team
        Type  = if ($inHoliday) { "WEEK" } else { "WKND" }
    }

    $friday = $friday.AddDays(7)
    $week++
}

# ── Display ──────────────────────────────────────────────────────────

Write-Host "`n=== 2026/27 Warden Schedule ($($duties.Count) duties) ===`n" -ForegroundColor Cyan

$duties | ForEach-Object {
    [pscustomobject]@{
        Type = $_.Type
        From = $_.Start.ToString("ddd dd MMM yyyy HH:mm")
        To   = $_.End.ToString("ddd dd MMM yyyy HH:mm")
        Team = $_.Team
    }
} | Format-Table -AutoSize

if (-not $Confirm) {
    Write-Host "Review the schedule above. Run with -Confirm to POST to $ApiBase/api/schedule" -ForegroundColor Yellow
    return
}

# ── Submit ───────────────────────────────────────────────────────────

Write-Host "Posting $($duties.Count) duties to $ApiBase/api/schedule ..." -ForegroundColor Green

$success = 0
$failed = 0
foreach ($d in $duties) {
    $body = @{
        startDate = $d.Start.ToString("yyyy-MM-ddTHH:mm:ss")
        endDate   = $d.End.ToString("yyyy-MM-ddTHH:mm:ss")
        teamName  = $d.Team
    } | ConvertTo-Json

    try {
        $null = Invoke-RestMethod -Uri "$ApiBase/api/schedule" -Method Post -Body $body -ContentType "application/json"
        $success++
    } catch {
        Write-Host "  FAILED: $($d.Team) $($d.Start.ToString('dd MMM')) - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`nDone: $success created, $failed failed." -ForegroundColor Cyan
