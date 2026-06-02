param(
    [string]$BaseUrl = "https://speakpath.app",
    [int]$TimeoutSeconds = 60,
    [int]$IntervalSeconds = 5
)

$ErrorActionPreference = "Stop"

function Invoke-CanaryRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Url
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = [string]$response.Content
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $body = ""
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = [System.IO.StreamReader]::new($stream)
                $body = $reader.ReadToEnd()
            }
        }
        catch {
            $body = ""
        }

        return [pscustomobject]@{
            StatusCode = [int]$statusCode
            Body = $body
        }
    }
}

function Wait-ForCanaryCondition {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Check
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastMessage = ""

    while ((Get-Date) -lt $deadline) {
        $result = & $Check
        if ($result.Ok) {
            return $result
        }

        $lastMessage = $result.Message
        Write-Host "$Name not ready yet: $lastMessage"
        Start-Sleep -Seconds $IntervalSeconds
    }

    throw "$Name did not pass within $TimeoutSeconds seconds. Last result: $lastMessage"
}

$base = $BaseUrl.TrimEnd("/")

Write-Host "Checking $base"
Wait-ForCanaryCondition "Landing page" {
    $landing = Invoke-CanaryRequest "$base/"
    [pscustomobject]@{
        Ok = $landing.StatusCode -ge 200 -and $landing.StatusCode -lt 400
        Message = "expected 2xx/3xx, got $($landing.StatusCode)"
    }
} | Out-Null

Write-Host "Checking $base/health"
Wait-ForCanaryCondition "API health" {
    $health = Invoke-CanaryRequest "$base/health"
    $looksLikeHtml = $health.Body -match "<html|<!doctype"
    [pscustomobject]@{
        Ok = $health.StatusCode -eq 200 -and -not $looksLikeHtml
        Message = if ($looksLikeHtml) {
            "expected API health, got SPA HTML"
        } else {
            "expected 200 API health, got $($health.StatusCode)"
        }
    }
} | Out-Null

Write-Host "Checking protected API returns 401"
Wait-ForCanaryCondition "Protected API auth boundary" {
    $protected = Invoke-CanaryRequest "$base/api/dashboard"
    [pscustomobject]@{
        Ok = $protected.StatusCode -eq 401
        Message = "expected 401, got $($protected.StatusCode)"
    }
} | Out-Null

Write-Host "Production canary checks passed."
