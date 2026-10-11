# BepInEx 模组启动器 Demo

Windows 游戏外启动器。当前验证环境为《药剂工艺》与 BepInEx 5.4.23.2。
插件识别逻辑不依赖 Potion Craft 的模组名单；其他使用 BepInEx 5 的 Windows 游戏可手动选择主程序尝试。
本 Demo 尚未验证 BepInEx 6 / IL2CPP 或非 Windows 平台。

## 使用

双击 `BepInExModLauncher.exe`。同目录的 `Mono.Cecil.dll` 必须保留。
使用 Windows 的 .NET Framework 4.8，不需要安装 .NET 10。

1. 启动器优先读取 `PotionCraftPath` 环境变量（进程、用户、系统），随后尝试上次选择和启动器所在目录。命令行指定游戏 EXE 时使用指定值。找不到时点击“选择游戏”，选择游戏主 EXE（不要选择 UnityCrashHandler）。
2. 选择需要启用的插件。详情中可查看 GUID、声明依赖、冲突、适用进程、文件指纹和配置位置。
   勾选插件时自动递归勾选已安装的必需依赖；取消依赖时级联取消依赖它的插件，可选依赖不会自动勾选。
   缺失依赖、版本不足、重复 GUID、循环或冲突会阻止本次勾选并保留原选择，不会自动下载插件。
3. 点击“应用并启动游戏”。只应用选择时点击“应用选择”。游戏运行期间不能应用选择或保存配置。

本地标签和备注可以自由填写、清空，不代表作者身份或框架定义的模组分类。旧版标注保留。
模组列表只允许改变启用复选框，名称、版本、路径等信息只读；不支持表格排序、行列调整、复制或增删行。
配置编辑仍在单独的编辑窗口中进行。

选择会通过实际 DLL 状态保留。本地标签、备注、上次游戏路径与首次观察的 SHA-256 保存在
`%LOCALAPPDATA%/BepInExModLauncher/profile.json`。文件指纹变化只表示内容变化，不自动推断作者或修改来源。
保存标签／备注会同时确认当前指纹作为新基线。

## 发现与启停

- 递归读取 `BepInEx/plugins` 内 `.dll` 和 `.dll.disabled` 的元数据，不执行模组代码。
- 使用 `BepInPlugin` 识别插件，读取依赖、最低版本、声明冲突和进程限制。辅助库、PDB、ZIP 和资源文件不列为插件。
- 同一个 DLL 中的多个插件合并为一个启停条目，详情分别列出插件信息。
- 禁用：`Example.dll` → `Example.dll.disabled`；启用：恢复原名。所有资源、辅助库和配置保留在原位置。
- 必需依赖缺失、版本不足、声明冲突、重复 GUID 或必需依赖循环时，阻止应用。可选依赖不会要求一起启用。
- 应用前检查 DLL 指纹及目标路径冲突；记录操作日志，失败时尝试回滚。进程中断后可点击“恢复未完成操作”。
- `BepInEx/patchers` 的预加载补丁不纳入本 Demo 的启停列表。

这控制的是 BepInEx 的插件发现。模组自行用文件名加载其他 DLL、自定义加载器或未声明的依赖无法仅凭标准元数据完全判断。
辅助库不随插件禁用；没有元数据的依赖只能人工检查。不要同时运行多个启动器或在应用期间更新模组。

## 配置

扫描 `BepInEx/config` 中现有 `.cfg`，优先通过文件头的 `Plugin GUID` 关联，否则匹配 `<GUID>.cfg`。
配置编辑器目前采用原文编辑，保留注释、分组、未知键、编码和换行；保存时在配置旁创建时间戳备份，检测外部修改以避免覆盖。
没有已有配置时不会执行插件来生成配置，也不会猜测配置定义。
非标准 JSON/TXT、动态配置路径、README/图标/作者信息的自动关联暂未实现。

## 示例场景

发现与启停根据实际扫描结果工作，不保存开发者本机的安装路径、模组清单或扫描结果到源码。

| 场景 | 处理方式 |
| --- | --- |
| 自己编写的模组 | 与第三方模组使用相同的元数据发现逻辑 |
| 第三方模组 | 读取插件声明，区分辅助库与插件 |
| 修改过的第三方模组 | 读取当前 DLL 的元数据，支持人工来源备注与文件指纹变化提示 |
| 共享目录、多 DLL、中文文件名 | 递归扫描，按 DLL 分组，通过 GUID 关联配置 |

本地修改的第三方模组使用实际安装 DLL 的元数据和配置；标签由使用者自由填写。
不会将“版本号相同”误认为“内容与原版相同”，也不会把来源标签当作已验证的作者信息。

## 构建

```powershell
# 已配置 PotionCraftPath 环境变量时，无需在源码中设置游戏目录。
dotnet build src/BepInExModLauncher/BepInExModLauncher.csproj -c Release -o artifacts/mod-launcher-demo

# 也可以按次传入 MSBuild 属性。
dotnet build src/BepInExModLauncher/BepInExModLauncher.csproj -c Release -o artifacts/mod-launcher-demo "-p:PotionCraftPath=$env:PotionCraftPath"
```

构建引用游戏随 BepInEx 提供的 `Mono.Cecil.dll` 并复制到输出目录。
启动器本身不引用游戏程序集或 BepInEx 运行时。项目单独构建，避免现有游戏插件构建流程部署它。
Mono.Cecil 使用 MIT 许可证，打包时保留随包提供的第三方许可说明。

## 验证

无界面扫描（只读游戏目录，写出报告）：

```powershell
Start-Process artifacts/mod-launcher-demo/BepInExModLauncher.exe -ArgumentList ('--scan "{0}" "artifacts/mod-launcher-demo/scan-report.tsv"' -f $env:PotionCraftPath) -WindowStyle Hidden -Wait
```

复制真实 DLL / CFG 到新的测试目录后验证启停、依赖阻止、冲突保护、事务恢复、配置备份、外部修改检测、多插件共 DLL、依赖循环和来源标注：

```powershell
Start-Process artifacts/mod-launcher-demo/BepInExModLauncher.exe -ArgumentList ('--self-test "{0}" "artifacts/launcher-fixture-new"' -f $env:PotionCraftPath) -WindowStyle Hidden -Wait
```

测试目标目录必须尚不存在，不能位于真实游戏目录内。结果写入测试目录的 `test-results.txt`。
命令失败会返回非零退出码，并在当前工作目录写入 `launcher-error.txt`。
验证没有改动真实游戏安装；实际启动游戏与游戏内功能仍需使用者体验验证。

构建产物、测试副本、扫描报告和预览均放在 Git 忽略的 `artifacts/` 中。
本机配置写入用户目录；不要将其复制进源码或提交到 Git。
