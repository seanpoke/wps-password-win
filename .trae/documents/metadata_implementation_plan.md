# 元数据功能实现计划

## 1. 项目分析

### 1.1 当前项目结构
- **现有文件**：`WpsPasswordManager/Business/MetadataManager.cs` - 负责元数据读写管理
- **项目语言**：C#
- **目标平台**：Windows

### 1.2 安卓设计方案参考
根据通用业务技术文档，安卓版本的核心元数据功能包括：
- **FileMeta**：核心数据结构，存储文件的密码和权限信息
- **FileMetaFactory**：创建和管理FileMeta实例
- **FileMetaManager**：从文件中读取和写入元数据
- **ZipExtraFieldManager**：管理ZIP文件的额外字段存储

## 2. 实现计划

### 2.1 新增文件

| 文件名 | 功能描述 | 对应安卓组件 |
|--------|---------|------------|
| `FileMeta.cs` | 元数据核心数据结构 | FileMeta |
| `FileMetaFactory.cs` | 元数据实例管理工厂 | FileMetaFactory |
| `FileMetaManager.cs` | 元数据读写管理 | FileMetaManager |
| `ZipExtraFieldManager.cs` | ZIP额外字段管理 | ZipExtraFieldManager |

### 2.2 核心功能实现

#### 2.2.1 FileMeta 类
- 实现与安卓相同的数据结构
- 包含文件路径、UID、密码、权限等信息
- 支持待定密码列表管理

#### 2.2.2 FileMetaFactory 类
- 管理FileMeta实例的创建和生命周期
- 提供密码更新和验证功能
- 实现内存存储管理

#### 2.2.3 FileMetaManager 类
- 从文件中读取和写入元数据
- 与ZipExtraFieldManager交互
- 提供密码和UID的读写接口

#### 2.2.4 ZipExtraFieldManager 类
- 实现ZIP额外字段的读写
- 构建和解析元数据块
- 支持CRC32校验

### 2.3 技术实现要点

1. **内存存储**：使用`ConcurrentDictionary`存储FileMeta实例
2. **文件存储**：利用ZIP Extra Field存储元数据
3. **数据格式**：遵循与安卓相同的ZIP Extra Field格式
4. **线程安全**：确保多线程环境下的安全操作
5. **错误处理**：完善的异常处理和日志记录

### 2.4 实现步骤

1. **创建FileMeta.cs**：定义核心数据结构
2. **创建FileMetaFactory.cs**：实现元数据实例管理
3. **创建ZipExtraFieldManager.cs**：实现ZIP额外字段操作
4. **创建FileMetaManager.cs**：实现元数据读写逻辑
5. **添加必要的工具类**：如CRC32计算等
6. **测试验证**：确保功能正常工作

### 2.5 依赖关系

- **FileMetaFactory** → **FileMeta**
- **FileMetaManager** → **FileMetaFactory**, **ZipExtraFieldManager**
- **ZipExtraFieldManager** → 无外部依赖

### 2.6 风险评估

- **文件操作风险**：文件锁定和并发访问可能导致写入失败
- **兼容性风险**：不同ZIP工具可能对额外字段处理方式不同
- **性能风险**：频繁的文件I/O操作可能影响性能

### 2.7 解决方案

- **文件操作**：实现重试机制和不同的文件共享模式
- **兼容性**：使用标准ZIP格式，确保跨平台兼容
- **性能**：优化文件I/O操作，减少不必要的读写

## 3. 预期成果

- 完整的元数据管理功能，与安卓版本保持一致
- 独立于现有MetadataManager的实现
- 支持密码和权限信息的存储和管理
- 提供清晰的API接口供其他模块使用

## 4. 后续步骤

1. 实现核心类
2. 编写测试代码
3. 集成到现有系统
4. 性能优化和稳定性测试