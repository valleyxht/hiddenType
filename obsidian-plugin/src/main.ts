import { MarkdownView, Notice, Plugin, setIcon } from "obsidian";
import { EditorView } from "@codemirror/view";
import { createMaskExtension, MaskRuntime, setMaskRuntime } from "./editor-extension";
import { DEFAULT_SETTINGS, HiddenTypeSettings, HiddenTypeSettingTab } from "./settings";

export default class HiddenTypePlugin extends Plugin {
  settings: HiddenTypeSettings = DEFAULT_SETTINGS;
  private revealed = false;
  private statusBarItem: HTMLElement | null = null;
  private ribbonItem: HTMLElement | null = null;

  async onload(): Promise<void> {
    await this.loadSettings();
    this.registerEditorExtension(createMaskExtension(() => this.runtime));
    this.addSettingTab(new HiddenTypeSettingTab(this.app, this));

    this.ribbonItem = this.addRibbonIcon("eye-off", "HiddenType：显示全文", () => {
      this.toggleReveal();
    });

    this.statusBarItem = this.addStatusBarItem();
    this.statusBarItem.addClass("hidden-type-status");
    this.statusBarItem.addEventListener("click", () => this.toggleReveal());
    this.statusBarItem.setAttribute("aria-label", "切换 HiddenType 全文显示");

    this.addCommand({
      id: "toggle-privacy-mode",
      name: "切换隐私写作模式",
      callback: async () => {
        this.settings.enabled = !this.settings.enabled;
        this.revealed = false;
        await this.savePluginSettings();
        new Notice(this.settings.enabled ? "HiddenType 已启用" : "HiddenType 已关闭");
      },
    });

    this.addCommand({
      id: "toggle-reveal",
      name: "显示全文或恢复遮罩",
      checkCallback: (checking) => {
        if (!this.settings.enabled) return false;
        if (!checking) this.toggleReveal();
        return true;
      },
    });

    this.addCommand({
      id: "increase-visible-characters",
      name: "增加可见字符",
      callback: async () => this.changeRevealCount(1),
    });

    this.addCommand({
      id: "decrease-visible-characters",
      name: "减少可见字符",
      callback: async () => this.changeRevealCount(-1),
    });

    this.registerDomEvent(window, "blur", () => this.restoreMaskAfterBlur());
    this.registerDomEvent(document, "visibilitychange", () => {
      if (document.hidden) this.restoreMaskAfterBlur();
    });
    this.registerEvent(this.app.workspace.on("active-leaf-change", () => this.restoreMaskAfterBlur()));

    this.updateUi();
  }

  async savePluginSettings(): Promise<void> {
    this.settings.revealCount = Math.max(1, Math.min(50, this.settings.revealCount));
    if (!this.settings.enabled) this.revealed = false;
    await this.saveData(this.settings);
    this.broadcastRuntime();
    this.updateUi();
  }

  private async loadSettings(): Promise<void> {
    const stored = (await this.loadData()) as Partial<HiddenTypeSettings> | null;
    this.settings = Object.assign({}, DEFAULT_SETTINGS, stored ?? {});
    this.settings.revealCount = Math.max(1, Math.min(50, this.settings.revealCount));
  }

  private get runtime(): MaskRuntime {
    return {
      enabled: this.settings.enabled,
      revealed: this.revealed,
      revealCount: this.settings.revealCount,
    };
  }

  private toggleReveal(): void {
    if (!this.settings.enabled) {
      new Notice("请先启用 HiddenType 隐私写作模式");
      return;
    }
    this.revealed = !this.revealed;
    this.broadcastRuntime();
    this.updateUi();
  }

  private restoreMaskAfterBlur(): void {
    if (!this.settings.autoMaskOnBlur || !this.revealed) return;
    this.revealed = false;
    this.broadcastRuntime();
    this.updateUi();
  }

  private async changeRevealCount(delta: number): Promise<void> {
    this.settings.revealCount = Math.max(1, Math.min(50, this.settings.revealCount + delta));
    await this.savePluginSettings();
    new Notice(`保留 ${this.settings.revealCount} 个可见字符`);
  }

  private broadcastRuntime(): void {
    const runtime = this.runtime;
    this.app.workspace.iterateAllLeaves((leaf) => {
      if (!(leaf.view instanceof MarkdownView)) return;
      // Obsidian exposes CodeMirror through MarkdownView at runtime but not in its public typings.
      const editorView = (leaf.view.editor as unknown as { cm?: EditorView }).cm;
      editorView?.dispatch({ effects: setMaskRuntime.of(runtime) });
    });
  }

  private updateUi(): void {
    if (this.statusBarItem) {
      this.statusBarItem.setText(
        this.settings.enabled
          ? `HiddenType · ${this.revealed ? "全文" : `${this.settings.revealCount} 字`}`
          : "HiddenType · 关闭",
      );
      this.statusBarItem.toggleClass("is-revealed", this.revealed);
      this.statusBarItem.toggleClass("is-disabled", !this.settings.enabled);
    }

    if (this.ribbonItem) {
      setIcon(this.ribbonItem, this.revealed ? "eye" : "eye-off");
      this.ribbonItem.setAttribute("aria-label", this.revealed ? "HiddenType：恢复遮罩" : "HiddenType：显示全文");
    }
  }
}
