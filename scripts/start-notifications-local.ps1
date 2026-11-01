# Read local Docker values into this process only. Never print credentials or execute dotenv contents.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$values = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $root 'docker/.env')) {
    if ($line -match '^\s*([A-Z0-9_]+)\s*=(.*)$') {
        $value = $Matches[2].Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $values[$Matches[1]] = $value
    }
}
$mapping = @{
    NOTIFICATION_EMAIL_PASSWORD='Email__Password'; NOTIFICATION_EMAIL_HOST='Email__Host'
    NOTIFICATION_EMAIL_PORT='Email__Port'; NOTIFICATION_EMAIL_FROM='Email__From'
    NOTIFICATION_EMAIL_USERNAME='Email__Username'; NOTIFICATION_EMAIL_ENABLED='Email__Enabled'
    NOTIFICATION_FRONTEND_URL='Email__FrontendBaseUrl'; NOTIFICATIONS_SERVICE_KEY='Notifications__ServiceKey'
    ANALYTICS_SERVICE_KEY='Analytics__ServiceKey'; JWT_KEY='Jwt__Key'
    RABBITMQ_USER='RabbitMq__User'; RABBITMQ_PASSWORD='RabbitMq__Password'
}
$settings = @{
    ASPNETCORE_ENVIRONMENT='Development'; ASPNETCORE_URLS='http://localhost:5006'
    Jwt__Issuer='wardmate'; Jwt__Audience='wardmate-client'
}
foreach ($key in $mapping.Keys) { if ($values.ContainsKey($key) -and $values[$key]) { $settings[$mapping[$key]] = $values[$key] } }
if ($values.ContainsKey('NOTIFICATIONS_ENABLED')) {
    $settings['Notifications__WorkersEnabled'] = $values['NOTIFICATIONS_ENABLED']
    $settings['RabbitMq__Enabled'] = $values['NOTIFICATIONS_ENABLED']
}
# Npgsql quoting preserves passwords containing semicolons and quotes.
if ($values['NOTIFICATIONS_DB_PASSWORD']) {
    $escaped = $values['NOTIFICATIONS_DB_PASSWORD'].Replace('"', '""')
    $settings['ConnectionStrings__NotificationsDatabase'] = 'Host=localhost;Port=5436;Database=wardmate_notifications_db;Username=wardmate_notifications;Password="' + $escaped + '"'
}
$previous = @{}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    Write-Host 'Starting Analytics on localhost:5006. Requires local IAM, Workflow, RabbitMQ and migrated notifications DB. Email may be sent for queued events.'
    & dotnet run --no-launch-profile --project (Join-Path $root 'src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API')
    if ($LASTEXITCODE -ne 0) { throw 'Analytics process failed. Review service logs without sharing secrets.' }
}
finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
