# HiddenType for Windows

Android 版“隐藏码字”的轻量 Windows 客户端。它保留相同的笔记文件格式和核心隐私交互，但使用适合桌面的大纲式双栏布局。

## 功能

- 输入时自动将旧文字降为低对比度，并叠加半透明底膜与细密斜线，仅保留最后 1–50 个非空白字符
- 一键显示全文；窗口失去焦点后自动恢复遮罩
- 多笔记、新建、重命名、删除和 UTF-8 TXT 导出
- 500 ms 防抖自动保存和原子文件替换
- 深色/浅色主题、常用键盘快捷键
- 默认完全本地运行，不申请网络权限

> 文字遮罩用于减少旁人直接看到内容的机会，不等同于文件加密。

## 构建与运行

无需安装 SDK。Windows 10/11 自带的 .NET Framework 4.8 即可构建，在仓库根目录执行：

```powershell
.\windows\build.ps1
.\windows\dist\Release\HiddenType.exe
```

默认数据位于 `%LOCALAPPDATA%\HiddenType`。如需把数据保存在程序目录旁，可使用：

```powershell
.\windows\dist\Release\HiddenType.exe --portable
```

## 测试与发布

```powershell
.\windows\test.ps1
.\windows\build.ps1 -Configuration Release
```

发布结果位于 `windows/dist/Release/HiddenType.exe`，这是无需安装、可直接分发的单文件程序。

## 快捷键

- `Ctrl+N`：新建笔记
- `Ctrl+S`：立即保存
- `Ctrl+Shift+E`：导出 TXT
- `F2`：重命名
- `F10`：显示全文/恢复遮罩
