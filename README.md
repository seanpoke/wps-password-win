# 密码管理插件

## 项目简�?

密码管理插件是一�?Windows 桌面端独立后台插件，专为解决 WPS Office 加密文档密码管理痛点而设计。本插件无需 WPS 内部插件依赖、无主界面，通过系统�?API 模拟人为键鼠操作，实现加密文档时强密码一键生成与自动填充，以及打开加密文档时密码自动读取与填充功能�? 

插件�?**Win10�?4 位）+ WPS Office 12.1.0.25225�?4 位）** 为双核心开发与适配基准，兼�?Windows 7/11 �?WPS 2019/2021 等多版本环境，核心功能全程可用�?

## 功能特�?

### 核心功能

#### 1. 文档加密 - 一键生成强密码

- **智能对话框识�?*：后台实时监�?WPS 进程，自动识别「文档加密」和「密码加密」对话框
- **悬浮按钮展示**：在密码输入框右侧显示「一键生成密码」悬浮按钮，适配多种 DPI 缩放
- **强密码生�?*：自动生�?16 位符合规范的强密码（大写字母、小写字母、数字、特殊符号各至少 2 位）
- **自动填充**：同步填充至「密码」和「确认密码」双输入框，模拟人为打字，WPS 无感�?
- **元数据写�?*：加密保存后自动将密码写入文档自定义元数据，无外部存储风�?

#### 2. 打开加密文档 - 自动密码填充

- **解密对话框识�?*：自动识别打开加密文档时的「密码输入」对话框
- **文档路径解析**：无需人工操作，自动解析待打开文档的本地绝对路�?
- **密码精准读取**：从文档元数据中读取保存的密码信�?
- **自动填充打开**：模拟输入密码并点击「确定」按钮，实现无感知秒开

### 辅助功能

- **系统托盘运行**：后台静默运行，仅在系统托盘显示图标
- **右键快捷操作**：支持「打开安装目录」「退出插件」等操作
- **轻量提示�?*：异常时显示 3 秒自动关闭的提示，不阻塞 WPS 操作
- **多窗口支�?*：支持多显示器、WPS 窗口贴靠/缩放/移动/最大化/最小化

### 技术特�?

- **无侵入�?*：通过 Win32 API 实现，无需 WPS 内部插件依赖
- **跨版本兼�?*：支�?Windows 7/10/11 �?WPS 2019/2021/12.1.0.25225
- **高性能低占�?*：运行时 CPU 占用 �?%，内存占�?�?0MB
- **快速响�?*：对话框识别 �?00ms，整体自动化流程响应 �?00ms
- **安全存储**：密码仅存储在文档元数据中，无外部数据库

## 环境要求

### 操作系统要求

| 优先�?| 系统版本 | 架构 | 备注 |
|--------|----------|------|------|
| 推荐 | Windows 10 (64�? 1909 及以�?| x64 | 专业�?企业�?|
| 兼容 | Windows 11 (64�? | x64 | - |
| 兼容 | Windows 7 (64�? SP1 | x64 | 仅支�?100% DPI |

### WPS 版本要求

| 优先�?| WPS 版本 | 架构 | 备注 |
|--------|----------|------|------|
| 推荐 | WPS Office 12.1.0.25225 | x64 | - |
| 兼容 | WPS Office 2021 | x64 | - |
| 兼容 | WPS Office 2019 | x64 | - |

### 运行要求

- **.NET 运行�?*�?NET 6.0 Windows Desktop Runtime（插件安装包已内置，无需单独安装�?
- **权限要求**：普通用户权限即可运行，无需管理员权�?
- **屏幕要求**：支持多�?DPI 缩放�?00%/125%/150%�?

## 安装部署

### 安装方式

#### 方式一：一键安装（推荐�?

1. 下载最新版本的安装包（`PasswordManager-Setup.exe`�?
2. 双击安装包，按照提示完成安装
3. 安装完成后，插件自动启动并在系统托盘显示图标

#### 方式二：静默安装（企业批量部署）

```bash
# 命令行静默安�?
PasswordManager-Setup.exe /S

# 指定安装目录
PasswordManager-Setup.exe /S /D=C:\Program Files\PasswordManager
```

#### 方式三：便携式运�?

1. 下载便携版压缩包并解�?
2. 直接运行 `PasswordManager.exe`
3. 配置文件和数据存储在程序同目录下

### 卸载方式

1. 通过开始菜�?�?程序列表 �?卸载
2. 或双击安装包选择「卸载�?
3. 或使用控制面�?�?程序和功能卸�?

卸载后自动清理安装目录、快捷方式等，无冗余残留�?

### 自启动配�?

安装时可选勾选「开机自动启动」，或后续通过系统托盘右键菜单配置�?

## 基本使用方法

### 首次使用

1. 运行插件后，系统托盘显示图标
2. 双击托盘图标打开登录窗口
3. 输入服务器地址、端口、用户名和密�?
4. 点击「登录」完成认�?

### 加密文档

1. �?WPS 中打开或新�?docx 文档
2. 选择「文件」→「文档加密�?
3. 在密码输入框右侧，点击「一键生成密码」悬浮按�?
4. 插件自动生成并填�?16 位强密码
5. 直接点击 WPS「确定」完成加密并保存文档
6. 插件自动将密码写入文档元数据

### 打开加密文档

1. 双击打开由本插件加密�?docx 文档
2. 插件自动识别解密对话�?
3. 自动读取文档元数据中的密码并填充
4. 自动点击「确定」按�?
5. 文档无感知秒开，全程无需手动输入密码

### 托盘图标操作

- **左键双击**：打开登录/主窗�?
- **右键菜单**�?
  - 「主页」：打开登录窗口
  - 「打开安装目录」：打开程序安装位置
  - 「退出」：关闭插件

## 目录结构

```
wps-password-win/
├── PasswordManager/                    # 主程序目�?
�?  ├── Business/                         # 业务逻辑�?
�?  �?  ├── AutoFillAttemptManager.cs     # 自动填充尝试管理�?
�?  �?  ├── AutoFillAttemptRecord.cs      # 自动填充尝试记录
�?  �?  ├── FileMeta.cs                   # 文件元数据模�?
�?  �?  ├── FileMetaFactory.cs            # 文件元数据工�?
�?  �?  ├── FileMetaManager.cs            # 文件元数据管理器
�?  �?  ├── OfficeEncryptUtils.cs         # Office加密工具�?
�?  �?  ├── PasswordGenerator.cs          # 密码生成�?
�?  �?  └── ZipExtraFieldManager.cs       # ZIP扩展字段管理�?
�?  ├── Filler/                           # 密码填充模块
�?  �?  └── PasswordAutoFiller.cs        # 密码自动填充�?
�?  ├── Locator/                          # 窗口定位模块
�?  �?  └── QtWindowLocator.cs           # Qt窗口定位�?
�?  ├── Monitor/                          # 监控模块
�?  �?  ├── FileMonitor.cs               # 文件监控�?
�?  �?  └── WpsMonitor.cs                # WPS进程监控�?
�?  ├── Services/                         # 服务�?
�?  �?  ├── Report/                      # 报告服务
�?  �?  �?  └── PasswordReportService.cs # 密码报告服务
�?  �?  ├── Request/                     # HTTP请求服务
�?  �?  �?  ├── HttpRequestService.cs    # HTTP请求处理
�?  �?  �?  └── RequestFactory.cs       # 请求工厂
�?  �?  └── Routing/                     # 路由服务
�?  �?      ├── ApiResponse.cs          # API响应模型
�?  �?      └── ApiRoutes.cs            # API路由常量
�?  ├── UI/                              # 用户界面�?
�?  �?  ├── Controls/                   # 自定义控�?
�?  �?  �?  └── AuthTreeView.cs        # 权限树视图控�?
�?  �?  ├── AuthTreeForm.cs            # 权限树表�?
�?  �?  ├── FloatingButton.cs          # 悬浮按钮
�?  �?  ├── FloatingButtonManager.cs   # 悬浮按钮管理�?
�?  �?  ├── LoginForm.cs              # 登录表单
�?  �?  ├── NotificationForm.cs        # 通知表单
�?  �?  └── TrayIcon.cs               # 系统托盘图标
�?  ├── Utils/                          # 工具�?
�?  �?  ├── CryptoUtils.cs            # 加密解密工具
�?  �?  ├── GlobalState.cs            # 全局状态管�?
�?  �?  ├── Logger.cs                 # 日志记录�?
�?  �?  └── StorageManager.cs         # 存储管理�?
�?  ├── Program.cs                     # 应用程序入口
�?  └── PasswordManager.csproj     # 项目配置文件
├── doc/                               # 项目文档目录
�?  ├── 接口文档.md                    # API接口文档
�?  ├── 密码管理插件-任务执行文档.md
�?  ├── 密码管理插件-技术方案文�?v1.0.md
�?  ├── 密码管理插件-需求规格说明书.md
�?  ├── 密码管理插件-方案设计文档.md
�?  └── 通用业务技术文�?md
├── .vscode/                           # VSCode配置目录
�?  ├── launch.json                   # 调试启动配置
�?  └── tasks.json                    # 任务配置
├── .trae/                             # Trae规则目录
�?  ├── documents/                   # 规则文档
�?  �?  └── metadata_implementation_plan.md
�?  └── rules/                       # 构建规则
�?      └── buildmethod.md
├── pic/                              # 图片资源目录
└── README.md                         # 项目说明文档
```

## 核心模块说明

### Business（业务逻辑层）

负责核心业务逻辑的实现，包括文件元数据管理、Office文档加密验证、密码生成等核心功能�?

- **FileMeta**：`FileMeta` 是文件元数据的核心模型，包含文件路径、密码、权限等关键信息
- **OfficeEncryptUtils**：提供Office文档加密状态检测和密码验证功能
- **PasswordGenerator**：生成符合安全规范的强密�?
- **FileMetaManager/Factory**：管理文件元数据的读写和生命周期

### Filler（密码填充模块）

负责通过UI自动化技术实现密码的自动填充�?

- **PasswordAutoFiller**：核心填充器，通过Win32 API和UI Automation技术定位控件并填充密码

### Locator（窗口定位模块）

负责WPS窗口和控件的识别与定位�?

- **QtWindowLocator**：专门处理WPS使用的Qt框架窗口，支持子控件的精确定�?

### Monitor（监控模块）

负责系统级监控，包括WPS进程和文件变化�?

- **WpsMonitor**：监控WPS进程和密码对话框的显示状�?
- **FileMonitor**：监控文件系统变化，触发相关业务逻辑

### Services（服务层�?

提供HTTP请求、API路由等远程服务能力�?

- **HttpRequestService**：封装HTTP请求，支持GET/POST/PUT方法
- **ApiRoutes**：定义后端服务API路由常量

### UI（用户界面层�?

提供用户交互界面，包括登录窗口、托盘图标、悬浮按钮等�?

- **TrayIcon**：系统托盘图标管�?
- **LoginForm**：用户登录窗�?
- **FloatingButton**：密码输入框旁的悬浮按钮

### Utils（工具类�?

提供通用工具能力�?

- **GlobalState**：单例模式的全局状态管�?
- **StorageManager**：本地配置和数据的持久化存储
- **Logger**：统一的日志记录功�?

## 贡献指南

### 提交规范

- **提交格式**：`<type>(<scope>): <subject>`
- **type 类型**�?
  - `feat`：新增功�?
  - `fix`：修复问�?
  - `docs`：文档更�?
  - `style`：代码格式调�?
  - `refactor`：重�?
  - `perf`：性能优化
  - `test`：测试相�?
  - `chore`：构�?工具相关

### 开发环�?

1. **安装 .NET 6.0 SDK**
2. **安装 Visual Studio 2022**（推荐）�?VSCode + C#扩展
3. **克隆项目**：`git clone <repository_url>`
4. **还原依赖**：`dotnet restore`
5. **编译项目**：`dotnet build`
6. **运行调试**：`dotnet run`

### 测试规范

- 所有新增功能必须包含单元测�?
- 修复问题必须附带回归测试
- 测试覆盖率不得低�?80%
- 性能测试在双基准环境下验�?

### 代码审查

- 所有代码必须通过审查后才能合�?
- 审查重点：代码质量、安全性、性能、兼容�?
- 审查清单�?
  - [ ] 代码符合项目编码规范
  - [ ] 无硬编码敏感信息
  - [ ] 异常处理完善
  - [ ] 性能影响评估
  - [ ] 兼容性影响评�?

## API 接口

插件通过 HTTP 与后端服务通信，主要接口包括：

| 接口路径 | 方法 | 说明 |
|---------|------|------|
| `/account/login` | POST | 用户登录认证 |
| `/account/refresh-token` | POST | 刷新访问令牌 |
| `/account/logout` | POST | 用户登出 |
| `/doc/owner` | GET | 获取文档所属人 |
| `/doc/password` | GET | 获取文档密码 |
| `/doc/auth/tree` | GET | 获取文档权限�?|
| `/doc/auth/update` | PUT | 更新文档权限 |
| `/doc/save/log` | POST | 上报保存记录 |
| `/config/ldap` | GET | 获取LDAP配置 |
| `/config/refresh` | GET | 刷新配置 |
| `/config/encrypt` | POST | 公钥加密 |
| `/config/latest-key` | GET | 获取最新密�?|

详细接口规范请参�?[接口文档.md](doc/接口文档.md)�?

## 常见问题

### Q1: 插件无法识别 WPS 密码对话框？

**A**: 请确认以下条件：
1. WPS 版本是否为支持的版本�?019/2021/12.1.0.25225�?
2. 是否以管理员权限运行（部分系统需要）
3. 检查日志文件确认具体错误信�?

### Q2: 悬浮按钮位置偏移�?

**A**: 请检查：
1. 系统 DPI 缩放设置是否�?100%/125%/150%
2. 是否使用多显示器（仅支持主显示器�?
3. WPS 窗口是否处于最大化状�?

### Q3: 无法读取加密文档密码�?

**A**: 可能原因�?
1. 文档非本插件加密（缺少元数据�?
2. 文档元数据被第三方软件修�?
3. 文档正在被其他程序占�?

### Q4: 插件启动后托盘图标不显示�?

**A**: 尝试以下操作�?
1. 重启插件程序
2. 检查是否被安全软件拦截
3. 确认插件安装路径无特殊字�?

## 更新日志

详细更新日志请查看项�?Release 页面。主要版本更新包括：

- **v1.0.0**：初始版本发布，核心功能上线
- **v1.1.0**：优化窗口识别算法，提升兼容�?
- **v1.2.0**：新增多显示器支持，完善异常处理

## 许可�?

本项目采�?[MIT 许可证](LICENSE) 开源�?

Copyright (c) 2024 WPS Password Manager Team

## 联系方式

- **项目主页**：https://github.com/your-org/wps-password-win
- **问题反馈**：https://github.com/your-org/wps-password-win/issues
- **技术讨�?*：https://github.com/your-org/wps-password-win/discussions

## 致谢

感谢所有为本项目做出贡献的开发者！

本项目参考了以下开源项目和技术文档：
- [NPOI](https://github.com/nissl-lab/npoi) - Office 文档处理�?
- [DocumentFormat.OpenXml](https://github.com/dotnet/OpenXMLSDK) - Office Open XML 处理
- [.NET 6 Windows Desktop](https://docs.microsoft.com/zh-cn/dotnet/desktop/winforms/) - Windows Forms 开发框�?
- [Win32 API](https://docs.microsoft.com/zh-cn/windows/win32/api/) - Windows 平台 API
