# 上传 GitHub 与发布下载包

## 已准备的材料

| 材料 | 用途 |
| --- | --- |
| 当前仓库源码 / `BitGlance-GitHub-source.zip` | 上传仓库 Code，包含 README、源码、文档、预览图、许可证、Actions 和 Issue 模板 |
| `BitGlance-v1.2.2-Windows.zip` | 上传 GitHub Release，供用户直接运行，附完整对应源码与法律文件 |
| `BitGlance-v1.2.2-Windows.zip.sha256` | 与运行包一起上传 Release，供校验下载 |
| [v1.2.2 发布文案](releases/v1.2.2.md) | 可直接粘贴到 Release 描述 |

## 新建仓库

建议仓库名：`BitGlance`。

建议 Description：

> Windows 桌面 BTC / ETH / SOL USDT 永续价格悬浮窗，支持透明背景、折线和 K 线。

建议 Topics：`bitcoin`、`ethereum`、`solana`、`crypto`、`windows`、`wpf`、`desktop-widget`、`usdt`。

在 GitHub 新建空仓库。已有本地 README、`.gitignore` 和 LICENSE，创建时无需让 GitHub 自动生成这些文件。保留项目现有 AGPL-3.0-only 与上游声明。

## 上传方式一：使用 Git 推送

在本地 BitGlance 仓库目录打开 PowerShell，将下面地址替换成你刚创建的仓库地址：

```powershell
$repoUrl = 'https://github.com/你的用户名/BitGlance.git'
git remote add origin $repoUrl
git push -u origin main
git push origin v1.2.2
```

准备好的 GitHub 源码目录是独立仓库，`main` 与 `v1.2.2` 指向整理后的源码。原来的本地开发记录留在原开发目录中。

如果已经配置过 `origin`，先用 `git remote -v` 查看，使用现有正确地址即可，不必重复添加。

## 上传方式二：网页上传当前源码

解压 `BitGlance-GitHub-source.zip`，进入其中的 `BitGlance` 目录，将其内容上传到仓库根目录，包括 `.github`、`.gitignore` 和 `.gitattributes`。不要只把源码 ZIP 文件上传到 Code，也不要把包含当前运行程序和临时文件的工作目录整个拖入网页。

这种方式只上传当前源码，不带本地 Git 历史。提交后在 Release 中从 `main` 新建 `v1.2.2` 标签。

## 发布可运行程序

1. 确认仓库 Code 首页显示 README，Actions 中 `Windows build` 成功。工作流只构建并保存产物，不自动发布。
2. 打开 Releases，创建发布，选择 `v1.2.2` 标签，标题填写 `BitGlance 1.2.2`。
3. 粘贴 [发布文案](releases/v1.2.2.md)。
4. 上传 `BitGlance-v1.2.2-Windows.zip` 与同名 `.sha256` 文件。
5. 发布后，从 Releases 下载 ZIP 并试运行。GitHub 自动生成的 Source code 下载只有源码，没有预编译 EXE。

上传前已检查源码目录与生成包中的本机路径、配置和常见凭据形式；未发现个人配置或硬编码凭据。仍应以实际选中的上传文件为准。

## 后续版本

更新版本号与 CHANGELOG，运行 `source/package.ps1`，确认测试报告，再创建相应的版本标签与 Release。图表或鼠标交互修改需按 [开发指引](../CONTRIBUTING.md) 增加桌面验证。

参考：[GitHub 官方本地仓库上传指引](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github)。
