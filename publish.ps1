param(
    [switch]$NoSign,
    [switch]$Zip
)

$MSBuildExe="msbuild"
if ($null -eq (Get-Command $MSBuildExe -ErrorAction SilentlyContinue)) {
    $MSBuildExe="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    Write-Warning "MSBuild not in path, using $MSBuildExe"
    
    if ($null -eq (Get-Command $MSBuildExe -ErrorAction SilentlyContinue)) {
        Writer-Error "Cannot find MSBuild (aborting)"
        exit 1
    }
}

if ($NoSign) {
    Write-Warning "Signing has been disabled."
}

if ($Zip) {
    Write-Host "Zip archive will be created!" -ForegroundColor Green
}

# Publish script for SRS projects
$outputPath = ".\install-build"

# Common publish parameters
$commonParams = @(
    "--configuration", "Release",
    "/p:PublishReadyToRun=true",
    "/p:PublishSingleFile=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:IncludeSourceRevisionInInformationalVersion=false" #Dont add a git hash into the build version
)

# Define the path to signtool.exe
$signToolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x86\signtool.exe"
if (-not $NoSign -and -not (Test-Path $signToolPath)) {
    Write-Error "SignTool.exe not found at $signToolPath. Please verify the path."
    exit 1
}

# Define common parameters for signtool
$commonParameters = @(
    "sign",                                     # The sign command for signtool
    "/n", "`"Open Source Developer, Ciaran Fisher`"", # Subject Name of the certificate (explicitly quoted)
    "/a",                                       # Automatically select the best signing certificate
    "/t", "`"http://time.certum.pl/`"",             # Timestamp server URL (explicitly quoted)
    "/fd", "`"sha256`"",                              # File digest algorithm (explicitly quoted)
    "/v"                                        # Verbose output
)

# Main Client
Write-Host "Publishing DCS-SR-Client..." -ForegroundColor Green
Remove-Item "$outputPath\Client" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./DCS-SR-Client/DCS-SR-Client.csproj"
dotnet publish "./DCS-SR-Client/DCS-SR-Client.csproj" `
    --runtime win-x64 `
    --output "$outputPath\Client" `
    --self-contained false `
    @commonParams
Remove-Item "$outputPath\Client\*.so" -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\Client\*.config"  -Recurse -ErrorAction SilentlyContinue
Copy-Item "$outputPath\Client\runtimes\win-x64\native\*.dll" -Destination "./$outputPath/Client"
Remove-Item "$outputPath\Client\runtimes" -Recurse -ErrorAction SilentlyContinue

# Server
Write-Host "Publishing Server..." -ForegroundColor Green
Remove-Item "$outputPath\Server" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./Server/Server.csproj"
dotnet publish "./Server/Server.csproj" `
    --runtime win-x64 `
    --output "$outputPath\Server" `
    --self-contained false `
    @commonParams
Remove-Item "$outputPath\Server\*.so"  -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\Server\*.config"  -Recurse -ErrorAction SilentlyContinue


# Server Command Line - Windows
Write-Host "Publishing ServerCommandLine for Windows..." -ForegroundColor Green
Remove-Item "$outputPath\ServerCommandLine-Windows" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./ServerCommandLine\ServerCommandLine.csproj"
dotnet publish "./ServerCommandLine\ServerCommandLine.csproj" `
    --runtime win-x64 `
    --output "$outputPath\ServerCommandLine-Windows" `
    --self-contained true `
    @commonParams
Remove-Item "$outputPath\ServerCommandLine-Windows\*.so"  -Recurse -ErrorAction SilentlyContinue

# Server Command Line - Linux
Write-Host "Publishing ServerCommandLine for Linux..." -ForegroundColor Green
Remove-Item "$outputPath\ServerCommandLine-Linux" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./ServerCommandLine\ServerCommandLine.csproj"
dotnet publish "./ServerCommandLine\ServerCommandLine.csproj" `
    --runtime linux-x64 `
    --output "$outputPath\ServerCommandLine-Linux" `
    --self-contained true `
    @commonParams
Remove-Item "$outputPath\ServerCommandLine-Linux\*.dll"  -Recurse -ErrorAction SilentlyContinue


# External Audio
Write-Host "Publishing DCS-SR-ExternalAudio..." -ForegroundColor Green
Remove-Item "$outputPath\ExternalAudio" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./DCS-SR-ExternalAudio\DCS-SR-ExternalAudio.csproj"
dotnet publish "./DCS-SR-ExternalAudio\DCS-SR-ExternalAudio.csproj" `
    --runtime win-x64 `
    --output "$outputPath\ExternalAudio" `
    --self-contained false `
    @commonParams
Remove-Item "$outputPath\ExternalAudio\*.so"  -Recurse -ErrorAction SilentlyContinue


# Auto Updater
Write-Host "Publishing AutoUpdater..." -ForegroundColor Green
Remove-Item "$outputPath\AutoUpdater" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./AutoUpdater\AutoUpdater.csproj"
dotnet publish "./AutoUpdater\AutoUpdater.csproj" `
    --runtime win-x64 `
    --output "$outputPath\AutoUpdater" `
    --self-contained false `
    @commonParams



# SRS Lua Wrapper
Write-Host "Building SRS-Lua-Wrapper..." -ForegroundColor Green
Remove-Item "$outputPath\Scripts" -Recurse -ErrorAction SilentlyContinue
Write-Host "Copy Scripts..." -ForegroundColor Green
Copy-Item "./Scripts" -Destination "$outputPath\Scripts" -Recurse
&  $MSBuildExe `
    ".\SRS-Lua-Wrapper\SRS-Lua-Wrapper.vcxproj" `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /t:Rebuild

# Create directory and copy the built DLL
New-Item -ItemType Directory -Force -Path "$outputPath\Scripts\DCS-SRS\bin"
Copy-Item ".\SRS-Lua-Wrapper\x64\Release\srs.dll" -Destination "$outputPath\Scripts\DCS-SRS\bin"

Write-Host "Publishing Installer..." -ForegroundColor Green
Remove-Item "$outputPath\Installer" -Recurse -ErrorAction SilentlyContinue
dotnet clean "./Installer\Installer.csproj"
dotnet publish "./Installer\Installer.csproj" `
    --runtime win-x64 `
    --output "$outputPath\Installer" `
    --self-contained false `
    @commonParams

# VC Redist
Write-Host "Downloading VC redistributables..." -ForegroundColor Green
Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile "$outputPath\VC_redist.x64.exe"


##Prep Directory
Write-Host "Clean up and prepare Installer and AutoUpdater .exe's in the root" -ForegroundColor Green
Copy-Item "$outputPath\Installer\Installer.exe" -Destination "./$outputPath"
Copy-Item "$outputPath\AutoUpdater\SRS-AutoUpdater.exe" -Destination "./$outputPath"
Remove-Item "$outputPath\AutoUpdater" -Recurse -ErrorAction SilentlyContinue
Remove-Item "$outputPath\Installer" -Recurse -ErrorAction SilentlyContinue

Write-Host "Publishing complete! Check the $outputPath directory for the published files." -ForegroundColor Green

##Now Sign
Write-Host "Signing files"

if ($NoSign) {
    Write-Host "Skipped"
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
        $filesToSign = Get-ChildItem -Path $searchPath -Recurse -Include "srs.dll", "*.exe" -File -ErrorAction Stop
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
            $currentFileArgs = $commonParameters + $quotedFilePath

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