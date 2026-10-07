# Requires PowerShell 7, Docker and a completed Release build. Uses an isolated temporary database.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$container = 'wardmate-cookie-check-' + [Guid]::NewGuid().ToString('N')
$password = 'Check!' + [Guid]::NewGuid().ToString('N')
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:checks = 0

function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw "FAIL: $name" }
    $script:checks++
    Write-Output "PASS: $name"
}
function Free-Port {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}
function Start-Api([string]$project, [string]$assembly, [int]$port, [hashtable]$settings) {
    $directory = Join-Path $repo $project
    $dll = Join-Path $directory "bin/Release/net8.0/$assembly.dll"
    if (!(Test-Path -LiteralPath $dll)) { throw 'Run a solution-wide Release build first.' }
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.ArgumentList.Add($dll)
    $info.ArgumentList.Add('--urls')
    $info.ArgumentList.Add("http://localhost:$port")
    foreach ($entry in $settings.GetEnumerator()) { $info.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::Start($info)
    $processes.Add($process)
    # Drain logs without printing credentials/tokens to the console.
    $null = $process.StandardOutput.ReadToEndAsync()
    $null = $process.StandardError.ReadToEndAsync()
    for ($i = 0; $i -lt 100; $i++) {
        if ($process.HasExited) { throw "Temporary API stopped: $assembly" }
        try {
            $health = Invoke-WebRequest "http://localhost:$port/health" -TimeoutSec 1 -SkipHttpErrorCheck
            if ($health.StatusCode -eq 200) { return $process }
        } catch { }
        Start-Sleep -Milliseconds 200
    }
    throw "Temporary API did not become ready: $assembly"
}
function Request([string]$method, [string]$path, $body = $null, [hashtable]$headers = @{}) {
    $arguments = @{ Uri = "$script:base$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $arguments.Body = ConvertTo-Json $body -Depth 15 -Compress; $arguments.ContentType = 'application/json' }
    return Invoke-WebRequest @arguments
}
function Cookie-Pair($response) {
    return ($response.Headers['Set-Cookie'] | Where-Object { $_ -like 'refreshToken=*' } | Select-Object -First 1).Split(';')[0]
}

try {
    $null = & docker run --detach --rm --name $container -e "POSTGRES_PASSWORD=$password" -e POSTGRES_USER=cookie_check -e POSTGRES_DB=cookie_check -p 127.0.0.1::5432 postgres:16-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the temporary PostgreSQL container.' }
    $mapping = & docker port $container 5432/tcp
    $dbPort = ($mapping.Trim() -split ':')[-1]
    $ready = $false
    for ($i = 0; $i -lt 100; $i++) {
        $null = & docker exec $container pg_isready -U cookie_check -d cookie_check 2>$null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (!$ready) { throw 'PostgreSQL did not become ready.' }
    $iamPort = Free-Port
    $settings = @{
        ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development'
        ConnectionStrings__Database = "Host=localhost;Port=$dbPort;Database=cookie_check;Username=cookie_check;Password=$password"
        Jwt__Key = $key; Database__AutoMigrate = 'true'; Serilog__EnableFileSink = 'false'
        AuthCookie__AllowInsecureLocalhost = 'true'; AuthCookie__SameSite = 'Strict'
        Cors__AllowedOrigins__0 = 'http://localhost:5173'; Cors__AllowedOrigins__1 = 'http://localhost:3000'
    }
    $iam = Start-Api 'src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API' 'WardMate.Services.IAM.API' $iamPort $settings
    $gatewayPort = Free-Port
    $null = Start-Api 'src/Gateways/WardMate.YarpGateway' 'WardMate.YarpGateway' $gatewayPort @{
        ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development'
        ReverseProxy__Clusters__iam__Destinations__primary__Address = "http://localhost:$iamPort/"
        Cors__AllowedOrigins__0 = 'http://localhost:5173'; Cors__AllowedOrigins__1 = 'http://localhost:3000'
    }
    $script:base = "http://localhost:$gatewayPort"
    $auth = @{ 'X-CSRF-Protection' = '1'; Origin = 'http://localhost:5173' }
    $account = @{ username = 'cookie_' + [Guid]::NewGuid().ToString('N'); email = ([Guid]::NewGuid().ToString('N') + '@example.test'); password = $password; fullName = 'Kiểm thử Cookie' }
    $registration = Request POST '/api/v1/auth/register' $account $auth
    Check ($registration.StatusCode -eq 201) 'Register through Gateway'
    $credentials = @{ usernameOrEmail = $account.username; password = $password }
    $blocked = Request POST '/api/v1/auth/login' $credentials
    Check ($blocked.StatusCode -eq 403) 'Missing CSRF header rejected'
    $blocked = Request POST '/api/v1/auth/login' $credentials @{ 'X-CSRF-Protection' = '1'; Origin = 'https://untrusted.example.test' }
    Check ($blocked.StatusCode -eq 403) 'Untrusted Origin rejected even with custom header'
    $login = Request POST '/api/v1/auth/login' $credentials $auth
    Check ($login.StatusCode -eq 200) 'Login through Gateway'
    $json = $login.Content | ConvertFrom-Json
    Check (!$json.PSObject.Properties['refreshToken'] -and $json.accessToken) 'JSON excludes refresh-token secret'
    $setCookie = [string]($login.Headers['Set-Cookie'] -join ';')
    Check ($setCookie -match 'httponly' -and $setCookie -match 'samesite=strict' -and $setCookie -match 'path=/api/v1/auth' -and $setCookie -notmatch 'domain=') 'HttpOnly Strict host-only cookie with auth path'
    Check ($setCookie -notmatch ';\s*secure') 'Development localhost supports HTTP'
    Check ($login.Headers['Cache-Control'] -contains 'no-store') 'Auth response is not cacheable'
    Check (($login.Headers['Access-Control-Allow-Origin'] -join '') -eq 'http://localhost:5173' -and ($login.Headers['Access-Control-Allow-Credentials'] -join '') -eq 'true') 'Credentialed CORS through Gateway'
    $oldCookie = Cookie-Pair $login
    $refreshHeaders = $auth.Clone(); $refreshHeaders.Cookie = $oldCookie
    $refresh = Request POST '/api/v1/auth/refresh-token' $null $refreshHeaders
    Check ($refresh.StatusCode -eq 200) 'Refresh works with cookie only after losing access token'
    $newCookie = Cookie-Pair $refresh
    Check ($newCookie -ne $oldCookie) 'Refresh rotates cookie'
    Check (!(($refresh.Content | ConvertFrom-Json).PSObject.Properties['refreshToken'])) 'Refresh JSON does not leak secret'
    $replay = Request POST '/api/v1/auth/refresh-token' $null $refreshHeaders
    Check ($replay.StatusCode -eq 401 -and !$replay.Headers['Set-Cookie']) 'Old refresh token rejected without clearing newer cookie'
    $bodyOnly = Request POST '/api/v1/auth/refresh-token' @{ refreshToken = $newCookie.Substring('refreshToken='.Length) } $auth
    Check ($bodyOnly.StatusCode -eq 401) 'JSON refresh token is not accepted as fallback'
    $missingHeader = Request POST '/api/v1/auth/refresh-token' $null @{ Cookie = $newCookie }
    Check ($missingHeader.StatusCode -eq 403) 'Refresh requires CSRF header'
    $access = ($refresh.Content | ConvertFrom-Json).accessToken
    $revokeHeaders = $auth.Clone(); $revokeHeaders.Cookie = $newCookie; $revokeHeaders.Authorization = "Bearer $access"
    $logout = Request POST '/api/v1/auth/revoke-token' $null $revokeHeaders
    Check ($logout.StatusCode -eq 204 -and ($logout.Headers['Set-Cookie'] -join '') -match 'expires=Thu, 01 Jan 1970') 'Logout revokes and deletes matching cookie'
    $afterLogout = Request POST '/api/v1/auth/refresh-token' $null @{ 'X-CSRF-Protection' = '1'; Cookie = $newCookie }
    Check ($afterLogout.StatusCode -eq 401) 'Revoked cookie cannot refresh'
    $preflight = Request OPTIONS '/api/v1/auth/refresh-token' $null @{ Origin = 'http://localhost:5173'; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'x-csrf-protection,content-type' }
    Check ($preflight.StatusCode -eq 204 -and $preflight.Headers['Access-Control-Allow-Credentials']) 'Allowed preflight succeeds'
    $preflight = Request OPTIONS '/api/v1/auth/refresh-token' $null @{ Origin = 'https://untrusted.example.test'; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'x-csrf-protection' }
    Check (!$preflight.Headers['Access-Control-Allow-Origin']) 'Untrusted preflight has no CORS allowance'
    $script:base = "http://localhost:$iamPort"
    $swagger = (Request GET '/swagger/v1/swagger.json').Content | ConvertFrom-Json -AsHashtable
    Check (!$swagger.paths['/api/v1/auth/refresh-token'].post.ContainsKey('requestBody')) 'Swagger refresh has no token request body'
    Check (!$swagger.components.schemas.BrowserAuthResponse.properties.ContainsKey('refreshToken')) 'Swagger response omits refresh secret'

    $iam.Kill($true); $iam.WaitForExit()
    $settings.ASPNETCORE_ENVIRONMENT = 'Production'; $settings.DOTNET_ENVIRONMENT = 'Production'
    $settings.AuthCookie__AllowInsecureLocalhost = 'false'
    $settings.Cors__AllowedOrigins__0 = 'https://frontend.example.test'; $settings.Cors__AllowedOrigins__1 = 'https://frontend2.example.test'
    $null = Start-Api 'src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API' 'WardMate.Services.IAM.API' $iamPort $settings
    $secureLogin = Request POST '/api/v1/auth/login' $credentials @{ 'X-CSRF-Protection' = '1' }
    Check ($secureLogin.StatusCode -eq 200 -and ($secureLogin.Headers['Set-Cookie'] -join '') -match ';\s*secure') 'Production always issues Secure cookie'
    $publicHost = 'wardmate-iam.blackmeadow-a2f12767.japaneast.azurecontainerapps.io'
    $proxyHeaders = @{
        Origin = "https://$publicHost"; 'X-CSRF-Protection' = '1'
        'X-Forwarded-Host' = $publicHost; 'X-Forwarded-Proto' = 'https'; 'X-Forwarded-For' = '198.51.100.10'
    }
    $proxyLogin = Request POST '/api/v1/auth/login' $credentials $proxyHeaders
    Check ($proxyLogin.StatusCode -eq 200) 'Forwarded HTTPS Swagger origin works without CORS allowlisting'
    $withoutForwarding = Request POST '/api/v1/auth/login' $credentials @{ Origin = "https://$publicHost"; 'X-CSRF-Protection' = '1' }
    Check ($withoutForwarding.StatusCode -eq 403) 'External origin mismatches internal HTTP request without forwarding'
    $noCsrf = $proxyHeaders.Clone(); $noCsrf.Remove('X-CSRF-Protection')
    Check ((Request POST '/api/v1/auth/login' $credentials $noCsrf).StatusCode -eq 403) 'Forwarding does not bypass CSRF header requirement'
    $wrongOrigin = $proxyHeaders.Clone(); $wrongOrigin.Origin = 'https://untrusted.example.test'
    Check ((Request POST '/api/v1/auth/login' $credentials $wrongOrigin).StatusCode -eq 403) 'Forwarding still rejects unrelated origins'
    $allowedOrigin = $proxyHeaders.Clone(); $allowedOrigin.Origin = 'https://frontend.example.test'
    $allowedResponse = Request POST '/api/v1/auth/login' $credentials $allowedOrigin
    Check ($allowedResponse.StatusCode -eq 200 -and ($allowedResponse.Headers['Access-Control-Allow-Origin'] -join '') -eq $allowedOrigin.Origin) 'Forwarding preserves credentialed CORS for approved frontend'
    Write-Output "Completed: $script:checks HTTP checks passed."
}
finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    # Only the unique container created by this script is removed; no local application data is touched.
    $null = & docker rm --force $container 2>$null
}
