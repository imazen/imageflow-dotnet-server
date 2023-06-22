using System.Text.RegularExpressions;
using Tomlyn.Model;

namespace Imageflow.Server.Configuration.Future;

// TODO: major flaw, secret redaction won't work if the secret contains escape sequences. 
// We should explore the dynamic object model of the toml to redact strings, but also record a secretsSet so we can redact secrets from comments as well

internal partial class ConfigRedactor{

    internal ConfigRedactor(TomlTable table){
        // Extract harmless and secret vars from toml 
        PopulateSets(table);
    }

    HashSet<string> secrets = new();
    HashSet<string> harmlessStrings = new(){ "watermark", "fit", "fit_crop", "within", "within_crop", "distort", "fit_mode", "up_filter", "down_filter", "none", "null", "true", "false",
        "http_402_error", "http_422_error" };
    HashSet<string> harmlessKeys = new(){ "enforcement", "for", "path", "name", "virtual_path", "physical_path", "relative_to", "fit_mode", "content_type", "content", "container", "apply_override_commands", "prefix",
            "route", "up_filter", "down_filter", "region", "apply_default_commands", "cache_control_string", "http_client", "root", "use_exception_handler", "config_schema" , "allowed_domains", "blocked_domains"};
        // Define a Set of strings like "watermark" and other strings from the TOML that are harmless
    static HashSet<string> SecretKeys => new(){ "license", "signing_keys", "access_key_secret", "access_key_id", "configuration_string"};

        
    // Test if a string is harmless (just a var reference, or numeric, or a known enum value)
    bool IsHarmless(string input){
        if (string.IsNullOrEmpty(input)) return true;
        if (secrets.Contains(input)) return false;
        if (harmlessStrings.Contains(input)) return true;

        if (IsVariableReference(input)) return true;
        if (int.TryParse(input, out var _)) return true;
        if (double.TryParse(input, out var _)) return true;

        return false; //Default to strings being secrets
    }

    

    // recurse through table and all subtables, adding certain property values to a harmless set
    internal void PopulateSets(TomlObject element){
        if (element is TomlTable table){
            foreach (var kvp in table){
                var value = kvp.Value;
                var key = kvp.Key;
                if (value == null) continue;
                if (value is TomlTable subtable) PopulateSets(subtable);
                if (value is TomlArray arr) PopulateSets(arr);


                if (table.ContainsKey("secret") && (table["secret"] as bool?) == true) {
                    AddStrings(secrets, value);
                } else if (table.ContainsKey("name") && "Authorization".Equals(table["name"] as string, StringComparison.OrdinalIgnoreCase)) {
                    AddStrings(secrets, value);
                } else if (SecretKeys.Contains(key)){
                    AddStrings(secrets, value);
                } else if (harmlessKeys.Contains(key)){
                    AddStrings(harmlessStrings, value);
                } else if (table.ContainsKey("secret") && (table["secret"] as bool?) == false)
                {
                    AddStrings(harmlessStrings, value);
                } 
            }
        } else if (element is TomlArray arr){
            foreach (var item in arr) {
                if (item is TomlObject obj){
                    PopulateSets(obj);
                }
            }
        }
    }

    internal static void AddStrings(HashSet<string> collection, object element){
        if (element is TomlArray arr){
            foreach (var item in arr) {
                var str = item?.ToString();
                if (!string.IsNullOrWhiteSpace(str)){
                    collection.Add(str);
                }
            }
        }else {
            var str = element?.ToString();
            if (!string.IsNullOrWhiteSpace(str)){
                collection.Add(str);
            }
        }
    }
    
    // Allow strings through where match 1 is a variable reference such as ${var.iable}
    bool IsVariableReference(string input) => VariableReference().IsMatch(input);


    // Regexes to check if string is a query string like name=value&name2=value2
    [GeneratedRegex(@"^([a-zA-Z0-9_\-]+=[a-zA-Z0-9_\-]+&)*([a-zA-Z0-9_\-]+=[a-zA-Z0-9_\-]+)$")] // Unescaped, "^([a-zA-Z0-9_\-]+=[a-zA-Z0-9_\-]+&)*([a-zA-Z0-9_\-]+=[a-zA-Z0-9_\-]+)$"
    internal static partial Regex QueryString();

    [GeneratedRegex(@"^(\$\{([^\}]+)\}|\\|/|\s*)$")] //Allow multiple variable references, whitespace, and slashes
    internal static partial Regex VariableReference();

    [GeneratedRegex("\"\"\"(.*)\"\"\"")] // TOML doesn't support escaping triple quotes, so this is safe
    internal static partial Regex TripleQuoted();
    [GeneratedRegex("'([^']*)'")] // TOML doesn't support escaping single quotes, so this is safe
    internal static partial Regex SingleQuoted();
    [GeneratedRegex("\"((?:\\\\\"|[^\"])*)\"")] // Unescaped, "(?:\\"|[^"])*"
    internal static partial Regex DoubleQuoted();
    public string Redact(string input){
        // Go through all matches for each regex, and replace the match with ****** unless it's a variable reference
        var output = input;
        output = TripleQuoted().Replace(output, match => IsHarmless(match.Value) ? match.Value : "\"\"\"******\"\"\"");
        output = SingleQuoted().Replace(output, match => IsHarmless(match.Value) ? match.Value : "'******'");
        output = DoubleQuoted().Replace(output, match => IsHarmless(match.Value) ? match.Value : "\"******\"");

        // Go through each secret and replace it with ****** in output
        foreach (var secret in secrets){
            output = output.Replace(secret, "******");
        }
        
        return output;
    }
}

// // Write unit tests for each string regex
// public class StringRegexTests {

//     [Fact]
//     public void TestQueryString() {
//         var regex = ConfigRedactor.QueryString();

//         Assert.Matches(regex, "name=value");
//         Assert.Matches(regex, "name=value&name2=value2");
//         Assert.Matches(regex, "name=value&name2=value2&");
//         Assert.DoesNotMatch(regex, "name=value&name2=value2&?");
//         Assert.DoesNotMatch(regex, "name=value&name2=value2&?&");

//     }

//     [Fact]
//     public void TestVariableReference(){
//         var regex = ConfigRedactor.VariableReference();

//         Assert.Matches(regex, "${variable}");
//         Assert.Matches(regex, "${var.iable}");
//         Assert.Matches(regex, "${var_iable}");
//         Assert.Matches(regex, "${var-iable}");
//         Assert.DoesNotMatch(regex, "name=value&name2=value2&?&"); ;
//     }
// }