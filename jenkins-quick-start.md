# Jenkins 快速启动指南

## 第一步：准备Jenkins服务器

### 1.1 安装Jenkins
```powershell
# 下载Jenkins Windows安装包
# 访问: https://www.jenkins.io/download/
# 选择: Windows (jenkins.msi)

# 或者使用Chocolatey安装
choco install jenkins
```

### 1.2 安装必要软件
```powershell
# 安装.NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8

# 安装Git
winget install Git.Git

# 安装AutoCAD 2026 (完整安装)
# 从Autodesk官网下载并安装
```

### 1.3 验证安装
```powershell
# 检查.NET版本
dotnet --version
# 应该显示: 8.x.x

# 检查Git
git --version

# 检查AutoCAD DLLs
dir "C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll"
```

## 第二步：配置Jenkins

### 2.1 启动Jenkins
```powershell
# 启动Jenkins服务
net start jenkins

# 或者通过服务管理器启动
services.msc
# 找到Jenkins服务并启动
```

### 2.2 访问Jenkins Web界面
```
浏览器打开: http://localhost:8080
```

### 2.3 安装必要插件
在Jenkins中安装以下插件：
- **Pipeline** (通常已安装)
- **Git** 
- **Email Extension**
- **Build Timeout**
- **Workspace Cleanup**

安装步骤：
1. 管理Jenkins → 管理插件
2. 在"可选插件"中搜索并安装上述插件

## 第三步：创建Jenkins Job

### 3.1 创建新Pipeline Job
1. 点击"新建任务"
2. 输入任务名称：`AutoCAD-Plugin-CI`
3. 选择"Pipeline"
4. 点击"确定"

### 3.2 配置Pipeline
在Job配置页面：

#### 基本设置
- **描述**: "AutoCAD .NET插件CI/CD管道"
- **丢弃旧的构建**: 勾选
  - 保持构建的最大个数: 10
  - 保持构建产物的最大个数: 5

#### 构建触发器
- **GitHub hook trigger for GITScm polling**: 勾选 (如果使用GitHub)
- **Poll SCM**: `H/5 * * * *` (每5分钟检查一次)

#### Pipeline设置
- **Definition**: Pipeline script from SCM
- **SCM**: Git
- **Repository URL**: 您的Git仓库URL
- **Credentials**: 添加Git凭据 (如果需要)
- **Branch Specifier**: `*/main`
- **Script Path**: `Jenkinsfile`

### 3.3 保存配置
点击"保存"

## 第四步：准备Git仓库

### 4.1 推送代码到Git仓库
```bash
# 初始化Git仓库
git init

# 添加所有文件
git add .

# 提交代码
git commit -m "Initial commit with Jenkins CI/CD"

# 添加远程仓库
git remote add origin <您的Git仓库URL>

# 推送代码
git push -u origin main
```

### 4.2 确保Jenkinsfile在根目录
```
您的项目/
├── Jenkinsfile          ← 必须在这里
├── build.ps1
├── jenkins-setup.md
├── AutoCAD_Plugin.sln
├── layer_batch_tools/
├── make_block_cmd/
└── move_lines_to_layer/
```

## 第五步：运行第一个构建

### 5.1 手动触发构建
1. 在Jenkins中打开您的Job
2. 点击"立即构建"
3. 观察构建进度

### 5.2 查看构建日志
1. 点击构建号 (如 #1)
2. 点击"控制台输出"
3. 查看详细日志

## 第六步：验证构建结果

### 6.1 检查构建产物
构建成功后：
1. 在构建页面点击"构建产物"
2. 应该看到ZIP文件：
   - `layer_batch_tools_v1.zip`
   - `make_block_cmd_v1.zip`
   - `move_lines_to_layer_v1.zip`

### 6.2 下载并测试
1. 下载ZIP文件
2. 解压到AutoCAD插件目录
3. 在AutoCAD中测试插件

## 常见问题解决

### 问题1：.NET未找到
```
错误: 'dotnet' is not recognized as an internal or external command
```
**解决方案**：
```powershell
# 重新安装.NET SDK
winget install Microsoft.DotNet.SDK.8

# 重启Jenkins服务
net stop jenkins
net start jenkins
```

### 问题2：AutoCAD DLLs未找到
```
错误: AutoCAD 2026 not found at: C:\Program Files\Autodesk\AutoCAD 2026
```
**解决方案**：
1. 确保AutoCAD 2026完整安装
2. 检查DLL文件是否存在：
```powershell
dir "C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll"
```

### 问题3：Git权限问题
```
错误: Authentication failed
```
**解决方案**：
1. 在Jenkins中添加Git凭据
2. 管理Jenkins → 管理凭据
3. 添加用户名/密码或SSH密钥

### 问题4：PowerShell执行策略
```
错误: PowerShell execution policy
```
**解决方案**：
```powershell
# 以管理员身份运行PowerShell
Set-ExecutionPolicy RemoteSigned -Scope LocalMachine -Force
```

## 自动化部署配置

### 配置部署服务器路径
在Jenkinsfile中修改部署路径：

```groovy
// 修改为您的实际服务器路径
if exist "\\\\您的服务器\\插件目录" (
    copy "${ARTIFACTS_DIR}\\*.zip" "\\\\您的服务器\\插件目录\\"
)
```

### 配置邮件通知
取消注释邮件配置：

```groovy
emailext (
    subject: "Build Success: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
    body: "构建成功！插件已部署。",
    to: "your-email@example.com"
)
```

## 下一步优化

### 1. 设置Webhook (自动构建)
如果使用GitHub：
1. 在GitHub仓库设置中添加Webhook
2. URL: `http://您的Jenkins服务器:8080/github-webhook/`
3. 选择"Push events"

### 2. 配置多环境部署
- 开发环境：develop分支
- 测试环境：staging分支  
- 生产环境：main分支

### 3. 添加更多测试
- 单元测试
- 集成测试
- 代码质量检查

## 成功标志

当您看到以下内容时，说明Jenkins CI/CD已成功运行：

✅ **构建成功**：绿色圆球
✅ **构建产物**：ZIP文件可下载
✅ **自动部署**：插件自动部署到服务器
✅ **邮件通知**：构建状态邮件通知

恭喜！您的AutoCAD插件CI/CD管道已成功运行！🎉


