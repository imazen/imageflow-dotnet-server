namespace Imazen.Abstractions.AssemblyAttributes;

/// An assembly attribute that indicates the nuget package name the assembly is primarily distributed with
/// It can alternately be "{Assembly.Name}" to just inherit that value.
[AttributeUsage(AttributeTargets.Assembly)]
public class NugetPackageAttribute : Attribute
{
    public string PackageName { get; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="packageName">
    /// {Assembly.Name} to inherit the assembly name, or the name of the nuget package
    /// </param>
    public NugetPackageAttribute(string packageName)
    {
        PackageName = packageName;
    }

    public override string ToString()
    {
        return $"This assembly is primarily distributed with the nuget package '{PackageName}'";
    }
}
 