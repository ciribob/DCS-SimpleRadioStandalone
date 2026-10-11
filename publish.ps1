param(
    [switch]$NoSign,
    [switch]$Zip,
    [switch]$NoRedist,
    [string]$outputPath=".\install-build",
    [string]$VersionPrefix="2.4.1.0"

)

if ($NoSign) {
    Write-Warning "Signing has been disabled."
}

if ($Zip) {
    Write-Host "Zip archive will be created!" -ForegroundColor Green
}

$sharedParams = @(
     "--configuration", "Release",
     "-p:VersionPrefix=$VersionPrefix",
     "-p:IncludeSourceRevisionInInformationalVersion=false", #Dont add a git hash into the build version
     "--no-restore" # Don't restore packages (done as a root step)
)


# Publish script for SRS projects
dotnet restore --locked-mode

# Common publish parameters
$executableParams = $sharedParams + @(
    "/p:PublishReadyToRun=true",
    "/p:PublishSingleFile=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)

$windowsExecutableParams = $executableParams + @("--runtime", "win-x64")

$selfContainedExecutableParams = $windowsExecutableParams + @("--self-contained")
$frameworkDependentExecutableParams = $windowsExecutableParams + @("--no-self-contained")

# Define the path to signtool.exe
$signToolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\signtool.exe"
if (-not $NoSign -and -not (Test-Path $signToolPath)) {
    Write-Error "SignTool.exe not found at $signToolPath. Please verify the path."
    exit 1
}

# Define common parameters for signtool
$signParameters = @(
    "sign",                                     # The sign command for signtool
    "/n", "`"Open Source Developer, Ciaran Fisher`"", # Subject Name of the certificate (explicitly quoted)
    "/a",                                       # Automatically select the best signing certificate
    "/t", "`"http://time.certum.pl/`"",             # Timestamp server URL (explicitly quoted)
    "/fd", "`"sha256`"",                              # File digest algorithm (explicitly quoted)
    "/v"                                        # Verbose output
)

if (Test-Path "$outputPath")
{
    Write-Host "Removing $outputPath..."
    Remove-Item "$outputPath" -Recurse
    # Bubble the error if we fail to remove.
    # This means we would end up with a non-clean build tree.
}

# Copy Scripts.
function Format-Script {
    param([string]$file)

    # https://stackoverflow.com/a/30893960
    $content = [System.IO.File]::ReadAllText($file).Replace("VersionPrefix","$VersionPrefix")
    [System.IO.File]::WriteAllText($file, $content)
}

Write-Host "Copy Scripts..." -ForegroundColor Green
Copy-Item "./Scripts" -Destination "$outputPath/Scripts" -Recurse
foreach ($file in Get-ChildItem -Path "$outputPath\Scripts" -Filter *.lua -File -Recurse) {
    Format-Script $file.FullName
}

# Copy Examples.txt, Readme.txt
Write-Host "Copy Readme.txt, Examples.txt..." -ForegroundColor Green
Copy-Item "./docs/Readme.txt" -Destination "$outputPath"
Copy-Item "./docs/Examples.txt" -Destination "$outputPath"

function Publish-Project
{
    param($project)

    Write-Host "Publishing $($project.csproj) to '$outputPath$($project.outputSlug)'..." -ForegroundColor Green
    dotnet clean "$($project.csproj)"
    $params = $project.publishParams
    dotnet publish "$($project.csproj)" @params --output "$outputPath$($project.outputSlug)"
    if ($LastExitCode -ne 0)
    {
        exit $LastExitCode
    }
}

$projects = @(
    @{ csproj = "./DCS-SR-Client/DCS-SR-Client.csproj"; outputSlug = "/Client"; publishParams = $frameworkDependentExecutableParams }
    , @{ csproj = "./Server/Server.csproj"; outputSlug = "/Server"; publishParams = $frameworkDependentExecutableParams  }
    , @{ csproj = "./ServerCommandLine\ServerCommandLine.csproj"; outputSlug = "/ServerCommandLine-Windows"; publishParams = $selfContainedExecutableParams }
    , @{ csproj = "./ServerCommandLine\ServerCommandLine.csproj"; outputSlug = "/ServerCommandLine-Linux"; publishParams = $executableParams + @("--runtime", "linux-x64", "--self-contained") }
    , @{ csproj = "./DCS-SR-ExternalAudio\DCS-SR-ExternalAudio.csproj"; outputSlug = "/ExternalAudio"; publishParams = $frameworkDependentExecutableParams }
    , @{ csproj = "./AutoUpdater\AutoUpdater.csproj"; outputSlug = ""; publishParams = $frameworkDependentExecutableParams }
    , @{ csproj = "./DCS-SR-Lua/DCS-SR-Lua.csproj"; outputSlug = "/Scripts/DCS-SRS/bin"; publishParams = $sharedParams + @("--runtime", "win-x64", "--self-contained") }
    , @{ csproj = "./Installer\Installer.csproj"; outputSlug = ""; publishParams = $frameworkDependentExecutableParams }
)

foreach ($project in $projects)
{
    Publish-Project $project
}

Write-Host "Cleaning up/moving artifacts..." -ForegroundColor Green

# Main Client
Remove-Item "$outputPath\Client\*.so" -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\Client\*.config"  -Recurse -ErrorAction SilentlyContinue
Move-Item "$outputPath\Client\runtimes\win-x64\native\*.dll" -Destination "./$outputPath/Client"
Remove-Item "$outputPath\Client\runtimes" -Recurse -ErrorAction SilentlyContinue

# Server
Remove-Item "$outputPath\Server\*.so"  -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\Server\*.config"  -Recurse -ErrorAction SilentlyContinue


# Server Command Line - Windows
Remove-Item "$outputPath\ServerCommandLine-Windows\*.so"  -Recurse -ErrorAction SilentlyContinue

# Server Command Line - Linux
Remove-Item "$outputPath\ServerCommandLine-Linux\*.dll"  -Recurse -ErrorAction SilentlyContinue

# External Audio
Remove-Item "$outputPath\ExternalAudio\*.so"  -Recurse -ErrorAction SilentlyContinue

# SRS Lua .NET
# Strip pdb if it was copied.
Remove-Item -Path "$outputPath/Scripts/DCS-SRS/bin/lua-srs.pdb" -ErrorAction SilentlyContinue

# VC Redist
Write-Host "Downloading VC redistributables..." -ForegroundColor Green
if (-Not $NoRedist)
{
    Invoke-WebRequest -Uri "https://aka.ms/vs/18/release/vc_redist.x64.exe" -OutFile "$outputPath\VC_redist.x64.exe"
}
else
{
    Write-Warning "Skipped."
}

Write-Host "Publishing complete! Check the $outputPath directory for the published files." -ForegroundColor Green

##Now Sign
Write-Host "Signing files"

if ($NoSign) {
    Write-Warning "Skipped"
} else {
    # Define the root path to search for files to be signed
    # The script will recursively find all .dll and .exe files in this path and its subdirectories.
    $searchPath = $outputPath

    if (-not (Test-Path $searchPath -PathType Container)) {
        Write-Error "Search path '$searchPath' not found or is not a directory. Please verify the path."
        exit 1
    }

    Write-Host "Searching for .dll and .exe files in '$searchPath' and its subdirectories..."
    # Get all .exe files recursively. -File ensures we only get files.
    try {
        $filesToSign = Get-ChildItem -Path $searchPath -Recurse -Include "lua-srs.dll", "*.exe" -File -ErrorAction Stop
    } catch {
        Write-Error "Error occurred while searching for files: $($_.Exception.Message)"
        exit 1
    }


    if ($null -eq $filesToSign -or $filesToSign.Count -eq 0) {
        Write-Warning "No .exe files found in '$searchPath' to sign."
    } else {
        Write-Host "Found $($filesToSign.Count) file(s) to process."

        # Loop through each found file and sign it
        foreach ($fileInstance in $filesToSign) {
            $filePath = $fileInstance.FullName # Get the full path of the file

            if ($fileInstance.FullName -match "VC_redist.x64")
            {
                Write-Host "Skipping VCRedist " -ForegroundColor Green
                continue
            }

            Write-Host "Attempting to sign $filePath..."

            # Explicitly quote the file path argument for signtool
            $quotedFilePath = "`"$filePath`""

            # Construct the arguments for the current file.
            # The explicitly quoted file path is added as the last argument.
            $currentFileArgs = $signParameters + $quotedFilePath

            # Start the signing process
            # Using Start-Process to call external executables is a robust way.
            # -NoNewWindow keeps the output in the current console.
            # -Wait ensures PowerShell waits for signtool.exe to complete.
            # -PassThru returns a process object (useful for checking ExitCode).
            try {
                $process = Start-Process -FilePath $signToolPath -ArgumentList $currentFileArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop
                
                Write-Host "Start-Process -FilePath $signToolPath -ArgumentList $currentFileArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop"
                
                if ($process.ExitCode -eq 0) {
                    Write-Host "Successfully signed $filePath." -ForegroundColor Green
                } else {
                    # Signtool.exe usually outputs its own errors to stderr, which PowerShell might show.
                    Write-Error "Failed to sign $filePath. SignTool.exe exited with code: $($process.ExitCode). Check output above for details from SignTool."
                    
                    exit 1;
                }
            } catch {
                Write-Error "An error occurred while trying to run SignTool.exe for $filePath. Error: $($_.Exception.Message)"
            }
        }
    }
}

### Zip

Write-Host "Creating zip archive..." -ForegroundColor Green

if(!$Zip){
    Write-Warning "Skipped."
    exit 0
}


Write-Host "Removing old zip files from '$outputPath'..." -ForegroundColor Yellow
Remove-Item -Path "$outputPath\*.zip" -ErrorAction SilentlyContinue


$installerPath = "$outputPath\Installer.exe"
if (Test-Path $installerPath) {
    $version = (Get-Item -Path $installerPath).VersionInfo.ProductVersion
    Write-Host "Found installer version: $version" -ForegroundColor Cyan
} else {
    Write-Error "Installer.exe not found at $installerPath. Cannot determine version."
    exit 1
}


$zipFileName = "DCS-SimpleRadioStandalone-$version.zip"
$zipFilePath = Join-Path -Path (Get-Item -Path $outputPath).FullName -ChildPath $zipFileName


try {
    Compress-Archive -Path "$outputPath\*" -DestinationPath $zipFilePath -Force
    Write-Host "Successfully created zip file at: $zipFilePath" -ForegroundColor Green
} catch {
    Write-Error "Failed to create the zip archive. Error: $($_.Exception.Message)"
    exit 1
}
