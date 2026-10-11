using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace BepInExModLauncher;

internal static class SelfTest
{
    // Tests only a fresh fixture; never changes the supplied real installation.
    public static void Run(string sourceRoot, string fixtureRoot)
    {
        sourceRoot = Path.GetFullPath(sourceRoot); fixtureRoot = Path.GetFullPath(fixtureRoot);
        if (Directory.Exists(fixtureRoot)) throw new IOException("测试目录必须不存在，防止误改真实安装。");
        if (fixtureRoot.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || fixtureRoot == sourceRoot)
            throw new IOException("测试目录不能在真实游戏目录中。");
        var results = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); results.Add("PASS: " + message); }
        L.SetLanguage("en");
        Check(L.T("Settings") == "Settings", "English localization");
        L.SetLanguage("zh-CN");
        Check(L.T("Settings") == "设置", "Simplified Chinese localization");
        foreach (var entry in L.Chinese)
        {
            var englishFields = System.Text.RegularExpressions.Regex.Matches(entry.Key, @"\{\d+\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
            var chineseFields = System.Text.RegularExpressions.Regex.Matches(entry.Value, @"\{\d+\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
            if (!englishFields.SequenceEqual(chineseFields)) throw new Exception("Translation placeholders differ: " + entry.Key);
            L.T(entry.Key, 1, 2, 3, 4, 5);
        }
        Check(true, "All translation format placeholders are valid");
        Check(L.T("Unregistered text") == "Unregistered text", "Missing translations fall back to English");
        var chineseSystem = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        Check(new Profile().Language == "en" && L.ResolveLanguage(null, chineseSystem) == "en" && L.ResolveLanguage("en", chineseSystem) == "en", "English is the default even on a Chinese system");
        Check(L.ResolveLanguage("auto", chineseSystem) == "zh-CN", "System language is used only when explicitly selected");
        L.SetLanguage("en");
        TestSelections(Check);
        var original = new Installation(sourceRoot); original.Scan();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(sourceRoot, "BepInEx", "plugins"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase))) Copy(file);
        foreach (var configFile in Directory.EnumerateFiles(Path.Combine(sourceRoot, "BepInEx", "config"), "*.cfg", SearchOption.AllDirectories)) Copy(configFile);
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "BepInEx", "core"));
        foreach (var core in Directory.EnumerateFiles(Path.Combine(sourceRoot, "BepInEx", "core"), "*.dll")) Copy(core);
        var exe = Path.Combine(fixtureRoot, "Potion Craft.exe"); File.WriteAllText(exe, "test placeholder; never executed");
        Check(GameLocation.Resolve(exe, null) == exe, "显式游戏参数优先于环境变量");
        Check(GameLocation.ResolveCandidates(new[] { fixtureRoot }, null, null) == exe, "PotionCraftPath 候选目录可定位游戏");
        Check(GameLocation.ResolveCandidates(new[] { Path.Combine(fixtureRoot, "missing"), fixtureRoot }, null, null) == exe, "无效环境目录跳过并使用下一候选");
        Check(GameLocation.ResolveCandidates(new string[0], exe, null) == exe, "环境未配置时回退上次选择");
        Check(GameLocation.ResolveCandidates(new string[0], null, fixtureRoot) == exe, "环境未配置时回退启动器目录");
        Check(GameLocation.ResolveCandidates(new string[0], null, null) == "", "没有有效路径时等待用户选择");
        var install = new Installation(fixtureRoot); install.Scan();
        Check(install.Files.Sum(f => f.Plugins.Count) == original.Files.Sum(f => f.Plugins.Count), "真实插件复制后识别数量一致");
        Check(install.LibraryCount > 0, "辅助库不会作为插件列出");
        Check(install.Files.SelectMany(f => f.Plugins).Any(p => p.Configs.Count > 0), "标准插件配置通过 GUID 关联");
        Check(install.Validate(exe).Count == 0, "真实安装的当前选择通过依赖检查");
        var target = install.Files.First(f => f.Enabled && f.Plugins.Any(p => p.Configs.Count > 0) &&
            !install.Files.Where(other => other != f && other.Selected).SelectMany(other => other.Plugins).SelectMany(p => p.Dependencies)
                .Any(d => d.Required && f.Plugins.Any(p => p.Guid == d.Guid)));
        var targetPath = target.Path;
        var originalHash = target.Hash;
        target.Selected = false; install.Apply(exe);
        Check(File.Exists(target.Path + ".disabled") && !File.Exists(target.Path), "禁用插件 DLL");
        install.Scan(); target = install.Files.Single(f => f.Path == targetPath);
        Check(!target.Enabled && target.Hash == originalHash && target.Plugins.Any(p => p.Configs.Count > 0), "禁用插件仍可发现、指纹和配置保留");
        target.Selected = true; install.Apply(exe); Check(File.Exists(target.Path) && Installation.Fingerprint(target.Path) == originalHash, "恢复插件无字节变化");
        var dependencyOwner = install.Files.First(f => f.Plugins.Any(p => p.Dependencies.Any(d => d.Required && install.Files.Any(x => x != f && x.Plugins.Any(m => m.Guid == d.Guid)))));
        var depGuid = dependencyOwner.Plugins.SelectMany(p => p.Dependencies).First(d => d.Required && install.Files.Any(x => x != dependencyOwner && x.Plugins.Any(m => m.Guid == d.Guid))).Guid;
        var dependencyFile = install.Files.First(f => f.Plugins.Any(p => p.Guid == depGuid)); dependencyFile.Selected = false;
        Check(install.Validate(exe).Any(e => e.Contains(depGuid)), "禁用必需依赖会阻止应用");
        ExpectFailure(() => install.Apply(exe)); Check(File.Exists(dependencyFile.Path), "依赖错误不改动文件");
        DependencySelection.Set(install, dependencyOwner, true, exe);
        Check(dependencyFile.Selected && install.Validate(exe).Count == 0, "真实插件元数据支持自动补齐依赖选择");
        target.Selected = false; File.WriteAllText(target.Path + ".disabled", "collision");
        ExpectFailure(() => install.Apply(exe)); Check(File.Exists(target.Path), "目标冲突不覆盖插件"); File.Delete(target.Path + ".disabled");
        // Simulate interruption after a rename, then recover from durable journal.
        Json.Write(install.Journal, new List<Move> { new Move { Source = target.Path, Destination = target.Path + ".disabled", Hash = target.Hash } });
        File.Move(target.Path, target.Path + ".disabled"); install.Recover(exe);
        Check(File.Exists(target.Path) && !File.Exists(install.Journal), "中断事务可恢复");
        var config = target.Plugins.SelectMany(p => p.Configs).First(); var before = File.ReadAllBytes(config); var document = new ConfigDocument(config);
        document.Save(document.Text); Check(File.ReadAllBytes(config).SequenceEqual(before), "配置无修改保存保留编码、换行和注释");
        Check(Directory.EnumerateFiles(Path.GetDirectoryName(config), Path.GetFileName(config) + ".modlauncher.*.bak").Any(), "配置保存自动备份");
        File.AppendAllText(config, "\n# changed externally"); ExpectFailure(() => document.Save(document.Text));
        Check(File.ReadAllText(config).Contains("changed externally"), "配置并发修改检测不覆盖外部变更");
        // Create a real metadata fixture with two plugins in one DLL, and a cycle.
        var groupedPath = Path.Combine(install.PluginRoot, "grouped-fixture.dll");
        using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("GroupedFixture", new Version(1, 0)), "GroupedFixture", ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            foreach (var metadata in new[] { (typeof(System.Reflection.AssemblyCompanyAttribute), "Fixture Company"), (typeof(System.Reflection.AssemblyDescriptionAttribute), "Description read from the DLL") })
            {
                var attribute = new CustomAttribute(module.ImportReference(metadata.Item1.GetConstructor(new[] { typeof(string) })));
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, metadata.Item2));
                assembly.CustomAttributes.Add(attribute);
            }
            var pluginAttr = new TypeReference("BepInEx", "BepInPlugin", module, new AssemblyNameReference("BepInEx", new Version(5, 4, 23, 2)));
            var depAttr = new TypeReference("BepInEx", "BepInDependency", module, pluginAttr.Scope);
            for (var i = 0; i < 2; i++)
            {
                var type = new TypeDefinition("Fixture", "Plugin" + i, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object); module.Types.Add(type);
                var ctor = new MethodReference(".ctor", module.TypeSystem.Void, pluginAttr) { HasThis = true };
                for (var n = 0; n < 3; n++) ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
                var attr = new CustomAttribute(ctor);
                foreach (var value in new[] { "fixture." + i, "Fixture " + i, "1.0.0" }) attr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
                type.CustomAttributes.Add(attr);
                var depCtor = new MethodReference(".ctor", module.TypeSystem.Void, depAttr) { HasThis = true };
                depCtor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String)); depCtor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
                var dep = new CustomAttribute(depCtor); dep.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "fixture." + (1 - i)));
                dep.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, 1)); type.CustomAttributes.Add(dep);
            }
            assembly.Write(groupedPath);
        }
        install.Scan(); Check(install.Files.Single(f => f.Path == groupedPath).Plugins.Count == 2, "同 DLL 多插件作为一组发现");
        var groupedFile = install.Files.Single(f => f.Path == groupedPath);
        Check(groupedFile.AssemblyName == "GroupedFixture" && groupedFile.Company == "Fixture Company" && groupedFile.Description == "Description read from the DLL", "Assembly information is read directly from DLL metadata");
        Check(install.Validate(exe).Any(e => e.Contains(L.T("Required dependency cycle: "))), "必需依赖循环检测");
        var profilePath = Path.Combine(fixtureRoot, "profile-test.json"); var profile = new Profile { Language = "en" };
        Json.Write(profilePath, profile);
        Check(Profile.Read(profilePath).Language == "en", "Language preference persists locally");
        var legacyPath = Path.Combine(fixtureRoot, "legacy-profile.json");
        File.WriteAllText(legacyPath, "{\"GameExe\":\"\",\"Annotations\":[]}");
        Check(Profile.Read(legacyPath).Language == "en", "Legacy settings without a language default to English");
        File.WriteAllLines(Path.Combine(fixtureRoot, "test-results.txt"), results, Encoding.UTF8);
        void Copy(string path)
        {
            var destination = Path.Combine(fixtureRoot, path.Substring(sourceRoot.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination);
        }
    }
    private static void ExpectFailure(Action action)
    {
        try { action(); } catch (IOException) { return; }
        throw new Exception("预期操作失败，但操作成功。");
    }

    private static void TestSelections(Action<bool, string> check)
    {
        var install = new Installation(Path.GetTempPath());
        ModFile Add(string guid)
        {
            var file = new ModFile { Path = guid + ".dll" };
            file.Plugins.Add(new Mod { Guid = guid, Name = guid, Version = "1.0.0" }); install.Files.Add(file); return file;
        }
        void Require(ModFile file, string guid, bool required = true, string minimum = null) =>
            file.Plugins[0].Dependencies.Add(new Dependency { Guid = guid, Required = required, Minimum = minimum });
        var app = Add("app"); var middle = Add("middle"); var shared = Add("shared"); var optional = Add("optional"); var sibling = Add("sibling");
        Require(app, "middle"); Require(middle, "shared"); Require(app, "optional", false); Require(sibling, "shared");
        var changes = DependencySelection.Set(install, app, true, "game.exe");
        check(app.Selected && middle.Selected && shared.Selected && changes.Count == 3, "启用插件递归选择全部必需依赖");
        check(!optional.Selected, "可选依赖不自动启用");
        DependencySelection.Set(install, sibling, true, "game.exe"); DependencySelection.Set(install, app, false, "game.exe");
        check(shared.Selected && sibling.Selected && middle.Selected && !app.Selected, "取消插件保留共享依赖和其他插件");
        DependencySelection.Set(install, app, true, "game.exe"); DependencySelection.Set(install, shared, false, "game.exe");
        check(!app.Selected && !middle.Selected && !shared.Selected && !sibling.Selected, "取消依赖级联取消所有必需依赖者");
        DependencySelection.Set(install, app, true, "game.exe"); DependencySelection.Set(install, optional, true, "game.exe");
        DependencySelection.Set(install, optional, false, "game.exe"); check(app.Selected, "取消可选依赖不取消插件");
        var broken = Add("broken"); Require(broken, "missing");
        ExpectFailure(() => DependencySelection.Set(install, broken, true, "game.exe"));
        check(!broken.Selected && app.Selected, "缺失依赖拒绝选择且保留原选择");
        var tooNew = Add("too-new"); Require(tooNew, "shared", true, "2.0.0");
        ExpectFailure(() => DependencySelection.Set(install, tooNew, true, "game.exe")); check(!tooNew.Selected, "依赖版本不足拒绝选择");
        var conflict = Add("conflict"); conflict.Plugins[0].Conflicts.Add("app");
        ExpectFailure(() => DependencySelection.Set(install, conflict, true, "game.exe")); check(!conflict.Selected && app.Selected, "冲突选择回滚，不自动取消其他插件");
        var cycleA = Add("cycle-a"); var cycleB = Add("cycle-b"); Require(cycleA, "cycle-b"); Require(cycleB, "cycle-a");
        ExpectFailure(() => DependencySelection.Set(install, cycleA, true, "game.exe")); check(!cycleA.Selected && !cycleB.Selected, "循环依赖不会留下部分勾选");
        var grouped = Add("group-one"); grouped.Plugins.Add(new Mod { Guid = "group-two", Name = "group-two", Version = "1.0.0" });
        grouped.Plugins[1].Dependencies.Add(new Dependency { Guid = "optional", Required = true });
        DependencySelection.Set(install, grouped, true, "game.exe"); check(grouped.Selected && optional.Selected, "同 DLL 中全部插件的依赖一起解析");
        var duplicate = Add("shared"); var duplicateUser = Add("duplicate-user"); Require(duplicateUser, "shared");
        ExpectFailure(() => DependencySelection.Set(install, duplicateUser, true, "game.exe"));
        check(!duplicateUser.Selected && !duplicate.Selected, "重复 GUID 不自动猜测依赖候选");
        check(DependencySelection.Describe(install, broken.Plugins[0].Dependencies[0]).Contains(L.T(" (not installed)")), "缺失依赖显示安装状态");
    }
}
