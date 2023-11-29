//diagnostics.allow_with_password and //license.key 

using Tomlyn.Model;

namespace Imageflow.Server.Configuration.Parsing;

// TODO: major flaw, secret redaction won't work if the secret contains escape sequences. 
// We should explore the dynamic object model of the toml to redact strings, but also record a secretsSet so we can redact secrets from comments as well

internal interface IConfigRedactor{
    string Redact(string input);
}
internal partial class ConfigRedactor : IConfigRedactor{

    HashSet<string> secrets = new();
    internal ConfigRedactor(TomlTable table){
        //license.key
        if (table.TryGetValue("license", out var licenseObj) && licenseObj is TomlTable license) {
            if (license.TryGetValue("key", out var keyObj) && keyObj is string key) {
                secrets.Add(key);
            }
        }
        // Now diagnostics.allow_with_password
        if (table.TryGetValue("diagnostics", out var diagnosticsObj) && diagnosticsObj is TomlTable diagnostics) {
            if (diagnostics.TryGetValue("allow_with_password", out var allowObj) && allowObj is string allow) {
                secrets.Add(allow);
            }
        }
    }

    public string Redact(string input){
        var output = input;
        // Go through each secret and replace it with ****** in output
        foreach (var secret in secrets){
            output = output.Replace(secret, "******");
        }
        
        return output;
    }

    internal List<string> GetAllSecrets()
    {
        return secrets.ToList();
    }
}