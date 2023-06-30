using Tomlyn.Model;
using System.Runtime.CompilerServices;

namespace Imageflow.Server.Configuration.Parsing;
#nullable enable



internal readonly record struct ConfigTextPosition(int Offset, int Line, int Column){
    public string LineString => $"line {Line}";
    public override string ToString() => $"line {Line}, column {Column}";
}

// TomlTextContext can have start and end position, the filename? and the text contents
internal readonly partial record struct ConfigTextContext(ConfigRedactor? Redactor, ConfigTextPosition? Start, ConfigTextPosition? End, string? FileName, string? FileContents){
    public override string ToString() {
        var fileBit = !string.IsNullOrEmpty(FileName) ? $"in {FileName} " : "";

        if (Start == null && !string.IsNullOrEmpty(fileBit)) return fileBit;
        if (Start == null) return "(no context available)";

        var lineBit = Start?.Line == End?.Line || End == null ? $"line {Start?.Line}" : $"lines {Start?.Line} to {End?.Line}";

        // Check offsets are inside contents
        string? contents = "";
        var from = Start?.Offset ?? 0;
        var to = End?.Offset ?? 0;
        if (FileContents != null && from < FileContents.Length && to < FileContents.Length) contents = FileContents.Substring(from, to);
        
        contents = Redactor?.Redact(contents);
        if (!string.IsNullOrEmpty(contents)) contents = $" ({contents})";

        return $@"{fileBit}{lineBit}${contents}";
    }

    internal ConfigTextContext(string? fileName) 
        : this(null, null, null, fileName, null) { }

}


internal class ValidationContext{
    
    public int ConfigSchemaMajorVersion = 1;
    public int ConfigSchemaMinorVersion = 0;

    public string? FileName { get; init; }
    public string? FileContents {get; init;}

    public required ConfigRedactor Redactor { get; init; }
    
    internal readonly record struct ContextLayer(string PropertyName, string PropertyType);

    public Stack<ContextLayer> ContextHints { get; set; } = new Stack<ContextLayer>();


    public void ValidateRecursive<P,T>(P parent, T obj, bool? expression, string? failureMessage = null,
            [CallerArgumentExpression(nameof(parent))] string? parentExpr=null, // will just be 'this', typically
             [CallerArgumentExpression(nameof(obj))] string? objExpression=null,
             [CallerArgumentExpression(nameof(expression))] string? exprExpression=null)  where P: IValidationCapable, ITomlMetadataProvider
    {
        if (expression == false){
            throw new InvalidConfigPropertyException(){
                PropertyName = PascalToSnakeCase(objExpression),
                PropertyValue = obj?.ToString() ?? "null",
                ParentFullName = PascalToSnakeCase(GetPropertyAddress()),
                TextContext = new ConfigTextContext(FileName), // TODO, once we can account for preprocessing to adjust line numbers
                FailedExpectation = exprExpression,
                FailureMessage = failureMessage 
            };
        }
        if (obj is ICollection<P> list){
            ContextHints.Push(new ContextLayer(objExpression ?? "unknown", typeof(T).Name));
            foreach (var item in list){
                ValidateRecursive(parent, item, expression, parentExpr, objExpression, exprExpression);
            }
            ContextHints.Pop();
        }
        else if (obj is IValidationCapable validationCapable && validationCapable != null){
            ContextHints.Push(new ContextLayer(objExpression ?? "unknown", typeof(T).Name));
            validationCapable.Validate(this);
            ContextHints.Pop();
        }
    }

    public void Require<P,T>(P parent, T obj, string? failureMessage = null,
            [CallerArgumentExpression(nameof(parent))] string? parentExpr=null, // will just be 'this', typically
             [CallerArgumentExpression(nameof(obj))] string? objExpression=null) where P: IValidationCapable, ITomlMetadataProvider
    {
        if (EqualityComparer<T>.Default.Equals(obj, default(T))){
            throw new MissingConfigPropertyException(){
                PropertyName = PascalToSnakeCase(objExpression),
                ParentFullName = PascalToSnakeCase(GetPropertyAddress()),
                TextContext = new ConfigTextContext(FileName), // TODO, once we can account for preprocessing to adjust line numbers
                FailureMessage = failureMessage
            };
        }
    }

    // It's NOT YET possible to get a SourceSpan (TextPosition) for a property
    // https://github.com/xoofx/Tomlyn/issues/66
    private static ConfigTextPosition? GetErrorLocation(ITomlMetadataProvider parent, string propertyName){
        var snakeName = PascalToSnakeCase(propertyName);
        if (parent.PropertiesMetadata == null) return null;

        if (snakeName != null){
            if (parent.PropertiesMetadata.TryGetProperty(snakeName, out var metadata)){
            // return new TomlTextPosition(metadata.SourceSpan.Position, metadata.Line, metadata.Column);
            }
        }
        return null;
    } 

    private static string? PascalToSnakeCase(string? name){
        return name == null ? null : string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLowerInvariant();
    }

    private string GetPropertyAddress(){
        return string.Join(".", ContextHints.Select(x => $"{PascalToSnakeCase(x.PropertyName)}").Reverse());
    }

    internal InvalidConfigPropertyException InvalidProperty<T>(T obj, string? failedExpectation, [CallerArgumentExpression(nameof(obj))] string? objExpression=null){
        return new InvalidConfigPropertyException(){
            PropertyName = PascalToSnakeCase(objExpression),
            ParentFullName = PascalToSnakeCase(GetPropertyAddress()),
            TextContext = new ConfigTextContext(FileName), // TODO, once we can account for preprocessing to adjust line numbers
            FailedExpectation = failedExpectation
        };
    }

}

