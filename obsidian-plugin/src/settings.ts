import { App, PluginSettingTab, Setting } from "obsidian";
import HiddenTypePlugin from "./main";

export interface HiddenTypeSettings {
  enabled: boolean;
  revealCount: number;
  autoMaskOnBlur: boolean;
}

export const DEFAULT_SETTINGS: HiddenTypeSettings = {
  enabled: true,
  revealCount: 5,
  autoMaskOnBlur: true,
};

export class HiddenTypeSettingTab extends PluginSettingTab {
  constructor(app: App, private readonly plugin: HiddenTypePlugin) {
    super(app, plugin);
  }

  display(): void {
    const { containerEl } = this;
    containerEl.empty();

    new Setting(containerEl)
      .setName("启用隐私写作模式")
      .setDesc("在编辑器中遮住较早的文字，仅显示末尾字符。")
      .addToggle((toggle) =>
        toggle.setValue(this.plugin.settings.enabled).onChange(async (value) => {
          this.plugin.settings.enabled = value;
          await this.plugin.savePluginSettings();
        }),
      );

    new Setting(containerEl)
      .setName("保留可见字符")
      .setDesc("设置始终可见的末尾文字数量；空格、Tab 和换行不计数。")
      .addSlider((slider) =>
        slider
          .setLimits(1, 50, 1)
          .setDynamicTooltip()
          .setValue(this.plugin.settings.revealCount)
          .onChange(async (value) => {
            this.plugin.settings.revealCount = value;
            await this.plugin.savePluginSettings();
          }),
      );

    new Setting(containerEl)
      .setName("失去焦点时恢复遮罩")
      .setDesc("切换到其他窗口或应用后，自动退出临时全文显示。")
      .addToggle((toggle) =>
        toggle.setValue(this.plugin.settings.autoMaskOnBlur).onChange(async (value) => {
          this.plugin.settings.autoMaskOnBlur = value;
          await this.plugin.savePluginSettings();
        }),
      );
  }
}
