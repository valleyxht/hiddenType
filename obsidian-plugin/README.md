# HiddenType for Obsidian

HiddenType 是一个面向桌面版 Obsidian 的隐私写作插件。输入时，较早的文字会以低对比度、轻微模糊并带有淡色半透明底膜与细密斜线的方式显示，只保留最后几个字符清晰可见。近处写作者仍能辨认上下文，远处则难以直接阅读。

## 功能

- 自动遮住旧文字，可设置保留 1–50 个非空白字符（空格、Tab 和换行不计数）
- 一键显示全文或恢复遮罩
- Obsidian 失去焦点、切换笔记或进入后台时自动恢复遮罩
- 命令面板、左侧 Ribbon 按钮和状态栏入口
- 不修改 Markdown 内容，不联网，不收集数据

> HiddenType 只提供屏幕视觉遮挡，不会加密笔记。复制、搜索、其他插件以及磁盘上的 Markdown 文件仍可访问原文。

## 安装

普通用户不需要安装 Node.js，也不需要运行任何命令。

1. 从 GitHub Releases 下载 `HiddenType-Obsidian-版本号.zip`。
2. 解压 ZIP，把里面完整的 `hidden-type` 文件夹放入 Vault 的 `.obsidian/plugins/`。
3. 完全退出并重新打开 Obsidian。
4. 点击左下角齿轮，进入“设置 → 第三方插件”。如果尚未开启第三方插件，先按页面提示开启。
5. 在“已安装插件”中找到 **HiddenType**，打开右侧开关。

安装完成后的目录应当是：

```text
你的 Vault/.obsidian/plugins/hidden-type/
├── main.js
├── manifest.json
└── styles.css
```

请确认 `main.js` 直接位于 `hidden-type` 文件夹内，不要多套一层同名文件夹。

启用后，可以在“设置 → HiddenType”中调整保留的可见字符数量。左侧眼睛按钮可以临时显示全文，再点一次恢复遮罩。

## 命令

- 切换隐私写作模式
- 显示全文或恢复遮罩
- 增加可见字符
- 减少可见字符

## 从源码构建

以下内容仅供开发者使用：

```powershell
cd obsidian-plugin
npm install
npm run build
npm run package
```
