using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Imageflow.Server.Configuration.Parsing;

internal readonly record struct ReferencedExpression(string Namespace, string VarName, string FullExpression, string OriginValue, string OriginKey, string SourcePath){

    // ToString with OriginKey, OriginValue and FullExpression
    public override string ToString() => $"${FullExpression} (from {OriginKey} = {OriginValue})";
}


interface IVarLookup{
    string Lookup(ReferencedExpression expr);
    string? TryLookup(ReferencedExpression expr);
}

internal partial class StringInteroplation {
    
    internal StringInteroplation(IVarLookup varLookup, string sourcePath){
        this.varLookup = varLookup;
        this.sourcePath = sourcePath;
    }
    IVarLookup varLookup;
    string sourcePath;


    [GeneratedRegex("(?<!\\\\)\\$\\{(.*?)\\}")]
    private static partial Regex VarReferenceExpr();


    [GeneratedRegex("^([a-zA-Z0-9_-]+\\.?)*$")]
    private static partial Regex ValidVarName();
    internal string InterpolateString(string text, string fromKey){
        // Tokenize string into segments of ${} and non-${} text, ignoring \$ sequences. 
        // Contents of ${} may contain A-Za-z0-9_- and . characters. The . can only be used once, between the namespace and var name. There may be spaces by the { }
        // ${env.VARNAME} will be replaced with the value of the environment variable VARNAME
        // ${vars.VARNAME} will be replaced with the value of the variable VARNAME
        // ${app.VARNAME} will be replaced with the value of the application-specific variable VARNAME
        // ${VARNAME} will be replaced with the value of the variable VARNAME

        // write tokenization using MatchEvaluator

        var matches = VarReferenceExpr();

        var result = matches.Replace(text, new MatchEvaluator((m) =>
        {
            // removing leading '${' and trailing '}' from the match
            string fullExpression = m.Groups[0].Value.Trim();
            string expression = m.Groups[1].Value.Trim();

            if (!ValidVarName().IsMatch(expression))
            {
                throw new FormatException($"Invalid characters (not just [a-zA-Z0-9_-.]) in interpolation expression '{fullExpression}' in key '{fromKey}' in file '{sourcePath}'");
            }

            // splitting token into namespace and variable name
            string[] parts = expression.Split(new char[] {'.'}, 2);
            string? namespacePart = parts.Length > 1 ? parts[0].Trim() : null;
            string varName = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();

            if (string.IsNullOrWhiteSpace(namespacePart))
            {
                throw new FormatException($"Missing namespace (app., env., vars., folders., files., secrets.) in interpolation expression: '{fullExpression}'. Did you mean '${{vars.{expression}}}'? (from key '{fromKey}') in file '{sourcePath}'");
            }
            if (parts.Length > 2 )
            {
                throw new FormatException($"Too many '.' characters in  '{fullExpression}' (only one namespace such as app., env., vars., folders., files., or secrets. permitted) in key '{fromKey}' in file '{sourcePath}");
            }
            if (string.IsNullOrWhiteSpace(varName))
            {
                throw new FormatException($"Missing variable name in expression: '{fullExpression}' in file '{sourcePath}'");
            }

            var exprRef = new ReferencedExpression(namespacePart, varName, fullExpression, text, fromKey, sourcePath);

            var newValue = varLookup.Lookup(exprRef); // Will throw if not found

            return newValue;
        }));

        // Now replace any escaped $ signs that we skipped earlier
        result = result.Replace(@"\$", "$");

        return result;
    }


}
internal class DefaultVarLookup : IVarLookup{

    public DefaultVarLookup(Dictionary<string,string> appVars, [NotNull]Func<string,string?> getEnvironmentVariable){
        AppVars = appVars;
        GetEnvironmentVariable = getEnvironmentVariable;
    }

    public DefaultVarLookup(Dictionary<string,string> appVars){
        AppVars = appVars;
        GetEnvironmentVariable = Environment.GetEnvironmentVariable;
    }
    private Dictionary<string,string> AppVars {get;  set; }

    private Func<string,string?> GetEnvironmentVariable {get; init;}

    public string Lookup(ReferencedExpression expr){
        var result = TryLookup(expr);
        if (result == null){
            var friendlyNamespace = expr.Namespace switch {
                "app" => "Application",
                "env" => "Environment",
                _ => expr.Namespace
            };
            
            throw new KeyNotFoundException($"{friendlyNamespace} variable '{expr.VarName}' not found. Used in '{expr.FullExpression}' in key '{expr.OriginKey}' in file '{expr.SourcePath}'");
        }
        return result;
    }
    public string? TryLookup(ReferencedExpression expr)
    {

        var varName = expr.VarName;
        switch (expr.Namespace)
        {
            case "app":
                return AppVars.ContainsKey(varName) ? AppVars[varName] : null;
            case "env":
                string? envVar = GetEnvironmentVariable(varName);
                if (envVar == null)
                {
                    //TODO: this isn't working
                    // Because $HOME is not set on Windows (sometimes), we need to construct it
                    if (string.Equals("home", varName, StringComparison.OrdinalIgnoreCase)){
                        // Let's not check the platform (buggy?), and just assume that if $HOME is not set,
                        // then it's safe to use $HOMEDRIVE and $HOMEPATH if they're both set
                        var homeDrive = GetEnvironmentVariable("HOMEDRIVE");
                        var homePath = GetEnvironmentVariable("HOMEPATH");
                        if (string.IsNullOrWhiteSpace(homeDrive) || string.IsNullOrWhiteSpace(homePath)){
                            return null;
                        }
                        return homeDrive + homePath;
                    }
                }
                return envVar;
            default:
                throw new FormatException($"Invalid namespace '{expr.Namespace}' in expression '{expr.FullExpression}'. Did you mean 'env.', 'vars.', or 'app.'? (from key '{expr.OriginKey}') in file '{expr.SourcePath}'");
        }
    }


}
