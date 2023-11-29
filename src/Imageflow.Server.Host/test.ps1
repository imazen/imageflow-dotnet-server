
# Get the directory this file is in, and change to it.
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptPath

# First, we publish to a folder.
dotnet publish -c Release ./Imageflow.Server.Host.csproj -o ./test-publish/
# if the above fails, exit with a non-zero exit code.
if ($LASTEXITCODE -ne 0) {
    Write-Output "Failed to publish the AOT test project. Exiting."
    Write-Warning "Failed to publish the AOT test project. Exiting."

    exit 1
}

# run the executable in the background in ./publish/native/Imageflow.Server.Host.exe or ./publish/native/Imageflow.Server.Host
$process = $null
if (Test-Path -Path ./publish/native/Imageflow.Server.Host.exe) {
    $process = Start-Process -FilePath ./test-publish/Imageflow.Server.Host.exe -NoNewWindow -PassThru -RedirectStandardOutput "./output.log"
} else {
    $process = Start-Process -FilePath ./test-publish/Imageflow.Server.Host -NoNewWindow -PassThru -RedirectStandardOutput "./output.log"
}
# report on the process, if it started
if ($null -eq $process) {
    Write-Output "Failed to start the server. Exiting."
    Write-Warning "Failed to start the server. Exiting."
    exit 1
}
Write-Output "Started the server with PID $($process.Id)"

# quit if the process failed to start
if ($LASTEXITCODE -ne 0) {
    exit 1
}
# store the PID of the executable
$server_pid = $process.Id

# wait for the server to start 200ms
Start-Sleep -Milliseconds 200

$output = (Get-Content "./output.log");
# if null, it failed to start
if ($output -eq $null) {
    Write-Error "Failed to start the server (no output). Exiting."
    exit 1
}

Write-Output "Server output:"
Write-Output $output

# parse the port from the output log
$port = 5000
$portRegex = [regex]::new("Now listening on: http://localhost:(\d+)")
$portMatch = $portRegex.Match($output)
if ($portMatch.Success) {
    $port = $portMatch.Groups[1].Value
}



# if the process doesn't respond to a request, sleep 5 seconds and try again
$timeout = 5
$timeoutCounter = 0
while ($timeoutCounter -lt $timeout) {

    # try to make a request to the server
    $timeoutMs = $timeoutCounter * 500 + 200
    $url = "http://localhost:$port/"
    try{
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 1 -OutVariable response
        if ($response -ne $null) {
            Write-Output "Server responded to GET $url with status code $($response.StatusCode)"
            break
        }
    } catch {
        Write-Warning "Failed to make a request to $url with exception $_"
        $timeoutCounter++

        # if the process is not running, exit with a non-zero exit code
        if (-not (Get-Process -Id $server_pid -ErrorAction SilentlyContinue)) {
            Write-Warning "Server process with PID $server_pid is not running, crash detected. Exiting."
            exit 1
        }
        Start-Sleep -Seconds 1
        continue
    }
    Write-Warning "Server is not responding to requests at $url yet (timeout $timeoutMs), sleeping 1 second"
    # Find what's new in the output log that isn't in $output
    $newOutput = Get-Content "./output.log" | Select-Object -Skip $output.Length
    Write-Output $newOutput

    Start-Sleep -Seconds 1
    $timeoutCounter++
}

$testsFailed = 0
try
{
    # test /imageflow/version
    $version = Invoke-WebRequest -Uri http://localhost:5000/imageflow/version
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Request to /imageflow/version failed with exit code $LASTEXITCODE"
        $testsFailed += 1
    }
} catch {
    Write-Error "Request to /imageflow/version failed with exception $_"
    $testsFailed += 1
}

# test /imageflow/resize/width/10
try
{
    $resize = Invoke-WebRequest -Uri http://localhost:5000/imageflow/resize/width/10
    if ($LASTEXITCODE -ne 0)
    {
        Write-Warning "Request to /imageflow/resize/width/10 failed with exit code $LASTEXITCODE"
        $testsFailed += 1
    }
} catch {
    Write-Warning "Request to /imageflow/resize/width/10 failed with exception $_"
    $testsFailed += 1
}
# exit with a non-zero exit code if any tests failed
if ($testsFailed -ne 0)
{
    Write-Warning "$testsFailed tests failed. Exiting."
}

# kill the server
Stop-Process -Id $server_pid
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to kill the server process with PID $server_pid"
}

# print the process output
Get-Content "./output.log"

# exit with a non-zero exit code if any tests failed
if ($testsFailed -ne 0) {
    Write-Warning "$testsFailed tests failed. Exiting."
    exit 1
}
Write-Output "YAYYYY"
Write-Output "All tests passed. Exiting."
exit 0