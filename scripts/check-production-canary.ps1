param(
    [string]$BaseUrl = "https://speakpath.app"
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

$base = $BaseUrl.TrimEnd("/")

Write-Host "Checking $base"
$landing = Invoke-CanaryRequest "$base/"
if ($landing.StatusCode -lt 200 -or $landing.StatusCode -ge 400) {
    throw "Expected landing page to return 2xx/3xx, got $($landing.StatusCode)."
}

Write-Host "Checking $base/health"
$health = Invoke-CanaryRequest "$base/health"
if ($health.StatusCode -ne 200) {
    throw "Expected /health to return 200, got $($health.StatusCode)."
}
if ($health.Body -match "<html|<!doctype") {
    throw "Expected /health to return API health, but response looked like SPA HTML."
}

Write-Host "Checking protected API returns 401"
$protected = Invoke-CanaryRequest "$base/api/dashboard"
if ($protected.StatusCode -ne 401) {
    throw "Expected unauthenticated /api/dashboard to return 401, got $($protected.StatusCode)."
}

Write-Host "Production canary checks passed."
