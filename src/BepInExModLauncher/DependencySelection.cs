using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInExModLauncher;

public static class DependencySelection
{
    // Resolve in memory first. A failed selection never leaves half-selected dependencies.
    public static List<ModFile> Set(Installation installation, ModFile requested, bool enabled, string gameExe)
    {
        var before = installation.Files.ToDictionary(f => f, f => f.Selected);
        var planned = new Dictionary<ModFile, bool>(before);
        if (!planned.ContainsKey(requested)) throw new IOException("模组不属于当前扫描结果。");
        planned[requested] = enabled;
        if (enabled)
        {
            var visited = new HashSet<ModFile>();
            Action<ModFile> include = null;
            include = file => {
                if (!visited.Add(file)) return;
                planned[file] = true;
                foreach (var mod in file.Plugins)
                    foreach (var dependency in mod.Dependencies.Where(d => d.Required))
                    {
                        var candidates = installation.Files.SelectMany(f => f.Plugins.Where(p => p.Guid == dependency.Guid).Select(p => new { File = f, Plugin = p })).ToList();
                        if (candidates.Count == 0) throw new IOException(mod.Name + " 缺少必需依赖：" + dependency.Guid + "，请先安装。");
                        if (candidates.Count != 1) throw new IOException("依赖 GUID 有多个候选，请先处理重复插件：" + dependency.Guid);
                        var candidate = candidates[0];
                        if (dependency.Minimum != null && (!Version.TryParse(candidate.Plugin.Version, out var actual) ||
                            !Version.TryParse(dependency.Minimum, out var minimum) || actual < minimum))
                            throw new IOException(mod.Name + " 需要 " + candidate.Plugin.Name + " >= " + dependency.Minimum + "，当前版本 " + candidate.Plugin.Version);
                        include(candidate.File);
                    }
            };
            include(requested);
        }
        else
        {
            // Disable dependents transitively; optional dependencies do not propagate.
            bool changed;
            do
            {
                changed = false;
                var removed = new HashSet<string>(installation.Files.Where(f => !planned[f]).SelectMany(f => f.Plugins).Select(p => p.Guid));
                foreach (var file in installation.Files.Where(f => planned[f]))
                    if (file.Plugins.SelectMany(p => p.Dependencies).Any(d => d.Required && removed.Contains(d.Guid)))
                    { planned[file] = false; changed = true; }
            } while (changed);
        }
        try
        {
            foreach (var file in installation.Files) file.Selected = planned[file];
            if (enabled)
            {
                var errors = installation.Validate(gameExe);
                if (errors.Count > 0) throw new IOException(string.Join(Environment.NewLine, errors));
            }
            return installation.Files.Where(f => f.Selected != before[f]).ToList();
        }
        catch
        {
            foreach (var file in installation.Files) file.Selected = before[file];
            throw;
        }
    }

    public static string Describe(Installation installation, Dependency dependency)
    {
        var matches = installation.Files.SelectMany(f => f.Plugins.Where(p => p.Guid == dependency.Guid).Select(p => new { File = f, Plugin = p })).ToList();
        var kind = dependency.Required ? "必需" : "可选";
        var minimum = dependency.Minimum == null ? "" : " >= " + dependency.Minimum;
        if (matches.Count == 0) return kind + "：" + dependency.Guid + minimum + "（未安装）";
        if (matches.Count > 1) return kind + "：" + dependency.Guid + minimum + "（重复 GUID）";
        var match = matches[0];
        return kind + "：" + match.Plugin.Name + minimum + "（" + match.Plugin.Version + "，" + (match.File.Selected ? "已选择" : "未选择") + "）";
    }
}
