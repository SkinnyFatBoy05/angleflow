[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:8088',
    [string]$CamundaBaseUrl = 'http://localhost:8080',
    [ValidateRange(10, 300)]
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$expected = [ordered]@{
    'auto-approve' = 'Approved'
    'manual-review' = 'ManualReview'
    'decline' = 'Declined'
    'transient-failure' = 'Approved'
    'incident' = 'ChecksRunning'
}

function Get-IncidentCount {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$CamundaBaseUrl/v2/incidents/search" `
        -ContentType 'application/json' `
        -Body '{}'
    return [int]$response.page.totalItems
}

function New-SyntheticApplication([string]$Scenario, [string]$KeySuffix) {
    $body = @{
        applicantName = "Synthetic $Scenario"
        email = "$Scenario-$runId@example.test"
        annualIncome = 120000
        monthlyExpenses = 3500
        assetType = 'vehicle'
        assetValue = 65000
        requestedAmount = 45000
        termMonths = 48
        scenario = $Scenario
    } | ConvertTo-Json

    return Invoke-RestMethod `
        -Method Post `
        -Uri "$ApiBaseUrl/api/applications/" `
        -Headers @{ 'Idempotency-Key' = "smoke-$runId-$KeySuffix" } `
        -ContentType 'application/json' `
        -Body $body
}

$health = Invoke-RestMethod "$ApiBaseUrl/health/ready"
if ($health.status -ne 'Ready') {
    throw "API readiness returned '$($health.status)'."
}

$incidentsBefore = Get-IncidentCount
$created = [ordered]@{}
foreach ($scenario in $expected.Keys) {
    $created[$scenario] = New-SyntheticApplication -Scenario $scenario -KeySuffix $scenario
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $allReached = $true
    foreach ($scenario in $expected.Keys) {
        $current = Invoke-RestMethod "$ApiBaseUrl/api/applications/$($created[$scenario].id)"
        if ($current.status -ne $expected[$scenario]) {
            $allReached = $false
        }
    }

    $incidentDelta = (Get-IncidentCount) - $incidentsBefore
    if ($incidentDelta -lt 5) {
        $allReached = $false
    }

    if (-not $allReached) {
        Start-Sleep -Seconds 1
    }
} while (-not $allReached -and [DateTimeOffset]::UtcNow -lt $deadline)

if (-not $allReached) {
    throw "Scenario matrix did not reach its expected state within $TimeoutSeconds seconds."
}

$replayBody = @{
    applicantName = 'Synthetic auto-approve'
    email = "auto-approve-$runId@example.test"
    annualIncome = 120000
    monthlyExpenses = 3500
    assetType = 'vehicle'
    assetValue = 65000
    requestedAmount = 45000
    termMonths = 48
    scenario = 'auto-approve'
} | ConvertTo-Json
$replay = Invoke-RestMethod `
    -Method Post `
    -Uri "$ApiBaseUrl/api/applications/" `
    -Headers @{ 'Idempotency-Key' = "smoke-$runId-auto-approve" } `
    -ContentType 'application/json' `
    -Body $replayBody
if ($replay.id -ne $created['auto-approve'].id) {
    throw 'Idempotent replay returned a different application ID.'
}

$withdrawal = New-SyntheticApplication -Scenario 'manual-review' -KeySuffix 'withdrawal'
do {
    $withdrawalCurrent = Invoke-RestMethod "$ApiBaseUrl/api/applications/$($withdrawal.id)"
    if ($withdrawalCurrent.status -ne 'ManualReview') {
        Start-Sleep -Milliseconds 500
    }
} while ($withdrawalCurrent.status -ne 'ManualReview' -and [DateTimeOffset]::UtcNow -lt $deadline)

$withdrawn = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/applications/$($withdrawal.id)/withdraw"
if ($withdrawn.status -ne 'Withdrawn') {
    throw "Withdrawal returned '$($withdrawn.status)' instead of 'Withdrawn'."
}

$timeline = Invoke-RestMethod "$ApiBaseUrl/api/applications/$($withdrawal.id)/timeline"
for ($index = 0; $index -lt $timeline.Count; $index++) {
    if ($timeline[$index].sequence -ne ($index + 1)) {
        throw 'Withdrawal audit sequence is not contiguous.'
    }
}

$statuses = [ordered]@{}
foreach ($scenario in $expected.Keys) {
    $statuses[$scenario] = (Invoke-RestMethod "$ApiBaseUrl/api/applications/$($created[$scenario].id)").status
}

[pscustomobject]@{
    runId = $runId
    health = $health.status
    statuses = $statuses
    newIncidents = (Get-IncidentCount) - $incidentsBefore
    idempotentReplay = $true
    withdrawal = $withdrawn.status
    withdrawalAuditSequences = @($timeline.sequence)
} | ConvertTo-Json -Depth 6
