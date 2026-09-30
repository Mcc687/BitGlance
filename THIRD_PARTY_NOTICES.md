# 来源、依赖与修改说明

## PayDance

- 上游：[MrBaoboer/PayDance](https://github.com/MrBaoboer/PayDance)
- 参考提交：[`27ad42dc159e99a2a3545c47f5a47371e5e012f3`](https://github.com/MrBaoboer/PayDance/tree/27ad42dc159e99a2a3545c47f5a47371e5e012f3)
- 参考源文件：[`src/lib/window-mode.ts`](https://github.com/MrBaoboer/PayDance/blob/27ad42dc159e99a2a3545c47f5a47371e5e012f3/src/lib/window-mode.ts)
- 对应移植：[source/WindowPlacement.cs](source/WindowPlacement.cs)
- 版权：Copyright (C) 2026 Mr.Baoboer.
- 许可：[GNU Affero General Public License v3.0 only](LICENSE)，保留 [附加条款](legal/ADDITIONAL_TERMS.md)。

修改日期：2026-09-30。将窗口位置有效性检查、可见工作区域选择和边界限制移植为 C#；桌面界面、公开行情接入、K 线和迷你窗手势由 BitGlance 实现。本项目非 PayDance 官方版本，不代表上游制作、认可或背书。

Based on PayDance. Copyright (C) 2026 Mr.Baoboer.
Licensed under the GNU Affero General Public License v3.0 only.

`legal/` 内保留上游相应提交的法律文件，包括其原始第三方声明，以保持交叉引用完整。该目录中描述的 Vue、Tauri、Rust 与 Noto 字体是 **PayDance 上游的组件**；BitGlance 不捆绑这些组件。

## BitGlance 使用的系统组件与素材

- 运行与编译依赖 Windows 上的 .NET Framework 4.8 / WPF；发布包不捆绑 .NET 安装程序。
- 字体按名称引用系统中的 Segoe UI 与 Microsoft YaHei UI，不分发字体文件。
- 应用图标由 [source/make-icon.ps1](source/make-icon.ps1) 绘制。没有使用 PayDance 官方图标或海报。
- `docs/images/` 为 BitGlance 实际 WPF 界面的渲染图，行情数字仅代表捕获时刻，不是实时行情。
- 公开行情来源为 [Bitget 合约市场 API](https://www.bitget.com/docs/catalog/classic-contract-market/classic-contract-market)。应用不内置 API 密钥。
