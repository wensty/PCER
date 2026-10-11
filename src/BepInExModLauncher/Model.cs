using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace BepInExModLauncher;

public sealed class Dependency
{
    public string Guid;
    public string Minimum;
    public bool Required;
}

public sealed class Mod
{
    public string Guid, Name, Version;
    public readonly List<Dependency> Dependencies = new List<Dependency>();
    public readonly List<string> Conflicts = new List<string>();
    public readonly List<string> Processes = new List<string>();
    public readonly List<string> Configs = new List<string>();
}

public sealed class ModFile
{
    public string Path, Hash;
    public string AssemblyName, AssemblyVersion, Company, Description;
    public bool Enabled, Selected;
    public readonly List<Mod> Plugins = new List<Mod>();
    public string Name => string.Join(" / ", Plugins.Select(p => p.Name));
    public string Version => string.Join(" / ", Plugins.Select(p => p.Version));
}

[DataContract]
public sealed class Profile
{
    [DataMember] public string GameExe = "";
    [DataMember] public string Language = "en";
    public static Profile Read(string path)
    {
        var profile = File.Exists(path) ? Json.Read<Profile>(path) : new Profile();
        if (profile.Language != "en" && profile.Language != "zh-CN" && profile.Language != "auto") profile.Language = "en";
        return profile;
    }
}

public static class Json
{
    public static T Read<T>(string path)
    {
        using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var temporary = path + ".tmp";
        using (var stream = File.Create(temporary)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
}

public sealed class Installation
{
    public string Root { get; }
    public string PluginRoot => System.IO.Path.Combine(Root, "BepInEx", "plugins");
    public string Journal => System.IO.Path.Combine(Root, "BepInEx", "mod-launcher-transaction.json");
    public readonly List<ModFile> Files = new List<ModFile>();
    public readonly List<string> Warnings = new List<string>();
    public int LibraryCount;
    public Installation(string root) { Root = System.IO.Path.GetFullPath(root); }
    public string Key(ModFile file) => file.Path.Substring(PluginRoot.Length + 1);
    public static string Fingerprint(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    public void Scan()
    {
        Files.Clear(); Warnings.Clear(); LibraryCount = 0;
        if (!Directory.Exists(PluginRoot)) throw new IOException(L.T("BepInEx/plugins was not found: ") + PluginRoot);
        using (var resolver = new DefaultAssemblyResolver())
        {
            resolver.AddSearchDirectory(System.IO.Path.Combine(Root, "BepInEx", "core"));
            foreach (var dir in Directory.EnumerateDirectories(PluginRoot, "*", SearchOption.AllDirectories).Concat(new[] { PluginRoot }))
                resolver.AddSearchDirectory(dir);
            foreach (var physical in Directory.EnumerateFiles(PluginRoot, "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase)))
            {
                var enabled = physical.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                var logical = enabled ? physical : physical.Substring(0, physical.Length - ".disabled".Length);
                try
                {
                    using (var assembly = AssemblyDefinition.ReadAssembly(physical, new ReaderParameters { AssemblyResolver = resolver }))
                    {
                        var file = new ModFile { Path = logical, Hash = Fingerprint(physical), Enabled = enabled, Selected = enabled };
                        file.AssemblyName = assembly.Name.Name;
                        file.AssemblyVersion = assembly.Name.Version.ToString();
                        file.Company = AssemblyText(assembly, "System.Reflection.AssemblyCompanyAttribute");
                        file.Description = AssemblyText(assembly, "System.Reflection.AssemblyDescriptionAttribute");
                        foreach (var type in AllTypes(assembly.MainModule.Types))
                        {
                            var metadata = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
                            if (metadata == null) continue;
                            var mod = new Mod { Guid = Arg(metadata, 0), Name = Arg(metadata, 1), Version = Arg(metadata, 2) };
                            // Attributes on base plugin classes are inherited by BepInEx.
                            var current = type;
                            var visited = new HashSet<string>();
                            while (current != null && visited.Add(current.FullName))
                            {
                                foreach (var attr in current.CustomAttributes)
                                {
                                    switch (attr.AttributeType.FullName)
                                    {
                                        case "BepInEx.BepInDependency":
                                            var second = attr.ConstructorArguments.Count > 1 ? attr.ConstructorArguments[1].Value : 1;
                                            mod.Dependencies.Add(new Dependency { Guid = Arg(attr, 0), Minimum = second as string,
                                                Required = second is string || (Convert.ToInt32(second) & 1) != 0 });
                                            break;
                                        case "BepInEx.BepInIncompatibility": mod.Conflicts.Add(Arg(attr, 0)); break;
                                        case "BepInEx.BepInProcess": mod.Processes.Add(Arg(attr, 0)); break;
                                    }
                                }
                                if (current.BaseType == null || current.BaseType.FullName == "BepInEx.BaseUnityPlugin" || current.BaseType.FullName == "System.Object") break;
                                try { current = current.BaseType.Resolve(); }
                                catch (Exception e) { Warnings.Add(mod.Name + L.T(": Could not read inherited metadata: ") + e.Message); break; }
                            }
                            file.Plugins.Add(mod);
                        }
                        if (file.Plugins.Count == 0) LibraryCount++;
                        else Files.Add(file);
                    }
                }
                catch (Exception e) { Warnings.Add(System.IO.Path.GetFileName(physical) + "：" + e.Message); }
            }
        }
        var configRoot = System.IO.Path.Combine(Root, "BepInEx", "config");
        if (Directory.Exists(configRoot))
            foreach (var config in Directory.EnumerateFiles(configRoot, "*.cfg", SearchOption.AllDirectories))
            {
                try
                {
                    var header = File.ReadLines(config).Take(20).FirstOrDefault(l => l.StartsWith("## Plugin GUID:", StringComparison.Ordinal));
                    var guid = header == null ? System.IO.Path.GetFileNameWithoutExtension(config) : header.Substring("## Plugin GUID:".Length).Trim();
                    foreach (var mod in Files.SelectMany(f => f.Plugins).Where(m => m.Guid == guid)) mod.Configs.Add(config);
                }
                catch (Exception e) { Warnings.Add(L.T("Could not read configuration: ") + config + "：" + e.Message); }
            }
        Files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
    }
    private static string Arg(CustomAttribute attr, int index) => Convert.ToString(attr.ConstructorArguments[index].Value);
    private static string AssemblyText(AssemblyDefinition assembly, string attributeName)
    {
        var attribute = assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == attributeName);
        return attribute != null && attribute.ConstructorArguments.Count > 0 ? Arg(attribute, 0) : "";
    }
    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types) { yield return type; foreach (var nested in AllTypes(type.NestedTypes)) yield return nested; }
    }
    public List<string> Validate(string gameExe)
    {
        var errors = new List<string>();
        var selected = Files.Where(f => f.Selected).SelectMany(f => f.Plugins).ToList();
        foreach (var duplicate in selected.GroupBy(m => m.Guid).Where(g => g.Count() > 1)) errors.Add(L.T("Duplicate plugin GUID: ") + duplicate.Key);
        var process = System.IO.Path.GetFileNameWithoutExtension(gameExe);
        foreach (var mod in selected)
        {
            if (mod.Processes.Count > 0 && !mod.Processes.Any(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), process, StringComparison.OrdinalIgnoreCase)))
                errors.Add(mod.Name + L.T(" does not target this game process"));
            foreach (var dependency in mod.Dependencies.Where(d => d.Required))
            {
                var target = selected.FirstOrDefault(m => m.Guid == dependency.Guid);
                if (target == null) errors.Add(mod.Name + L.T(" is missing a required dependency: ") + dependency.Guid);
                else if (dependency.Minimum != null && (!System.Version.TryParse(target.Version, out var actual) ||
                    !System.Version.TryParse(dependency.Minimum, out var minimum) || actual < minimum))
                    errors.Add(mod.Name + L.T(" requires ") + dependency.Guid + " >= " + dependency.Minimum);
            }
            foreach (var conflict in mod.Conflicts.Where(c => selected.Any(m => m.Guid == c))) errors.Add(mod.Name + L.T(" conflicts with ") + conflict + L.T(" (incompatible)"));
        }
        // Reject hard-dependency cycles before mutating files.
        var visiting = new HashSet<string>(); var done = new HashSet<string>();
        Action<Mod> visit = null;
        visit = mod => {
            if (done.Contains(mod.Guid)) return;
            if (!visiting.Add(mod.Guid)) { errors.Add(L.T("Required dependency cycle: ") + mod.Guid); return; }
            foreach (var dep in mod.Dependencies.Where(d => d.Required))
            { var target = selected.FirstOrDefault(m => m.Guid == dep.Guid); if (target != null) visit(target); }
            visiting.Remove(mod.Guid); done.Add(mod.Guid);
        };
        foreach (var mod in selected) visit(mod);
        return errors.Distinct().ToList();
    }
    public static void EnsureStopped(string gameExe)
    {
        if (Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(gameExe)).Length > 0)
            throw new IOException(L.T("The game is running. Close it first."));
    }
    public void Apply(string gameExe)
    {
        if (!File.Exists(gameExe)) throw new IOException(L.T("Please select a valid game executable."));
        EnsureStopped(gameExe);
        if (File.Exists(Journal)) throw new IOException(L.T("An interrupted operation exists. Recover it first."));
        var errors = Validate(gameExe);
        if (errors.Count > 0) throw new IOException(string.Join(Environment.NewLine, errors));
        var moves = new List<Move>();
        foreach (var file in Files)
        {
            var source = file.Enabled ? file.Path : file.Path + ".disabled";
            if (!File.Exists(source) || Fingerprint(source) != file.Hash) throw new IOException(L.T("A file changed after scanning. Scan again: ") + source);
            if (file.Selected == file.Enabled) continue;
            var destination = file.Selected ? file.Path : file.Path + ".disabled";
            if (File.Exists(destination)) throw new IOException(L.T("The destination file already exists: ") + destination);
            moves.Add(new Move { Source = source, Destination = destination, Hash = file.Hash });
        }
        if (moves.Count == 0) return;
        Json.Write(Journal, moves);
        try
        {
            foreach (var move in moves) File.Move(move.Source, move.Destination);
            File.Delete(Journal);
            foreach (var file in Files) file.Enabled = file.Selected;
        }
        catch { Recover(gameExe); throw; }
    }
    public void Recover(string gameExe)
    {
        EnsureStopped(gameExe);
        if (!File.Exists(Journal)) return;
        var moves = Json.Read<List<Move>>(Journal);
        foreach (var move in moves)
        {
            var source = System.IO.Path.GetFullPath(move.Source);
            var destination = System.IO.Path.GetFullPath(move.Destination);
            if (!source.StartsWith(PluginRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !destination.StartsWith(PluginRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !(destination == source + ".disabled" || source == destination + ".disabled"))
                throw new IOException(L.T("The recovery journal contains an invalid path."));
            if (File.Exists(source) == File.Exists(destination)) throw new IOException(L.T("The recovery file state is ambiguous: ") + source);
            if (Fingerprint(File.Exists(source) ? source : destination) != move.Hash) throw new IOException(L.T("The recovery file fingerprint does not match: ") + source);
        }
        foreach (var move in moves.AsEnumerable().Reverse()) if (File.Exists(move.Destination)) File.Move(move.Destination, move.Source);
        File.Delete(Journal);
    }
}

[DataContract]
public sealed class Move
{
    [DataMember] public string Source;
    [DataMember] public string Destination;
    [DataMember] public string Hash;
}

public sealed class ConfigDocument
{
    public string Path { get; }
    private byte[] original;
    private Encoding encoding;
    private bool bom;
    public string Text { get; private set; }
    public ConfigDocument(string path)
    {
        Path = path; original = File.ReadAllBytes(path);
        using (var reader = new StreamReader(new MemoryStream(original), Encoding.UTF8, true)) { Text = reader.ReadToEnd(); encoding = reader.CurrentEncoding; }
        var preamble = encoding.GetPreamble(); bom = preamble.Length > 0 && original.Take(preamble.Length).SequenceEqual(preamble);
    }
    public void Save(string text)
    {
        if (!File.ReadAllBytes(Path).SequenceEqual(original)) throw new IOException(L.T("Another program changed this configuration. Reopen it before editing."));
        var bytes = (bom ? encoding.GetPreamble() : new byte[0]).Concat(encoding.GetBytes(text)).ToArray();
        var temporary = Path + ".modlauncher.tmp";
        var backup = Path + ".modlauncher." + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".bak";
        File.WriteAllBytes(temporary, bytes);
        File.Replace(temporary, Path, backup);
        original = bytes; Text = text;
    }
}
