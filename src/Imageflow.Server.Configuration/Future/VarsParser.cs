using Tomlyn.Model;

namespace Imageflow.Server.Configuration.Parsing.Future;
internal class VarsConsts{
    
const string VarsExample = """
# Variable interpolation is supported in 'strings ${vars.A}' and "like ${vars.B}${env.YOUR_ENV_VAR}${app.approot}" 
# Use single-quote strings like 'this\for\paths' or "\\escape\\backslashes\\like\\this"
[vars.vars."home2"]  # Var names are case-sensitive, and [A-Za-z0-9_-] only                       
value = "${env.HOME}"                        # ${env.NAME} will inject the given env var. value is required.
fallback = "${env.HOMEDRIVE}${env.HOMEPATH}" # Used if value is empty or has undefined variables
folder_must_exist = true                     # Throw an error if the variable value isn't a folder that exists on disk
file_must_exist = false
secret = false                               # this defaults to false. true will ensure the value is redacted from /imageflow.debug

# [vars] offers shorthand ways to define a variable with non-secret, folder-checking, file-checking, and secret-redaction enabled respectively. 
# Use [[var]] if you need a fallback or to make a folder or file a secret
[vars]
vars.app_root = "${app.approot}"                  # The hosting app can provide vars too, like the app deployment root
files.rewrites = "${app_root}\\rewrites.ini"   # Vars can reference other vars
folders.ImageRoot = "C:\\Images"
folders.CacheDrive = "D:\\"
folders.ImageCache = "${folders.CacheDrive}\\ImageflowCache"
folders.home = { "value" = "${env.HOME}", fallback = "${env.HOMEDRIVE}${env.HOMEPATH}", folder_must_exist = true, secret=false, file_must_exist = false }
secrets.AzureBlobConnection = "DefaultEndpointsProtocol=https;AccountName=example;AccountKey=example;EndpointSuffix=core.windows.net"
secrets.S3AccessID = "example"
secrets.S3AccessKey = "example"
""";

}

internal class VariableDefinition{
    public required string Name {get;init;}
    public required string? Value {get;init;}
    public string? Fallback {get;init;} = null;
    public bool? FolderMustExist {get;init;} = false;
    public bool? FileMustExist {get;init;} = false;
    public bool? IsSecret {get; init;} = null;
}

// Parses a TomlTable into a Dictionary<string, VariableDefinition>
internal partial class VarsParser{

    private readonly Dictionary<string, VariableDefinition> vars;
    private readonly TomlParserContext context;
    private readonly string sourcePath;

    internal required Func<string,string,string> InterpolateString {get; init;}
    internal VarsParser(TomlTable? varsTable, TomlParserContext context, string sourcePath){
        this.context = context;
        this.sourcePath = sourcePath;

        // vars may contain "vars", "folders", "files", and "secrets", but no other keys
        // All of those must be a table. All keys of that table are var names. All values of the table must be a string or another Table.
        // If they are a table, then they must have a "value" key, and optionally a "fallback" key. 
        // They can also have "folder_must_exist" and "file_must_exist" keys and secret=true

        var variables = new Dictionary<string, VariableDefinition>();
        this.vars = variables;
        if (varsTable is null){
            return;
        }
        foreach(var varKind in varsTable.Keys){
            if (varKind != "vars" && varKind != "folders" && varKind != "files" && varKind != "secrets"){
                throw new Exception($"[vars] '{varKind}' is not permitted. '");
            }
            if (varsTable[varKind] is not TomlTable kindTable)
            {
                throw new Exception($"[vars] '{varKind}' must be a table. '");
            }
            foreach (var varName in kindTable.Keys){
                VariableDefinition variable;
                var varVal = kindTable[varName];
                if (varVal is TomlTable varProps){
                    variable = ParseVarTable(varProps, varKind, varName, context);
                }
                else{
                    variable = new VariableDefinition{
                        Name = varName,
                        Value = varVal.ToString() ?? throw new Exception($"[vars] '{varName}' must be convertible to a string. '"),
                        IsSecret = varKind == "secrets" ? true : null,
                        FolderMustExist = varKind == "folders" ? true : null,
                        FileMustExist = varKind == "files" ? true : null
                    };
                }
                if (variables.ContainsKey(varName)){
                    throw new Exception($"variable '{varName}' is defined more than once in [vars]");
                } else{
                    variables.Add(varName, variable);
                }
            }
        }

    }

    static VariableDefinition ParseVarTable(TomlTable varProps, string varKind, string varName, TomlParserContext context){
        // Ensure only defined keys are present
        // Require "value" key
        string? value;
        if (varProps.ContainsKey("value")){
            value = varProps["value"].ToString();
        } else{
            throw new Exception($"[vars] {varKind}.{varName}.value is required. '");
        }
        // Allow "fallback" key
        string? fallback = null;
        if (varProps.ContainsKey("fallback")){
            fallback = varProps["fallback"].ToString();
        }
        // Allow "folder_must_exist" key
        bool? folderMustExist = null;
        if (varProps.ContainsKey("folder_must_exist")){
            folderMustExist = varProps["folder_must_exist"] as bool?;
        }
        // Allow "file_must_exist" key
        bool? fileMustExist = null;
        if (varProps.ContainsKey("file_must_exist")){
            fileMustExist = varProps["file_must_exist"] as bool?;
        }
        // Allow "secret" key
        bool? isSecret = null;
        if (varProps.ContainsKey("secret")){
            isSecret = varProps["secret"] as bool?;
        }

        foreach (var key in varProps.Keys){
            if (key != "value" && key != "fallback" && key != "folder_must_exist" && key != "file_must_exist" && key != "secret"){
                throw new Exception($"[vars] {varKind}.${varName}.{key} is not permitted. Only 'value', 'fallback', 'folder_must_exist', 'file_must_exist', and 'secret' are permitted. '");
            }
        }
        return new VariableDefinition{
            Name = varName,
            Value = value,
            Fallback = fallback,
            FolderMustExist = folderMustExist,
            FileMustExist = fileMustExist,
            IsSecret = isSecret
        };
    }

    
    private struct EvaluationContext{
      //  internal Stack<string> VariableStack; // Circular reference detection
      //  internal VarsParser VarsParser;
    }

    internal string? Get(string name) => GetEvaluatedVariableValue(name);

    internal string? GetEvaluatedVariableValue(string name){
        // Pull from the array of tables in the toml document [[vars]]
        // If the variable is not found, return null
        // If the variable is found, evaluate the variable expression
        // If there are evaluation errors (missing variables, etc), evaluate the fallback expression
        // If there are evaluation errors in the fallback expression, throw an exception
        // If there are no evaluation errors, return the evaluated value
        // Write code


        // If the variable is not found, return null
        if (!vars.ContainsKey(name)){
            return null;
        }
        var varDef = vars[name];
        var value = vars[name].Value;
        var fallback = vars[name].Fallback;

        string evaluated;
        try{
            if (value is null){
                throw new Exception($"Error evaluating variable '{name}' with null value"); //meh
            }
            evaluated = InterpolateString(value, name);
        }
        catch (Exception e){
            if (fallback == null){
                throw new Exception($"Error evaluating variable '{name}' with value '{value}' and no fallback expression", e);
            }
            evaluated = InterpolateString(fallback, name);
        }
        if (varDef.FolderMustExist ?? false){
            // check folder existence
            if (!Directory.Exists(evaluated)){
                throw new DirectoryNotFoundException($"[[vars]] '{name}' evaluated value '{evaluated}' does not exist as a folder on disk. Defined in file '{sourcePath}'");
            }
        }
        if (varDef.FileMustExist ?? false){
            if (!File.Exists(evaluated)){
                throw new FileNotFoundException($"[[vars]] '{name}' with evaluated value '{evaluated}' does not exist as a file on disk. Defined in file '{sourcePath}'");
            }
        }
        //TODO: we likely want to add a slash normalization feature of some kind
        return evaluated;
    }


}

// // Create xunit tests for variable interpolation
// internal class VarsParserTests
// {

     

//     [Fact]
//     public void TestParseTomlTable()
//     {
//         //Parse a TOML table with Tomlyn
//         var originalDoc = Tomlyn.Toml.Parse("""
// [vars]
// vars.a = "something"
// vars.b = "${vars.a}"
// secrets.c = "${env.HOME}"
// """, "test.toml");


// // folders.k = "${vars.a}"
// // files.z = "${vars.a}"

//         var dynModel = Tomlyn.Toml.ToModel(originalDoc);

//         var parser = new VarsParser(dynModel["vars"] as TomlTable, new ConfigurationParserContext("development",
//             new Dictionary<string, string>(){
//                 {"a", "appvar"}
//             },
//             (name) => {
//                 if (name == "HOME"){
//                     return "/home/user";
//                 }
//                 return null;
//             }
//         ));

//         Assert.Equal("something", parser.Get("a"));
//         Assert.Equal("something", parser.Get("b"));

        
//     }
// }

