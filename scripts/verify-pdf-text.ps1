param(
    [Parameter(Mandatory = $true)][string]$PdfPath,
    [string]$ExpectedText = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pdf = (Resolve-Path -LiteralPath $PdfPath).Path
$dll = Join-Path $root 'src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/bin/Release/net8.0/WardMate.Services.AIOCR.API.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Hãy build Release trước khi kiểm tra.' }
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$key = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$info = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$info.ArgumentList.Add($dll)
$info.WorkingDirectory = Split-Path $dll -Parent
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$info.Environment['ASPNETCORE_URLS'] = "http://127.0.0.1:$port"
$info.Environment['ASPNETCORE_ENVIRONMENT'] = 'Production'
$info.Environment['Extraction__Enabled'] = 'true'
$info.Environment['Extraction__UseAI'] = 'false'
$info.Environment['Extraction__OcrFallbackEnabled'] = 'false'
$info.Environment['ServiceAuthentication__Key'] = $key
$process = [System.Diagnostics.Process]::Start($info)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
try {
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        if ($process.HasExited) { throw 'AIOCR không khởi động được.' }
        try {
            $null = Invoke-RestMethod "http://127.0.0.1:$port/health" -TimeoutSec 1
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 300 }
    }
    if (-not $ready) { throw 'Hết thời gian chờ AIOCR khởi động.' }
    $result = Invoke-RestMethod "http://127.0.0.1:$port/internal/v1/procedure-extractions" -Method Post `
        -Headers @{ 'X-Service-Key' = $key } -ContentType 'application/pdf' -InFile $pdf -TimeoutSec 120
    if ($ExpectedText -and -not $result.extractedText.Contains($ExpectedText)) { throw 'Không tìm thấy đoạn văn bản mong đợi.' }
    [pscustomobject]@{
        status = 'PASS'
        characters = $result.extractedText.Length
        expectedTextMatched = if ($ExpectedText) { $true } else { $null }
        warnings = $result.warnings
        preview = $result.extractedText.Substring(0, [Math]::Min(350, $result.extractedText.Length))
    } | ConvertTo-Json -Depth 5
} finally {
    if (-not $process.HasExited) { $process.Kill($true) }
    $process.WaitForExit()
    $process.Dispose()
}
