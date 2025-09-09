# Jenkins CI/CD Setup für AutoCAD Plugin (Ohne Docker)

Diese Anleitung beschreibt, wie Sie Jenkins für die kontinuierliche Integration und Bereitstellung Ihrer AutoCAD Plugins konfigurieren - **ohne Docker**, direkt auf dem Jenkins-Server.

## Voraussetzungen

### Jenkins Server
- Jenkins 2.400 oder höher
- **Windows Server** (erforderlich für AutoCAD DLLs)
- Mindestens 4 GB RAM
- 20 GB freier Speicherplatz
- **AutoCAD 2026 installiert** (für DLL-Referenzen)

### Erforderliche Plugins
Installieren Sie folgende Jenkins Plugins:
- **Pipeline** (bereits enthalten)
- **Git** - für Git-Integration
- **Email Extension** - für E-Mail-Benachrichtigungen
- **Build Timeout** - für Build-Timeout-Management
- **Timestamper** - für Build-Zeitstempel
- **Workspace Cleanup** - für Workspace-Bereinigung

### System-Anforderungen
- **.NET 9.0 SDK** (auf Jenkins-Server installiert)
- **Git** (für Source Code Management)
- **PowerShell 5.1 oder höher**
- **AutoCAD 2026** (vollständig installiert auf Jenkins-Server)
- **Windows Server 2019/2022** (empfohlen)

## Jenkins Job Konfiguration

### 1. Neuen Pipeline Job erstellen

1. Melden Sie sich bei Jenkins an
2. Klicken Sie auf "New Item"
3. Geben Sie einen Job-Namen ein (z.B. "AutoCAD-Plugin-CI")
4. Wählen Sie "Pipeline" als Job-Typ
5. Klicken Sie auf "OK"

### 2. Pipeline-Konfiguration

#### Allgemeine Einstellungen
- **Beschreibung**: "CI/CD Pipeline für AutoCAD .NET Plugins"
- **Discard old builds**: Aktiviert
  - Max # of builds to keep: 10
  - Max # of builds to keep with artifacts: 5

#### Build-Trigger
- **GitHub hook trigger for GITScm polling**: Aktiviert (falls GitHub verwendet wird)
- **Poll SCM**: `H/5 * * * *` (alle 5 Minuten prüfen)
- **Build periodically**: Optional für regelmäßige Builds

#### Pipeline-Definition
- **Definition**: Pipeline script from SCM
- **SCM**: Git
- **Repository URL**: Ihre Git-Repository-URL
- **Credentials**: Git-Zugangsdaten (falls erforderlich)
- **Branch Specifier**: `*/main` oder `*/develop`
- **Script Path**: `Jenkinsfile`

#### Agent-Konfiguration
- **Label**: `windows` (für Windows-Agent)
- **Verwendet**: Direkten Jenkins-Server (kein Docker)

### 3. Umgebungsvariablen

Fügen Sie folgende Umgebungsvariablen hinzu:

```groovy
environment {
    DOTNET_VERSION = '9.0'
    AUTOCAD_VERSION = '2026'
    BUILD_CONFIGURATION = 'Release'
    ARTIFACTS_DIR = 'artifacts'
    AUTOCAD_PATH = 'C:\\Program Files\\Autodesk\\AutoCAD 2026'
    // Optional: Deployment-Pfade
    STAGING_PATH = '\\\\staging-server\\plugins'
    PRODUCTION_PATH = '\\\\production-server\\plugins'
}
```

### 4. Build-Umgebung konfigurieren

#### .NET SDK Installation
1. Gehen Sie zu "Manage Jenkins" > "Global Tool Configuration"
2. Fügen Sie .NET SDK hinzu:
   - **Name**: `dotnet-9.0`
   - **Installation**: Automatisch
   - **Version**: 9.0.x

#### AutoCAD Installation auf Jenkins-Server
Da wir **ohne Docker** arbeiten, muss AutoCAD direkt auf dem Jenkins-Server installiert sein:

1. **AutoCAD 2026 vollständig installieren** auf dem Jenkins-Server
2. **Standard-Installationspfad verwenden**:
   ```
   C:\Program Files\Autodesk\AutoCAD 2026\
   ```

3. **Erforderliche DLLs verifizieren**:
   ```
   C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll
   C:\Program Files\Autodesk\AutoCAD 2026\AcCui.dll
   C:\Program Files\Autodesk\AutoCAD 2026\acdbmgd.dll
   C:\Program Files\Autodesk\AutoCAD 2026\AcDx.dll
   C:\Program Files\Autodesk\AutoCAD 2026\acmgd.dll
   ```

4. **Projektdateien verwenden Standard-Pfade** (keine Änderung erforderlich):
   ```xml
   <Reference Include="accoremgd">
       <HintPath>..\..\..\..\..\..\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll</HintPath>
   </Reference>
   ```

## Pipeline-Stages erklärt

### 1. Checkout
Lädt den Quellcode aus dem Git-Repository.

### 2. Setup Environment
Überprüft die .NET-Installation, Version und AutoCAD-Verfügbarkeit.

### 3. Restore Dependencies
Stellt NuGet-Pakete wieder her.

### 4. Build Solution
Kompiliert die gesamte Solution.

### 5. Run Tests
Führt Unit-Tests aus (falls vorhanden).

### 6. Publish Artifacts
Erstellt deploybare Versionen der Plugins.

### 7. Package Plugins
Erstellt ZIP-Archive für die Distribution.

### 8. Save to Local Directory
Speichert die ZIP-Dateien in einem lokalen Verzeichnis (`C:\JenkinsArtifacts\AutoCAD-Plugin-CI\`).

### 9. Deploy to Staging
Stellt Plugins in der Staging-Umgebung bereit.

### 10. Deploy to Production
Stellt Plugins in der Produktionsumgebung bereit.

## Deployment-Konfiguration

### Staging-Deployment
```groovy
stage('Deploy to Staging') {
    when {
        branch 'develop'
    }
    steps {
        script {
            bat """
                if exist "\\\\staging-server\\plugins" (
                    copy "${ARTIFACTS_DIR}\\*.zip" "\\\\staging-server\\plugins\\"
                    echo "Staging deployment completed"
                ) else (
                    echo "Staging server not available"
                )
            """
        }
    }
}
```

### Production-Deployment
```groovy
stage('Deploy to Production') {
    when {
        branch 'main'
    }
    steps {
        script {
            bat """
                if exist "\\\\production-server\\plugins" (
                    echo "Creating backup..."
                    if not exist "\\\\production-server\\plugins\\backup" mkdir "\\\\production-server\\plugins\\backup"
                    copy "\\\\production-server\\plugins\\*.zip" "\\\\production-server\\plugins\\backup\\"
                    
                    echo "Deploying new version..."
                    copy "${ARTIFACTS_DIR}\\*.zip" "\\\\production-server\\plugins\\"
                    echo "Production deployment completed"
                ) else (
                    echo "Production server not available"
                )
            """
        }
    }
}
```

## E-Mail-Benachrichtigungen

Konfigurieren Sie E-Mail-Benachrichtigungen für Build-Status:

```groovy
post {
    success {
        emailext (
            subject: "Build Success: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
            body: """
                Build erfolgreich abgeschlossen!
                
                Job: ${env.JOB_NAME}
                Build: ${env.BUILD_NUMBER}
                Commit: ${env.GIT_COMMIT}
                Branch: ${env.GIT_BRANCH}
                
                Artifacts: ${env.BUILD_URL}artifact/
            """,
            to: "team@yourcompany.com"
        )
    }
    
    failure {
        emailext (
            subject: "Build Failed: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
            body: """
                Build fehlgeschlagen!
                
                Job: ${env.JOB_NAME}
                Build: ${env.BUILD_NUMBER}
                Commit: ${env.GIT_COMMIT}
                Branch: ${env.GIT_BRANCH}
                
                Console Output: ${env.BUILD_URL}console
            """,
            to: "dev-team@yourcompany.com"
        )
    }
}
```

## Sicherheitsüberlegungen

### Credentials Management
1. Verwenden Sie Jenkins Credentials für:
   - Git-Zugangsdaten
   - Deployment-Server-Zugang
   - E-Mail-Konfiguration

2. Erstellen Sie Credentials:
   - Gehen Sie zu "Manage Jenkins" > "Manage Credentials"
   - Fügen Sie neue Credentials hinzu
   - Verwenden Sie IDs in der Pipeline

### Netzwerk-Sicherheit
- Beschränken Sie Jenkins-Zugriff auf vertrauenswürdige IPs
- Verwenden Sie HTTPS für Jenkins-Web-Interface
- Konfigurieren Sie Firewall-Regeln für Deployment-Server

## Troubleshooting

### Häufige Probleme

#### 1. .NET nicht gefunden
```
Lösung: Stellen Sie sicher, dass .NET 9.0 SDK installiert ist und im PATH verfügbar
```

#### 2. AutoCAD DLLs nicht gefunden
```
Lösung: Überprüfen Sie die Pfade in den .csproj-Dateien und stellen Sie sicher, dass die DLLs verfügbar sind
```

#### 3. PowerShell Execution Policy
```
Lösung: Setzen Sie die Execution Policy: Set-ExecutionPolicy RemoteSigned -Scope LocalMachine
```

#### 4. AutoCAD DLLs nicht gefunden
```
Lösung: Stellen Sie sicher, dass AutoCAD 2026 vollständig auf dem Jenkins-Server installiert ist
```

#### 5. Build-Timeout
```
Lösung: Erhöhen Sie das Build-Timeout in den Jenkins-Einstellungen
```

### Log-Analyse
- Überprüfen Sie die Jenkins-Console-Ausgabe
- Verwenden Sie `-Verbose` Flag im PowerShell-Skript für detaillierte Ausgaben
- Aktivieren Sie Debug-Logging in Jenkins

## Erweiterte Konfiguration

### Parallel Builds
```groovy
stage('Parallel Builds') {
    parallel {
        stage('Build Plugin 1') {
            steps {
                sh 'dotnet build layer_batch_tools/layer_batch_tools.csproj'
            }
        }
        stage('Build Plugin 2') {
            steps {
                sh 'dotnet build make_block_cmd/make_block_cmd.csproj'
            }
        }
        stage('Build Plugin 3') {
            steps {
                sh 'dotnet build move_lines_to_layer/move_lines_to_layer.csproj'
            }
        }
    }
}
```

### Conditional Deployment
```groovy
stage('Deploy') {
    when {
        anyOf {
            branch 'main'
            branch 'develop'
        }
        not {
            changeRequest()
        }
    }
    steps {
        // Deployment-Logik
    }
}
```

## Monitoring und Metriken

### Build-Statistiken
- Überwachen Sie Build-Zeiten
- Verfolgen Sie Erfolgs-/Fehlerquoten
- Analysieren Sie Test-Coverage

### Plugin-Versionierung
- Verwenden Sie semantische Versionierung
- Automatische Versionierung basierend auf Git-Tags
- Changelog-Generierung

## Zusammenfassung

Diese **Docker-freie** Konfiguration bietet eine robuste CI/CD-Pipeline für Ihre AutoCAD Plugins:

### Vorteile ohne Docker:
- ✅ **Direkter Zugriff** auf AutoCAD DLLs
- ✅ **Einfachere Konfiguration** - keine Container-Komplexität
- ✅ **Bessere Performance** - keine Container-Overhead
- ✅ **Windows-native** - optimiert für AutoCAD-Umgebung
- ✅ **Einfacheres Troubleshooting** - direkte Server-Zugriff

### Pipeline-Features:
- 🔄 **Automatischer Build** bei Code-Commits
- 🧪 **Test-Integration** (falls Tests vorhanden)
- 📦 **Automatische Paketierung** in ZIP-Dateien
- 🚀 **Staging/Production Deployment**
- 📧 **E-Mail-Benachrichtigungen**
- 🔍 **Detaillierte Logs** und Fehlerbehandlung

Diese Konfiguration ist speziell für AutoCAD .NET Plugins optimiert und läuft direkt auf dem Jenkins-Server ohne Docker-Komplexität.
