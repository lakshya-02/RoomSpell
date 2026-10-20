param(
    [switch]$SkipApk,
    [string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe',
    [string]$Simulator = 'C:\Program Files\MetaXRSimulator\v207.0'
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$output = Join-Path $project "Validation\Simulator\$stamp"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

function Start-OwnedProcess([string]$File, [string[]]$Arguments, [string]$WorkingDirectory, [hashtable]$Environment) {
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.Arguments = ($Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($key in $Environment.Keys) { $info.EnvironmentVariables[$key] = $Environment[$key] }
    $p = [System.Diagnostics.Process]::Start($info)
    $processes.Add($p)
    return $p
}
function Wait-OwnedProcess($Process, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while (!$Process.HasExited) {
        if ((Get-Date) -gt $deadline) { throw "Process $($Process.Id) timed out after $Seconds seconds." }
        Start-Sleep -Seconds 1
    }
    $Process.Refresh()
    if ($Process.ExitCode -ne 0) { throw "Process $($Process.Id) exited $($Process.ExitCode). Logs: $output" }
}
function Operator-Request([string]$Method, $Parameters, [int]$Id) {
    $request = @{ jsonrpc = '2.0'; method = $Method; params = $Parameters }
    if ($Id -ge 0) { $request.id = $Id }
    $body = $request | ConvertTo-Json -Depth 20 -Compress
    $headers = @{Accept='application/json, text/event-stream'}
    if ($script:operatorSession) { $headers['Mcp-Session-Id'] = $script:operatorSession }
    $response = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8720/mcp' -Method Post -ContentType 'application/json' -Headers $headers -Body $body -TimeoutSec 10
    if ($response.Headers['Mcp-Session-Id']) { $script:operatorSession = $response.Headers['Mcp-Session-Id'] }
    $raw = $response.Content
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    if ($raw -match '^data:') { $raw = (($raw -split "`n" | Where-Object { $_.StartsWith('data:') } | Select-Object -Last 1).Substring(5)).Trim() }
    $decoded = $raw | ConvertFrom-Json
    if ($decoded.error) { throw ($decoded.error | ConvertTo-Json -Compress) }
    return $decoded.result
}
try {
    if (!(Test-Path $Editor) -or !(Test-Path "$Simulator\meta_openxr_simulator.json")) { throw 'Unity 6000.3.19f1 and Meta XR Simulator v207 are required.' }
    if (!(Get-Content "$project\ProjectSettings\ProjectVersion.txt" -Raw).Contains('6000.3.19f1')) { throw 'Unexpected project Unity version.' }
    $license = (& unity license status --format json) | ConvertFrom-Json
    if (!$license.success -or !$license.data.active) { throw 'An active Unity license is required.' }
    $manifest = Get-Content "$project\Packages\manifest.json" -Raw | ConvertFrom-Json
    $requiredVersions = @{
        'com.meta.xr.sdk.core'='207.0.0'; 'com.meta.xr.mrutilitykit'='207.0.0'; 'com.meta.xr.sdk.interaction.ovr'='207.0.0'
        'com.unity.xr.openxr'='1.18.0'; 'com.unity.ai.inference'='2.6.1'; 'com.unity.pipeline'='0.8.0-exp.1'
    }
    foreach ($package in $requiredVersions.Keys) {
        if ($manifest.dependencies.$package -ne $requiredVersions[$package]) { throw "Expected $package version $($requiredVersions[$package])." }
    }
    if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like "*$project*" }) {
        throw 'The project is already open. Close its Editor before starting this owned validation session.'
    }
    $backup = Join-Path $project 'Temp\__Backupscenes'
    if (Test-Path -LiteralPath $backup) {
        $resolvedBackup = (Resolve-Path -LiteralPath $backup).Path
        $preservedBackup = [IO.Path]::GetFullPath((Join-Path $output 'preserved-scene-backups'))
        if (!$resolvedBackup.StartsWith($project + '\') -or !$preservedBackup.StartsWith($output + '\')) {
            throw 'Scene backup paths are outside this project validation directory.'
        }
        Move-Item -LiteralPath $resolvedBackup -Destination $preservedBackup
        Write-Host "Preserved interrupted-session backups at $preservedBackup"
    }
    & dotnet run --project "$project\Tests\PuzzleRoundChecks.csproj"
    if ($LASTEXITCODE -ne 0) { throw 'Pure C# checks failed.' }
    $config = Get-Content "$Simulator\config\sim_core_configuration.json" -Raw | ConvertFrom-Json
    $config.log_location = "$output\simulator.log"
    $config.use_batch_mode = $false
    $config.ses_texture_format = 'jpg'
    $config | ConvertTo-Json -Depth 20 | Set-Content "$output\simulator-config.json" -Encoding UTF8
    $environment = @{
        XR_RUNTIME_JSON = "$Simulator\meta_openxr_simulator.json"
        XR_SELECTED_RUNTIME_JSON = "$Simulator\meta_openxr_simulator.json"
        META_XRSIM_CONFIG_JSON = "$output\simulator-config.json"
        ROOMSPELL_VALIDATION_DIR = $output
        ROOMSPELL_STARTED_UTC = [DateTime]::UtcNow.ToString('o')
    }
    $helper = Start-OwnedProcess "$Simulator\synthetic_env_server\synth_env_server.exe" @('GameRoom','-logFile',"$output\synthetic-environment.log") "$Simulator\synthetic_env_server" @{}
    $unity = Start-OwnedProcess $Editor @('-job-worker-count','2','-force-d3d11','-buildTarget','Win64','-projectPath',$project,'-logFile',"$output\unity.log") $project $environment
    Write-Host "Importing project and connecting Unity Pipeline. Artifacts: $output"
    $deadline = (Get-Date).AddMinutes(10)
    do {
        Start-Sleep -Seconds 5
        if ($unity.HasExited) { throw 'Unity exited during startup. Inspect unity.log.' }
        $catalogText = & unity command --project-path $project --query roomspell_validate --format json 2>$null
        try { $catalog = $catalogText | ConvertFrom-Json } catch { $catalog = $null }
        if ((Get-Date) -gt $deadline) { throw 'Unity Pipeline never became ready; check compile errors and license in unity.log.' }
        $loaded = (Test-Path "$output\unity.log") -and (Select-String -Path "$output\unity.log" -Pattern '\[Project\] Loading completed' -Quiet)
    } until ($catalog.success -and $catalog.data.count -gt 0 -and $loaded)
    $catalogText | Set-Content "$output\pipeline-catalog.json" -Encoding UTF8
    do {
        $started = & unity command roomspell_validate --project-path $project --format json
        $startResponse = $started | ConvertFrom-Json
        $started | Set-Content "$output\pipeline-start.json" -Encoding UTF8
        if (!$startResponse.success) {
            if ($startResponse.errors[0].message -notmatch '503|Server Busy|settling|Cannot connect|No Pipeline instance') {
                throw "Validation command could not start: $($startResponse.errors[0].message)"
            }
            if ((Get-Date) -gt $deadline) { throw 'Unity stayed busy after startup.' }
            Start-Sleep -Seconds 5
        }
    } until ($startResponse.success)
    $deadline = (Get-Date).AddMinutes(8)
    while (!(Test-Path "$output\pose-ready.txt")) {
        if (Test-Path "$output\results.json") { throw 'Validation finished before pose test. Inspect results.json.' }
        if ($unity.HasExited) { throw 'Validation exited before pose test. Inspect results.json.' }
        if ((Get-Date) -gt $deadline) { throw 'Pose test did not become ready.' }
        Start-Sleep -Seconds 1
    }
    $init = Operator-Request 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='RoomSpell terminal validation';version='1'}} 1
    Operator-Request 'notifications/initialized' @{} -1 | Out-Null
    $tools = Operator-Request 'tools/list' @{} 2
    $tools | ConvertTo-Json -Depth 30 | Set-Content "$output\operator-tools.json" -Encoding UTF8
    $poseTool = $tools.tools | Where-Object name -eq 'openxr_set_head_pose'
    if (!$poseTool) { throw 'Meta XR Operator head pose tool is unavailable.' }
    # Arguments use OpenXR coordinates; +X moves the view to the right.
    $pose = Operator-Request 'tools/call' @{name='openxr_set_head_pose';arguments=@{position=@(0.35,1.6,0.0);orientation=@(0.0,0.0,0.0,1.0);base_space='stage';reason='Verify RoomSpell tracking and board recentering'}} 3
    $pose | ConvertTo-Json -Depth 20 | Set-Content "$output\head-pose.json" -Encoding UTF8
    if ($pose.isError -or ($pose.content | Where-Object { $_.isError -or $_.text -like 'Error:*' })) { throw 'Head pose override failed. Inspect head-pose.json.' }
    $deadline = (Get-Date).AddMinutes(2)
    while (!(Test-Path "$output\capture-ready.txt")) {
        if ($unity.HasExited -or (Test-Path "$output\results.json")) { throw 'Validation exited before composited screenshot. Inspect results.json.' }
        if ((Get-Date) -gt $deadline) { throw 'Camera checks did not finish.' }
        Start-Sleep -Seconds 1
    }
    $capture = Operator-Request 'tools/call' @{name='openxr_capture_composited_image';arguments=@{eye='left';reason='Capture the actual RoomSpell board over synthetic passthrough'}} 4
    $captureImage = $capture.content | Where-Object type -eq 'image' | Select-Object -First 1
    if (!$captureImage) { throw 'XR compositor did not return an image.' }
    [IO.File]::WriteAllBytes("$output\composited-view.png", [Convert]::FromBase64String($captureImage.data))
    Set-Content "$output\capture-complete.txt" -Value 'captured' -Encoding UTF8
    Wait-OwnedProcess $unity 180
    $results = Get-Content "$output\results.json" -Raw | ConvertFrom-Json
    if (!$results.passed) { throw 'Simulator validation failed. Inspect results.json.' }
    Write-Host 'Simulator validation passed.'
    $legacy = Start-OwnedProcess $Editor @('-batchmode','-job-worker-count','2','-force-d3d11','-projectPath',$project,'-executeMethod','RoomSpellVerification.Run','-logFile',"$output\legacy-verification.log") $project $environment
    Wait-OwnedProcess $legacy 300
    if (!$SkipApk) {
        $build = Start-OwnedProcess $Editor @('-batchmode','-nographics','-quit','-job-worker-count','2','-buildTarget','Android','-projectPath',$project,'-executeMethod','RoomSpellQuestSetup.Build','-logFile',"$output\android-build.log") $project @{}
        Wait-OwnedProcess $build 1800
        if (!(Test-Path "$project\Builds\RoomSpell.apk")) { throw 'Build reported success but no APK exists.' }
        $android = Join-Path (Split-Path $Editor -Parent) 'Data\PlaybackEngines\AndroidPlayer'
        $buildTools = Get-ChildItem "$android\SDK\build-tools" -Directory | Sort-Object Name -Descending | Select-Object -First 1
        $apk = "$project\Builds\RoomSpell.apk"
        $badging = & "$($buildTools.FullName)\aapt2.exe" dump badging $apk
        if ($LASTEXITCODE -ne 0 -or ($badging -join "`n") -notmatch "package: name='com.roomspell.game'" -or ($badging -join "`n") -notmatch "native-code: 'arm64-v8a'") {
            throw 'APK identity or ARM64 packaging verification failed.'
        }
        $permissions = & "$($buildTools.FullName)\aapt2.exe" dump permissions $apk
        if ($LASTEXITCODE -ne 0 -or ($permissions -join "`n") -notmatch 'horizonos.permission.HEADSET_CAMERA' -or ($badging -join "`n") -notmatch 'com.oculus.feature.PASSTHROUGH') {
            throw 'APK is missing camera permission or passthrough feature.'
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($apk)
        try {
            if (!$archive.GetEntry('lib/arm64-v8a/libil2cpp.so')) { throw 'APK does not contain ARM64 IL2CPP.' }
        } finally { $archive.Dispose() }
        $signature = & "$android\OpenJDK\bin\java.exe" -jar "$($buildTools.FullName)\lib\apksigner.jar" verify --verbose $apk
        if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
        $badging | Set-Content "$output\apk-badging.txt" -Encoding UTF8
        $permissions | Set-Content "$output\apk-permissions.txt" -Encoding UTF8
        $signature | Set-Content "$output\apk-signature.txt" -Encoding UTF8
        @{passed=$true;apk=$apk;bytes=(Get-Item $apk).Length;sha256=(Get-FileHash $apk -Algorithm SHA256).Hash} |
            ConvertTo-Json | Set-Content "$output\apk-verification.json" -Encoding UTF8
    }
    Write-Host "Validation complete. Report: $output\results.json"
}
finally {
    $ownedIds = @($processes | ForEach-Object Id)
    $children = Get-CimInstance Win32_Process | Where-Object {
        $_.ParentProcessId -in $ownedIds -and $_.ExecutablePath -like "$Simulator\*"
    }
    foreach ($child in $children) { Stop-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
    foreach ($p in $processes) { if (!$p.HasExited) { $p.Kill(); $p.WaitForExit() } }
}
