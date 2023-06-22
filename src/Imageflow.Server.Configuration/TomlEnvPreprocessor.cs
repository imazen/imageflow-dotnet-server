using Tomlyn.Model;
namespace Imageflow.Server.Configuration.Parsing;


// Someday, instead of working with TomlTable, we'll work with a strongly typed object model
// We'll then be able to retain line numbers


internal record TomlPreprocessorResult(TomlTable ProcessedModel, string ProcessedText);
internal class TomlEnvironmentPreprocessor{
    readonly List<string> allEnvironments = new(){ "production", "staging", "development" };
    readonly string environmentName;
    readonly string sourcePath;
    readonly string tomlText;

   public TomlEnvironmentPreprocessor(string tomlText, string activeEnvironmentName, string sourcePath){

        this.tomlText = tomlText;
        this.environmentName = activeEnvironmentName.ToLowerInvariant();
        // validate environment name
        if (!allEnvironments.Contains(environmentName)){
            throw new Exception($"Environment name '{environmentName}' is not valid. Must be one of {string.Join(", ", allEnvironments)}");
        }
        this.sourcePath = sourcePath;

    }

    internal TomlPreprocessorResult Preprocess(){
        // First, preprocess all names that start with 'production.' or 'development.' into the appropriate section
        var originalDoc = Tomlyn.Toml.Parse(tomlText, sourcePath);
        var dynModel = Tomlyn.Toml.ToModel(originalDoc);
        dynModel = PreprocessToml(dynModel);

        
        var processed = Tomlyn.Toml.FromModel(dynModel);
        return new TomlPreprocessorResult(dynModel, processed);
    }


    TomlTable PreprocessToml(TomlTable dynModel){
        // This doesn't work on arrays, and only works on top level tables. 
        // Nor does it work on nested tables. 
        // TODO: at least allow top-level arrays to be processed
        // TODO: fail if env names are used as keys anywhere they aren't supported


        if (dynModel.TryGetValue(environmentName, out var envObj)){
            if (envObj is TomlTable envTable){
                MergeMutate(dynModel, envTable,  environmentName, "");
            }
        }
        // Remove all the others.
        foreach(string env in allEnvironments){
            dynModel.Remove(env);
        }
        return dynModel;
    }

        // // recursively check all tables and arrays

    object MergeMutate(object into, object from, string oldKey, string newKey){
        // A TOML table maps to a TomlTable object and is in practice a IDictionary<string, object?>.
        // A TOML table array maps to a TomlTableArray object
        // Both must be the same type to merge
        // If the types are different, throw an exception
        // If the types are the same, merge the two objects
        if (into.GetType() != from.GetType()){
            throw new Exception($"Cannot merge '{oldKey}' into '{newKey}' because they are different types ({into}, {from}). Used in file '{sourcePath}'");
        }
        if (into is TomlTable intoTable)
        {
            var fromTable = (TomlTable)from;
            foreach (var key in fromTable.Keys)
            {
                if (!intoTable.ContainsKey(key))
                {
                    intoTable.Add(key, fromTable[key]);
                    continue;
                }else{
                    //Overwrite existing keys
                    intoTable[key] = MergeMutate(intoTable[key], fromTable[key], $"{oldKey}.{key}", $"{newKey}.{key}");
                }
            }
            return intoTable;
        }
        else if (into is TomlTableArray intoTableArray)
        {
            var fromTableArray = (TomlTableArray)from;
            foreach (var table in fromTableArray)
            {
                intoTableArray.Add(table);
            }
            return intoTableArray;
        }
        else
        {
            // check it's a TomlArray, double, long, or string
            if (from is TomlArray || from is double || from is long || from is string || from is bool)
            {
                return from;
            }
            throw new Exception($"Cannot merge '{oldKey}' into '{newKey}' because they are not a recognized type ({from}). Used in file '{sourcePath}'");
        }
    }


}

