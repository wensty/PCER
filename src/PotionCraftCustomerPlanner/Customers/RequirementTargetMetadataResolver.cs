using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PotionCraft.QuestSystem;

namespace PotionCraftCustomerPlanner;

internal static class RequirementTargetMetadataResolver
{
    private static readonly Dictionary<string, string> DisplayNameCache =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly HashSet<string> NegativeCache =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string[]> TagsCache =
        new Dictionary<string, string[]>(StringComparer.Ordinal);
    private static readonly HashSet<string> TagsNegativeCache =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RequirementTagsMetadata> RequirementTagsCache =
        new Dictionary<string, RequirementTagsMetadata>(StringComparer.Ordinal);
    private static readonly HashSet<string> RequirementTagsNegativeCache =
        new HashSet<string>(StringComparer.Ordinal);

    public static bool TryGetDeclaredTarget(
        QuestRequirement requirement,
        out string displayName)
    {
        displayName = null;
        if (requirement == null)
            return false;

        string key = requirement.name ?? requirement.GetType().FullName;
        if (key != null)
        {
            if (DisplayNameCache.TryGetValue(key, out displayName))
                return true;
            if (NegativeCache.Contains(key))
                return false;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryGetDeclaredTargetFromAssembly(assembly, requirement, out displayName))
            {
                if (key != null)
                    DisplayNameCache[key] = displayName;
                return true;
            }
        }

        if (key != null)
            NegativeCache.Add(key);
        return false;
    }

    public static bool TryGetTags(
        QuestRequirement requirement,
        out string[] tags)
    {
        tags = null;
        if (requirement == null)
            return false;

        string key = requirement.name ?? requirement.GetType().FullName;
        if (key != null)
        {
            if (TagsCache.TryGetValue(key, out tags))
                return true;
            if (TagsNegativeCache.Contains(key))
                return false;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryGetDefinitionFromAssembly(assembly, requirement, out object definition))
            {
                tags = DefinitionTags(definition);
                if (tags.Length > 0)
                {
                    if (key != null)
                        TagsCache[key] = tags;
                    return true;
                }
            }
        }

        if (key != null)
            TagsNegativeCache.Add(key);
        return false;
    }

    public static bool TryGetRequirementTags(
        QuestRequirement requirement,
        out RequirementTagsMetadata metadata)
    {
        metadata = default;
        if (requirement == null)
            return false;

        string key = requirement.name ?? requirement.GetType().FullName;
        if (key != null)
        {
            if (RequirementTagsCache.TryGetValue(key, out metadata))
                return true;
            if (RequirementTagsNegativeCache.Contains(key))
                return false;
        }

        if (TryGetExternalRequirementTags(requirement, out metadata))
        {
            if (key != null)
                RequirementTagsCache[key] = metadata;
            return true;
        }

        if (key != null)
            RequirementTagsNegativeCache.Add(key);
        return false;
    }

    private static bool TryGetExternalRequirementTags(
        QuestRequirement requirement,
        out RequirementTagsMetadata metadata)
    {
        metadata = default;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryGetDefinitionFromAssembly(assembly, requirement, out object definition))
            {
                metadata = new RequirementTagsMetadata(
                    DefinitionStringCollection(definition, "Tags"),
                    DefinitionStringCollection(definition, "ConflictingTags"));
                if (metadata.Tags.Length > 0 || metadata.ConflictingTags.Length > 0)
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetDeclaredTargetFromAssembly(
        Assembly assembly,
        QuestRequirement requirement,
        out string displayName)
    {
        displayName = null;
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type != null).ToArray();
        }

        foreach (Type type in types)
        {
            foreach (MethodInfo tryGet in FindTryGetMethods(type))
            {
                try
                {
                    if (!TryInvokeCatalog(tryGet, requirement, out object definition))
                        continue;

                    object declaredTarget = definition.GetType()
                        .GetProperty("DeclaredTarget", BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(definition, null);
                    if (declaredTarget == null)
                        continue;

                    displayName = TargetDisplayName(declaredTarget);
                    if (!string.IsNullOrWhiteSpace(displayName))
                        return true;
                }
                catch
                {
                    // Metadata integration is optional. Ignore incompatible catalogs.
                }
            }
        }

        return false;
    }
    private static bool TryGetDefinitionFromAssembly(
        Assembly assembly,
        QuestRequirement requirement,
        out object definition)
    {
        definition = null;
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type != null).ToArray();
        }

        foreach (Type type in types)
        {
            foreach (MethodInfo tryGet in FindTryGetMethods(type))
            {
                if (TryInvokeCatalog(tryGet, requirement, out definition))
                    return true;
            }
        }

        return false;
    }

    private static bool TryInvokeCatalog(
        MethodInfo tryGet,
        QuestRequirement requirement,
        out object definition)
    {
        definition = null;
        try
        {
            object[] args = { requirement, null };
            object success = tryGet.Invoke(null, args);
            if (!(success is bool found) || !found || args[1] == null)
                return false;
            definition = args[1];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static MethodInfo[] FindTryGetMethods(Type catalogType)
    {
        return catalogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
            {
                if (method.Name != "TryGet" || method.ReturnType != typeof(bool))
                    return false;
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType == typeof(QuestRequirement)
                    && parameters[1].IsOut
                    && HasSupportedDefinitionMetadata(parameters[1].ParameterType.GetElementType());
            })
            .ToArray();
    }

    private static bool HasSupportedDefinitionMetadata(Type definitionType)
    {
        return definitionType
            ?.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.Name == "DeclaredTarget"
                || property.Name == "Tags"
                || property.Name == "ConflictingTags") == true;
    }

    private static string TargetDisplayName(object declaredTarget)
    {
        Type type = declaredTarget.GetType();
        string displayName = type.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(declaredTarget, null) as string;
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        object ingredientCategory = type.GetProperty("IngredientCategory", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(declaredTarget, null);
        return ingredientCategory?.ToString();
    }

    private static string[] DefinitionTags(object definition)
    {
        return DefinitionStringCollection(definition, "Tags");
    }

    private static string[] DefinitionStringCollection(object definition, string propertyName)
    {
        object collectionObject = definition.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(definition, null);
        if (collectionObject is System.Collections.IEnumerable enumerable && !(collectionObject is string))
        {
            return enumerable
                .Cast<object>()
                .Select(item => item?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}

internal readonly struct RequirementTagsMetadata
{
    public string[] Tags { get; }
    public string[] ConflictingTags { get; }

    public RequirementTagsMetadata(string[] tags, string[] conflictingTags)
    {
        Tags = tags ?? Array.Empty<string>();
        ConflictingTags = conflictingTags ?? Array.Empty<string>();
    }
}
