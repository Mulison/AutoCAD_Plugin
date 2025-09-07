# PowerShell Build Script für AutoCAD Plugin CI/CD
# Optimiert für Jenkins Windows-Agent (ohne Docker)
# Dieses Skript kann sowohl lokal als auch in Jenkins verwendet werden

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts",
    [switch]$Clean = $false,
    [switch]$Test = $false,
    [switch]$Package = $false,
    [switch]$Verbose = $false,
    [string]$AutoCADPath = "C:\Program Files\Autodesk\AutoCAD 2026"
)

# Farben für Output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-DotNetInstallation {
    Write-ColorOutput "Prüfe .NET Installation..." $InfoColor
    
    try {
        $dotnetVersion = dotnet --version
        Write-ColorOutput ".NET Version gefunden: $dotnetVersion" $SuccessColor
        
        if (-not $dotnetVersion.StartsWith("8.")) {
            Write-ColorOutput "WARNUNG: .NET 8.0 wird empfohlen. Gefundene Version: $dotnetVersion" $WarningColor
        }
        
        return $true
    }
    catch {
        Write-ColorOutput "FEHLER: .NET ist nicht installiert oder nicht im PATH" $ErrorColor
        return $false
    }
}

function Test-AutoCADReferences {
    Write-ColorOutput "Prüfe AutoCAD DLL Referenzen..." $InfoColor
    
    $requiredDlls = @(
        "accoremgd.dll",
        "AcCui.dll", 
        "acdbmgd.dll",
        "AcDx.dll",
        "acmgd.dll"
    )
    
    $missingDlls = @()
    
    foreach ($dll in $requiredDlls) {
        $dllPath = Join-Path $AutoCADPath $dll
        if (-not (Test-Path $dllPath)) {
            $missingDlls += $dll
        }
    }
    
    if ($missingDlls.Count -gt 0) {
        Write-ColorOutput "WARNUNG: Folgende AutoCAD DLLs fehlen:" $WarningColor
        foreach ($dll in $missingDlls) {
            Write-ColorOutput "  - $dll" $WarningColor
        }
        Write-ColorOutput "AutoCAD-Pfad: $AutoCADPath" $WarningColor
        Write-ColorOutput "Build wird möglicherweise fehlschlagen!" $WarningColor
        return $false
    }
    
    Write-ColorOutput "Alle AutoCAD DLLs gefunden in: $AutoCADPath" $SuccessColor
    return $true
}

function Invoke-Clean {
    Write-ColorOutput "Bereinige Build-Verzeichnisse..." $InfoColor
    
    $directories = @("bin", "obj", $OutputDir)
    
    foreach ($dir in $directories) {
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force
            Write-ColorOutput "Verzeichnis gelöscht: $dir" $SuccessColor
        }
    }
}

function Invoke-Restore {
    Write-ColorOutput "Stelle NuGet Pakete wieder her..." $InfoColor
    
    $result = dotnet restore "AutoCAD_Plugin.sln"
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "FEHLER: NuGet Restore fehlgeschlagen" $ErrorColor
        exit 1
    }
    
    Write-ColorOutput "NuGet Restore erfolgreich" $SuccessColor
}

function Invoke-Build {
    Write-ColorOutput "Baue Solution..." $InfoColor
    
    $buildArgs = @(
        "build",
        "AutoCAD_Plugin.sln",
        "--configuration", $Configuration,
        "--no-restore"
    )
    
    if ($Verbose) {
        $buildArgs += "--verbosity", "detailed"
    }
    
    $result = & dotnet $buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "FEHLER: Build fehlgeschlagen" $ErrorColor
        exit 1
    }
    
    Write-ColorOutput "Build erfolgreich" $SuccessColor
}

function Invoke-Test {
    Write-ColorOutput "Führe Tests aus..." $InfoColor
    
    # Suche nach Test-Projekten
    $testProjects = Get-ChildItem -Recurse -Filter "*.Test.csproj" -Name
    $testProjects += Get-ChildItem -Recurse -Filter "*Tests.csproj" -Name
    
    if ($testProjects.Count -eq 0) {
        Write-ColorOutput "Keine Test-Projekte gefunden, überspringe Tests" $WarningColor
        return
    }
    
    $testArgs = @(
        "test",
        "--configuration", $Configuration,
        "--no-build",
        "--verbosity", "normal"
    )
    
    $result = & dotnet $testArgs
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "FEHLER: Tests fehlgeschlagen" $ErrorColor
        exit 1
    }
    
    Write-ColorOutput "Tests erfolgreich" $SuccessColor
}

function Invoke-Publish {
    Write-ColorOutput "Publiziere Plugins..." $InfoColor
    
    # Erstelle Output-Verzeichnis
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }
    
    $projects = @(
        "layer_batch_tools/layer_batch_tools.csproj",
        "make_block_cmd/make_block_cmd.csproj", 
        "move_lines_to_layer/move_lines_to_layer.csproj"
    )
    
    foreach ($project in $projects) {
        $projectName = Split-Path $project -Parent
        $outputPath = Join-Path $OutputDir $projectName
        
        Write-ColorOutput "Publiziere $projectName..." $InfoColor
        
        $publishArgs = @(
            "publish",
            $project,
            "--configuration", $Configuration,
            "--output", $outputPath,
            "--no-build"
        )
        
        $result = & dotnet $publishArgs
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "FEHLER: Publish von $projectName fehlgeschlagen" $ErrorColor
            exit 1
        }
        
        Write-ColorOutput "$projectName erfolgreich publiziert" $SuccessColor
    }
}

function Invoke-Package {
    Write-ColorOutput "Erstelle Plugin-Pakete..." $InfoColor
    
    Push-Location $OutputDir
    
    try {
        $directories = Get-ChildItem -Directory
        
        foreach ($dir in $directories) {
            $zipName = "${dir}_v$env:BUILD_NUMBER.zip"
            if (-not $env:BUILD_NUMBER) {
                $zipName = "${dir}_$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"
            }
            
            Write-ColorOutput "Erstelle Paket: $zipName" $InfoColor
            
            Compress-Archive -Path $dir -DestinationPath $zipName -Force
            
            if (Test-Path $zipName) {
                Write-ColorOutput "Paket erstellt: $zipName" $SuccessColor
            } else {
                Write-ColorOutput "FEHLER: Paket-Erstellung fehlgeschlagen für $dir" $ErrorColor
            }
        }
    }
    finally {
        Pop-Location
    }
}

# Hauptfunktion
function Main {
    Write-ColorOutput "=== AutoCAD Plugin Build Script (Jenkins Optimized) ===" $InfoColor
    Write-ColorOutput "Konfiguration: $Configuration" $InfoColor
    Write-ColorOutput "Output-Verzeichnis: $OutputDir" $InfoColor
    Write-ColorOutput "AutoCAD-Pfad: $AutoCADPath" $InfoColor
    Write-ColorOutput "=====================================================" $InfoColor
    
    # Prüfungen
    if (-not (Test-DotNetInstallation)) {
        exit 1
    }
    
    if (-not (Test-AutoCADReferences)) {
        Write-ColorOutput "WARNUNG: AutoCAD DLLs nicht gefunden, aber Build wird fortgesetzt" $WarningColor
    }
    
    # Build-Schritte
    if ($Clean) {
        Invoke-Clean
    }
    
    Invoke-Restore
    Invoke-Build
    
    if ($Test) {
        Invoke-Test
    }
    
    Invoke-Publish
    
    if ($Package) {
        Invoke-Package
    }
    
    Write-ColorOutput "=== Build abgeschlossen ===" $SuccessColor
}

# Skript ausführen
Main
