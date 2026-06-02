# WPS 密码管理插件

一款专为 WPS Office 加密文档设计的密码管理插件，通过 Win32 API 实现无侵入式密码自动生成与填充功能。

## 功能特性

### 🔐 文档加密 - 一键生成强密码
- **智能对话框识别**：后台实时监控 WPS 进程，自动识别「密码加密」对话框
- **悬浮按钮操作**：在密码输入框右侧显示操作按钮组，支持 DPI 自适应
- **强密码生成**：自动生成符合规范的强密码（大写、小写、数字、特殊符号各至少 2 位）
- **自动填充**：同步填充至「密码」和「确认密码」双输入框
- **元数据写入**：加密保存后自动将密码写入文档自定义元数据

### 🔓 文档解密 - 自动密码填充
- **解密对话框识别**：自动识别打开加密文档时的「文档已加密」对话框
- **路径自动解析**：无需人工操作，自动解析待打开文档的本地绝对路径
- **密码精准读取**：从文档元数据中读取保存的密码信息
- **自动填充打开**：模拟输入密码并点击「确定」按钮，实现无感知秒开

### 👤 文档权限管理
- **权限树展示**：可视化展示文档的权限层级结构
- **权限编辑**：支持对文档权限进行增删改操作
- **实时同步**：权限变更实时同步至服务器

### ⚙️ 系统集成
- **系统托盘运行**：后台静默运行，仅在系统托盘显示图标
- **右键快捷操作**：支持「打开安装目录」「显示日志」「退出插件」等操作
- **轻量提示**：异常时显示自动关闭的提示，不阻塞 WPS 操作
- **多窗口支持**：支持多显示器、窗口贴靠/缩放/移动

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                      UI 层                                  │
│  TrayIcon | LoginForm | FloatingButton | AuthTreeForm       │
├─────────────────────────────────────────────────────────────┤
│                    业务逻辑层                                │
│  PasswordGenerator | FileMetaManager | OfficeEncryptUtils   │
├─────────────────────────────────────────────────────────────┤
│                    核心服务层                                │
│  WpsMonitor | QtWindowLocator | PasswordAutoFiller          │
├─────────────────────────────────────────────────────────────┤
│                    工具层                                    │
│  Logger | GlobalState | StorageManager | CryptoUtils        │
└─────────────────────────────────────────────────────────────┘
```

### 模块说明

| 模块 | 职责 | 核心文件 |
|------|------|----------|
| **Business** | 业务逻辑处理 | `PasswordGenerator.cs`, `FileMeta.cs`, `FileMetaManager.cs` |
| **Filler** | 密码自动填充 | `PasswordAutoFiller.cs` |
| **Locator** | Qt 窗口定位 | `QtWindowLocator.cs` |
| **Monitor** | WPS 进程监控 | `WpsMonitor.cs` |
| **Services** | HTTP 请求服务 | `HttpRequestService.cs`, `ApiRoutes.cs` |
| **UI** | 用户界面组件 | `TrayIcon.cs`, `FloatingButton.cs`, `LoginForm.cs` |
| **Utils** | 工具类 | `Logger.cs`, `GlobalState.cs`, `CryptoUtils.cs` |

## 环境要求

### 操作系统
| 优先级 | 系统版本 | 架构 | 备注 |
|--------|----------|------|------|
| 推荐 | Windows 10 1909+ | x64 | 支持 DPI 缩放 |
| 兼容 | Windows 11 | x64 | - |
| 兼容 | Windows 7 SP1 | x64 | 仅支持 100% DPI |

### WPS 版本
| 优先级 | WPS 版本 | 架构 |
|--------|----------|------|
| 推荐 | WPS Office 12.1.0.25225 | x64 |
| 兼容 | WPS Office 2021 | x64 |
| 兼容 | WPS Office 2019 | x64 |

### 运行要求
- .NET 6.0 Windows Desktop Runtime（安装包已内置）
- 管理员权限运行
- 支持多显示器 DPI 缩放（100%/125%/150%）

## 安装部署

### 一键安装（推荐）
1. 下载 `PasswordManager-Setup.exe`
2. 双击运行安装包
3. 安装完成后自动启动

### 静默安装（企业部署）
```bash
# 静默安装
PasswordManager-Setup.exe /S

# 指定安装目录
PasswordManager-Setup.exe /S /D=C:\Program Files\PasswordManager
```

### 便携式运行
1. 下载便携版压缩包并解压
2. 直接运行 `PasswordManager.exe`
3. 配置文件存储在程序同目录

## 使用方法

### 首次使用
1. 运行插件后，系统托盘显示图标
2. 双击托盘图标打开登录窗口
3. 输入服务器地址、端口、用户名和密码
4. 点击「登录」完成认证

### 加密文档
1. 在 WPS 中打开或新建文档
2. 选择「文件」→「文档加密」
3. 在密码输入框右侧，点击「生成密码」按钮
4. 插件自动生成并填充密码
5. 点击 WPS「确定」完成加密

### 打开加密文档
1. 双击打开由本插件加密的文档
2. 插件自动识别解密对话框
3. 自动读取元数据中的密码并填充
4. 自动点击「确定」，文档无感知打开

### 悬浮按钮功能
- **生成密码**：自动生成并填充强密码
- **提取密码**：将当前密码复制到剪贴板
- **文档权限**：查看和编辑文档的权限信息（需写权限）

## 目录结构

```
wps-password-win/
├── PasswordManager/                    # 主程序目录
│   ├── Business/                      # 业务逻辑层
│   │   ├── AutoFillAttemptManager.cs  # 自动填充尝试管理
│   │   ├── AutoFillAttemptRecord.cs   # 自动填充尝试记录
│   │   ├── FileMeta.cs                # 文件元数据模型
│   │   ├── FileMetaFactory.cs         # 文件元数据工厂
│   │   ├── FileMetaManager.cs         # 文件元数据管理
│   │   ├── FileStateManager.cs        # 文件状态管理
│   │   ├── OfficeEncryptUtils.cs      # Office 加密工具
│   │   ├── PasswordGenerator.cs       # 密码生成器
│   │   └── ZipExtraFieldManager.cs    # ZIP 扩展字段管理
│   ├── Filler/                        # 密码填充模块
│   │   └── PasswordAutoFiller.cs      # 密码自动填充器
│   ├── Locator/                       # 窗口定位模块
│   │   └── QtWindowLocator.cs         # Qt 窗口定位器
│   ├── Monitor/                       # 监控模块
│   │   └── WpsMonitor.cs              # WPS 进程监控器
│   ├── Services/                      # 服务层
│   │   ├── Report/                    # 报告服务
│   │   │   └── PasswordReportService.cs
│   │   ├── Request/                   # HTTP 请求服务
│   │   │   ├── HttpRequestService.cs
│   │   │   └── RequestFactory.cs
│   │   └── Routing/                   # API 路由
│   │       ├── ApiResponse.cs
│   │       └── ApiRoutes.cs
│   ├── UI/                            # 用户界面层
│   │   ├── Controls/                  # 自定义控件
│   │   │   └── AuthTreeView.cs        # 权限树视图
│   │   ├── AuthTreeForm.cs            # 权限树表单
│   │   ├── FloatingButton.cs          # 悬浮按钮
│   │   ├── FloatingButtonManager.cs   # 悬浮按钮管理器
│   │   ├── LogForm.cs                 # 日志窗口
│   │   ├── LoginForm.cs               # 登录表单
│   │   ├── NotificationForm.cs        # 通知表单
│   │   └── TrayIcon.cs                # 系统托盘图标
│   ├── Utils/                         # 工具类
│   │   ├── CryptoUtils.cs             # 加密工具
│   │   ├── DpiHelper.cs               # DPI 辅助工具
│   │   ├── GlobalState.cs             # 全局状态管理
│   │   ├── Logger.cs                  # 日志记录器
│   │   └── StorageManager.cs          # 存储管理器
│   ├── Program.cs                     # 应用入口
│   ├── PasswordManager.csproj         # 项目配置
│   └── doc/                           # 项目文档
│       └── 接口文档.md                # API 接口文档
├── .github/workflows/                 # GitHub Actions
│   ├── auto-clean.yml                 # 自动清理工作流
│   └── build.yml                      # 构建工作流
└── README.md                          # 项目说明文档
```

## 安全特性

- **零外部存储**：密码仅存储在文档元数据中，不依赖外部数据库
- **权限控制**：根据用户权限动态显示功能按钮
- **加密传输**：与服务器通信采用 HTTPS 加密
- **日志审计**：完整的操作日志记录

## 性能指标

| 指标 | 数值 |
|------|------|
| CPU 占用 | < 1% |
| 内存占用 | < 20MB |
| 对话框识别延迟 | < 100ms |
| 自动填充响应时间 | < 300ms |

## API 接口

| 接口路径 | 方法 | 说明 |
|---------|------|------|
| `/account/login` | POST | 用户登录 |
| `/account/logout` | POST | 用户登出 |
| `/account/refresh-token` | POST | 刷新令牌 |
| `/doc/owner` | GET | 获取文档所有者 |
| `/doc/password` | GET | 获取文档密码 |
| `/doc/auth/tree` | GET | 获取权限树 |
| `/doc/auth/update` | PUT | 更新权限 |
| `/doc/save/log` | POST | 上报保存记录 |
| `/config/ldap` | GET | 获取 LDAP 配置 |
| `/config/encrypt` | POST | 公钥加密 |

详细接口规范请参考 [接口文档](PasswordManager/doc/接口文档.md)。

## 常见问题

### Q: 插件无法识别 WPS 密码对话框？
- 确认 WPS 版本为支持的版本（2019/2021/12.1.0.25225）
- 确保以管理员权限运行
- 检查日志文件确认具体错误信息

### Q: 悬浮按钮位置偏移？
- 检查系统 DPI 缩放设置（推荐 100%/125%/150%）
- 确保 WPS 窗口未处于最小化状态

### Q: 无法读取加密文档密码？
- 文档必须由本插件加密（包含元数据）
- 文档未被其他程序占用
- 元数据未被第三方软件修改

### Q: 插件启动后托盘图标不显示？
- 尝试重启插件程序
- 检查是否被安全软件拦截
- 确认安装路径无特殊字符

## 开发指南

### 环境配置
1. 安装 .NET 6.0 SDK
2. 安装 Visual Studio 2022 或 VSCode + C# 扩展
3. 克隆项目：`git clone <repository_url>`
4. 还原依赖：`dotnet restore`
5. 编译项目：`dotnet build`

### 提交规范
- **格式**：`<type>(<scope>): <subject>`
- **type**：`feat` / `fix` / `docs` / `style` / `refactor` / `perf` / `test` / `chore`




## 许可证

MIT License


