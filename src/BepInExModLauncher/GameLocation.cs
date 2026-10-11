using System;
using System.Collections.Generic;
using System.IO;

namespace BepInExModLauncher;

internal static class GameLocation
{
    public static string Resolve(string explicitExe, string savedExe)
    {
        if (!string.IsNullOrWhiteSpace(explicitExe)) return explicitExe;
        var roots = new List<string>();
        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            roots.Add(Environment.GetEnvironmentVariable("PotionCraftPath", target));
        return ResolveCandidates(roots, savedExe, AppDomain.CurrentDomain.BaseDirectory);
    }

    internal static string ResolveCandidates(IEnumerable<string> configuredRoots, string savedExe, string launcherDirectory)
    {
        foreach (var root in configuredRoots)
        {
            var game = InDirectory(root);
            if (game != "") return game;
        }
        if (File.Exists(savedExe)) return savedExe;
        return InDirectory(launcherDirectory);
    }

    private static string InDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return "";
        try
        {
            var game = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"')), "Potion Craft.exe");
            return File.Exists(game) ? Path.GetFullPath(game) : "";
        }
        catch (ArgumentException) { return ""; }
        catch (NotSupportedException) { return ""; }
        catch (PathTooLongException) { return ""; }
    }
}
