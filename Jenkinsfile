pipeline {
    agent {
        label 'windows' // Verwendet Windows-Agent für AutoCAD DLLs
    }
    
    environment {
        DOTNET_VERSION = '8.0'
        AUTOCAD_VERSION = '2026'
        BUILD_CONFIGURATION = 'Release'
        ARTIFACTS_DIR = 'artifacts'
        AUTOCAD_PATH = 'C:\\Program Files\\Autodesk\\AutoCAD 2026'
    }
    
    stages {
        stage('Checkout') {
            steps {
                echo 'Checking out source code...'
                checkout scm
            }
        }
        
        stage('Setup Environment') {
            steps {
                echo 'Setting up build environment...'
                script {
                    // Prüfe .NET Installation
                    def dotnetVersion = bat(
                        script: 'dotnet --version',
                        returnStdout: true
                    ).trim()
                    echo "Installed .NET version: ${dotnetVersion}"
                    
                    if (!dotnetVersion.startsWith('8.')) {
                        error "Required .NET 8.0 not found. Installed version: ${dotnetVersion}"
                    }
                    
                    // Prüfe AutoCAD Installation
                    def autocadExists = bat(
                        script: 'if exist "${AUTOCAD_PATH}\\accoremgd.dll" echo "FOUND" else echo "NOT_FOUND"',
                        returnStdout: true
                    ).trim()
                    
                    if (autocadExists != "FOUND") {
                        error "AutoCAD 2026 not found at: ${AUTOCAD_PATH}"
                    }
                    
                    echo "AutoCAD 2026 found at: ${AUTOCAD_PATH}"
                }
            }
        }
        
        stage('Restore Dependencies') {
            steps {
                echo 'Restoring NuGet packages...'
                bat 'dotnet restore AutoCAD_Plugin.sln'
            }
        }
        
        stage('Build Solution') {
            steps {
                echo 'Building solution...'
                bat "dotnet build AutoCAD_Plugin.sln --configuration ${BUILD_CONFIGURATION} --no-restore"
            }
        }
        
        stage('Run Tests') {
            steps {
                echo 'Running tests...'
                script {
                    // Prüfe ob Test-Projekte vorhanden sind
                    def testProjects = bat(
                        script: 'dir /s /b *.Test.csproj *Tests.csproj 2>nul',
                        returnStdout: true
                    ).trim()
                    
                    if (testProjects) {
                        bat "dotnet test --configuration ${BUILD_CONFIGURATION} --no-build --verbosity normal"
                    } else {
                        echo 'No test projects found, skipping tests'
                    }
                }
            }
        }
        
        stage('Publish Artifacts') {
            steps {
                echo 'Publishing artifacts...'
                script {
                    // Erstelle Artifacts-Verzeichnis
                    bat "if not exist ${ARTIFACTS_DIR} mkdir ${ARTIFACTS_DIR}"
                    
                    // Publish jedes Plugin-Projekt
                    def projects = [
                        'layer_batch_tools/layer_batch_tools.csproj',
                        'make_block_cmd/make_block_cmd.csproj',
                        'move_lines_to_layer/move_lines_to_layer.csproj'
                    ]
                    
                    projects.each { project ->
                        def projectName = project.split('/')[0]
                        echo "Publishing ${projectName}..."
                        bat """
                            dotnet publish ${project} ^
                                --configuration ${BUILD_CONFIGURATION} ^
                                --output ${ARTIFACTS_DIR}\\${projectName} ^
                                --no-build
                        """
                    }
                }
            }
        }
        
        stage('Package Plugins') {
            steps {
                echo 'Packaging plugins...'
                script {
                    // Erstelle ZIP-Archive für jedes Plugin mit PowerShell
                    bat """
                        powershell -Command "& {
                            Set-Location '${ARTIFACTS_DIR}'
                            Get-ChildItem -Directory | ForEach-Object {
                                \$pluginName = \$_.Name
                                \$zipName = \"\${pluginName}_v${BUILD_NUMBER}.zip\"
                                Write-Host \"Creating package for \$pluginName...\"
                                Compress-Archive -Path \$pluginName -DestinationPath \$zipName -Force
                                Write-Host \"Created: \$zipName\"
                            }
                        }"
                    """
                }
            }
        }
        
        stage('Deploy to Staging') {
            when {
                branch 'develop' || branch 'main'
            }
            steps {
                echo 'Deploying to staging environment...'
                script {
                    // Beispiel: Kopieren zu Staging-Server
                    echo "Deploying artifacts to staging server..."
                    bat """
                        if exist "\\\\staging-server\\plugins" (
                            copy "${ARTIFACTS_DIR}\\*.zip" "\\\\staging-server\\plugins\\"
                            echo "Staging deployment completed"
                        ) else (
                            echo "Staging server not available, skipping deployment"
                        )
                    """
                }
            }
        }
        
        stage('Deploy to Production') {
            when {
                branch 'main'
            }
            steps {
                echo 'Deploying to production environment...'
                script {
                    // Beispiel: Kopieren zu Production-Server mit Backup
                    echo "Deploying artifacts to production server..."
                    bat """
                        if exist "\\\\production-server\\plugins" (
                            echo "Creating backup..."
                            if not exist "\\\\production-server\\plugins\\backup" mkdir "\\\\production-server\\plugins\\backup"
                            copy "\\\\production-server\\plugins\\*.zip" "\\\\production-server\\plugins\\backup\\"
                            
                            echo "Deploying new version..."
                            copy "${ARTIFACTS_DIR}\\*.zip" "\\\\production-server\\plugins\\"
                            echo "Production deployment completed"
                        ) else (
                            echo "Production server not available, skipping deployment"
                        )
                    """
                }
            }
        }
    }
    
    post {
        always {
            echo 'Cleaning up workspace...'
            cleanWs()
        }
        
        success {
            echo 'Build succeeded!'
            script {
                // Archive artifacts
                archiveArtifacts artifacts: "${ARTIFACTS_DIR}/*.zip", fingerprint: true
                
                // Optional: Benachrichtigung bei Erfolg
                // emailext (
                //     subject: "Build Success: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
                //     body: "Build succeeded for commit ${env.GIT_COMMIT}",
                //     to: "your-email@example.com"
                // )
            }
        }
        
        failure {
            echo 'Build failed!'
            script {
                // Optional: Benachrichtigung bei Fehler
                // emailext (
                //     subject: "Build Failed: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
                //     body: "Build failed for commit ${env.GIT_COMMIT}. Check console output for details.",
                //     to: "your-email@example.com"
                // )
            }
        }
        
        unstable {
            echo 'Build unstable!'
        }
    }
}
