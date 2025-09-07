pipeline {
    agent any // 使用任何可用的节点
    
    environment {
        DOTNET_VERSION = '9.0'
        AUTOCAD_VERSION = '2026'
        BUILD_CONFIGURATION = 'Release'
        ARTIFACTS_DIR = 'artifacts'
        AUTOCAD_PATH = 'C:\\Program Files\\Autodesk\\AutoCAD 2026'
    }
    
    stages {
        
        stage('Setup Environment') {
            steps {
                echo 'Setting up build environment...'
                script {
                    // Prüfe .NET Installation
                    def dotnetOutput = bat(
                        script: 'dotnet --version',
                        returnStdout: true
                    ).trim()
                    echo "Raw .NET output: ${dotnetOutput}"
                    
                    // 提取版本号（去除路径信息）
                    def dotnetVersion = dotnetOutput.split('\n').last().trim()
                    echo "Installed .NET version: ${dotnetVersion}"
                    
                    // .NET 9.0 是向后兼容的，也支持 .NET 8.0 项目
                    def versionParts = dotnetVersion.split('\\.')
                    if (versionParts.length >= 1) {
                        def majorVersion = versionParts[0] as Integer
                        if (majorVersion < 8) {
                            error "Required .NET 8.0 or higher not found. Installed version: ${dotnetVersion}"
                        }
                        echo "✓ .NET version check passed: ${dotnetVersion}"
                    } else {
                        error "Could not parse .NET version from: ${dotnetOutput}"
                    }
                    
                    // Prüfe AutoCAD Installation (可选)
                    def possiblePaths = [
                        'C:\\Program Files\\Autodesk\\AutoCAD 2026',
                        'C:\\Program Files\\Autodesk\\AutoCAD 2025',
                        'C:\\Program Files\\Autodesk\\AutoCAD 2024',
                        'C:\\Program Files (x86)\\Autodesk\\AutoCAD 2026',
                        'C:\\Program Files (x86)\\Autodesk\\AutoCAD 2025',
                        'C:\\Program Files (x86)\\Autodesk\\AutoCAD 2024'
                    ]
                    
                    def autocadFound = false
                    def foundPath = ""
                    
                    for (path in possiblePaths) {
                        echo "Checking AutoCAD path: ${path}"
                        // 检查多个可能的 AutoCAD DLL 文件
                        def dllFiles = ['accoremgd.dll', 'acdbmgd.dll', 'acmgd.dll', 'acad.exe']
                        def pathFound = false
                        
                        for (dllFile in dllFiles) {
                            def autocadExists = bat(
                                script: "if exist \"${path}\\${dllFile}\" echo \"FOUND\" else echo \"NOT_FOUND\"",
                                returnStdout: true
                            ).trim()
                            
                            echo "  Checking ${dllFile}: ${autocadExists}"
                            
                            if (autocadExists == "FOUND") {
                                echo "✓ Found AutoCAD file: ${path}\\${dllFile}"
                                autocadFound = true
                                foundPath = path
                                pathFound = true
                                break
                            }
                        }
                        
                        if (pathFound) break
                    }
                    
                    if (autocadFound) {
                        echo "✓ AutoCAD found at: ${foundPath}"
                    } else {
                        echo "⚠️  WARNING: AutoCAD not found in common installation paths"
                        echo "⚠️  Build will continue, but AutoCAD-specific features may not work"
                        echo "⚠️  Please ensure AutoCAD is installed for full functionality"
                    }
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
                        script: 'dir /s /b *.Test.csproj *Tests.csproj 2>nul || echo "NO_TESTS"',
                        returnStdout: true
                    ).trim()
                    
                    if (testProjects && testProjects != "NO_TESTS") {
                        echo "Found test projects: ${testProjects}"
                        bat "dotnet test --configuration ${BUILD_CONFIGURATION} --no-build --verbosity normal --logger trx --results-directory TestResults"
                    } else {
                        echo 'No test projects found, skipping tests'
                        // Create a dummy test result to prevent pipeline failure
                        bat 'mkdir TestResults 2>nul || echo "TestResults directory already exists"'
                    }
                }
            }
            post {
                always {
                    // Publish test results if they exist
                    script {
                        def testResultsExist = bat(
                            script: 'if exist "TestResults\\*.trx" echo "EXISTS" else echo "NOT_EXISTS"',
                            returnStdout: true
                        ).trim()
                        
                        if (testResultsExist == "EXISTS") {
                            echo "Publishing test results..."
                            publishTestResults testResultsPattern: 'TestResults/*.trx'
                        } else {
                            echo "No test results to publish"
                        }
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
                anyOf {
                    branch 'dev'
                    branch 'main'
                }
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
            // 注意：在 always 块中删除目录可能会影响 artifact 归档
            // 如果需要在归档后清理，应该在 success 块的最后进行
        }
        
        success {
            echo 'Build succeeded!'
            script {
                // Archive artifacts only if they exist
                def artifactsExist = bat(
                    script: "if exist \"${ARTIFACTS_DIR}\\*.zip\" echo \"EXISTS\" else echo \"NOT_EXISTS\"",
                    returnStdout: true
                ).trim()
                
                if (artifactsExist == "EXISTS") {
                    echo "Archiving artifacts..."
                    archiveArtifacts artifacts: "${ARTIFACTS_DIR}/*.zip", fingerprint: true
                } else {
                    echo "No artifacts to archive"
                }
                
                // Optional: Benachrichtigung bei Erfolg
                // emailext (
                //     subject: "Build Success: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
                //     body: "Build succeeded for commit ${env.GIT_COMMIT}",
                //     to: "your-email@example.com"
                // )
                
                // 清理工作空间
                echo "Cleaning up workspace after successful build..."
                deleteDir()
            }
        }
        
        failure {
            echo 'Build failed!'
            // Optional: Benachrichtigung bei Fehler
            // emailext (
            //     subject: "Build Failed: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
            //     body: "Build failed for commit ${env.GIT_COMMIT}. Check console output for details.",
            //     to: "your-email@example.com"
            // )
        }
        
        unstable {
            echo 'Build unstable!'
        }
    }
}
