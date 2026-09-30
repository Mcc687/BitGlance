# 开发与验证

使用 Windows 10 / 11、.NET Framework 4.8 和 PowerShell 5.1 或更高版本。C# 源码保持内置 Framework 编译器支持的语法，不使用 NuGet 依赖。

## 构建

```powershell
.\source\build.ps1
```

程序与配置写入 `artifacts/build/`。可通过 `-OutputDirectory` 指定其他目录；构建不会覆盖正在使用的根目录便携程序。

## 验证

发布脚本会编译并运行不依赖行情网络的逻辑检查：

```powershell
.\source\package.ps1
```

交互检查需要可交互桌面与 Bitget 网络连接，会打开使用独立设置的测试窗口：

```powershell
.\source\tests\run-desktop-checks.ps1 -WorkDirectory .\artifacts\desktop-checks -BinaryDirectory .\artifacts\build
```

它验证真实 WPF 控件、价格与图表、模拟手势、透明度像素及 Windows `WindowFromPoint` 原生命中。它不等同于实体鼠标人工测试；修改鼠标逻辑时也应手工检查连续拖动与双击。

三个币种全部周期的实时接口检查可单独运行：

```powershell
$exe = (Resolve-Path .\artifacts\build\BitGlance.exe).Path
$report = Join-Path (Get-Location) 'artifacts\live-feed.txt'
$run = Start-Process -FilePath $exe -ArgumentList @('--check-feed', ('"' + $report + '"')) -Wait -PassThru
Get-Content -LiteralPath $report
if ($run.ExitCode -ne 0) { throw 'Feed checks failed' }
```

GitHub Actions 仅执行编译、逻辑检查和打包，不依赖交互式桌面或交易所可达性。

## 提交修改

- 在 Issue 中说明版本、Windows 版本、DPI、复现步骤以及预期 / 实际行为。鼠标问题请附背景不透明度、币种及两个显示开关状态。
- 代码修改保留来源与 SPDX 声明；有关 PayDance 移植内容见 [第三方声明](THIRD_PARTY_NOTICES.md)。
- 不提交 `artifacts/`、个人配置、凭据或本机路径。构建产物作为 Release 附件发布。
- 版本更新时同步 `source/App.cs` 的程序集 / 关于版本、`source/app.manifest`、README、使用说明和 CHANGELOG。
- PR 描述写清修改行为、验证结果和未覆盖的场景。
