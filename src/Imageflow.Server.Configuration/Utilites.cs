using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Tomlyn.Model;

namespace Imageflow.Server.Configuration.Utilities;

internal partial class Utilities{
    
    internal static void ToStringRecursive(object obj, StringBuilder sb, string indent)
    {
        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties();

        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(obj);
            string valueString = value?.ToString() ?? "null";

            sb.AppendLine($"{indent}{property.Name}: {valueString}");

            if (value != null && !value.GetType().IsPrimitive)
            {
                ToStringRecursive(value, sb, indent + "  ");
            }
        }
    }


    /// <summary>
    /// Only recurses types from Imageflow namespace (and collections), everything else is ToString
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="dict"></param>
    /// <param name="parentPath"></param>
    internal static void AddToDictionaryRecursive(object? obj, Dictionary<string,string> dict, string parentPath = ""){
        
        if (obj is TomlPropertiesMetadata){
            return; // Skip that property - not that this is used with raw configs anyway
        }

        if (obj is IDictionary dictionary){
            foreach (var key in dictionary.Keys){
                AddToDictionaryRecursive(dictionary[key], dict, parentPath + $"[{key}]");
            }
            return;
        }
        if (obj is ICollection collection){
            int i = 0;
            foreach (var item in collection){
                AddToDictionaryRecursive(item, dict, parentPath + $"[{i++}].");
            }
            return;
        }
        
        Type? type = obj?.GetType();
        if (type?.Namespace?.StartsWith("Imageflow") == true){
            var parentDot = parentPath.Length > 0 ? parentPath + "." : "";

            PropertyInfo[] properties = type.GetProperties( BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public);

            foreach (PropertyInfo property in properties)
            {   
               object? value = property.GetValue(obj);
               AddToDictionaryRecursive(value, dict, parentDot + property.Name );
            }

            PropertyInfo[] fields = type.GetProperties( BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (PropertyInfo field in fields)
            {  
                // exclude names matching properties, case insensitive
                if (properties.Any(p => p.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase))) continue;

                object? value = field.GetValue(obj);
                AddToDictionaryRecursive(value, dict, parentDot + field.Name );
            }
        
        }else{
            // leaf nodes
            if (dict.ContainsKey(parentPath)){
                AddToDictionaryRecursive(obj, dict, parentPath + "`");
            }else{
                dict.Add(parentPath, obj?.ToString() ?? "null");
            }
        }


    }



    /// Escape strings using C# rules, permit """ if escapes required. Includes quotes
    internal static string EscapeStringCSharp(string value){
        var hasNewlines = value.Contains('\n');

        if (!hasNewlines && !invalidForDoubleQuoteString.IsMatch(value)){
            return "\"" + value + "\"";
        }
        if (!hasNewlines && !invalidForAtLiteral.IsMatch(value)){
            return "@" + "\"" + value + "\"";
        }

        // Use triple quotes
        if (!value.Contains("\"\"\"")){
            return "\"\"\"" + value + "\"\"\"";
        }
        if (!value.Contains("\"\"\"\"")){
            return "\"\"\"\"" + value + "\"\"\"\"";
        }
        throw new NotImplementedException("Haven't implemented escaping for 5 quotes yet");

    }

    /// <summary>
    /// Returns a string of the given dictionary in C# syntax
    /// </summary>
    /// <param name="dict"></param>
    /// <returns></returns>
    internal static string DictionaryToCSharp(string varName, Dictionary<string,string> dict){
        var sb = new StringBuilder();
        sb.AppendLine($"Dictionary<string,string> {varName} = new (){{");
        foreach (var kvp in dict){
            sb.AppendLine($"    {{{EscapeStringCSharp(kvp.Key)}, {EscapeStringCSharp(kvp.Value)}}},");
        }
        sb.AppendLine("};");
        return sb.ToString();
    }
    static Regex invalidForDoubleQuoteString = InvalidForDoubleQuoteString();

    [GeneratedRegex("[\0-\b\v\f\u000e-\u001f\\\\\"]")]
    private static partial Regex InvalidForDoubleQuoteString();

    static Regex invalidForAtLiteral = InvalidForAtLiteral();

    [GeneratedRegex("\"(?!\")")]
    private static partial Regex InvalidForAtLiteral();
}