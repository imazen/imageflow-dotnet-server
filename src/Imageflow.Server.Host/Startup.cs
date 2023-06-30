namespace Imageflow.Server.Host;
using Imageflow.Server.Configuration;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

public partial class Startup
{

    IAppConfigurator configurator;

    readonly string configurationFilePath;
    readonly DateTime configurationFileLastModified;
    readonly FileSystemWatcher? configurationFileWatcher;
    readonly WebConfigToucher? webConfigToucher;
    public Startup(IConfiguration configuration, IWebHostEnvironment env)
    {
        Configuration = configuration;
        Env = env;
        var parserContext = new TomlParserContext(env);
        configurationFilePath = Path.Combine(Env.ContentRootPath, "imageflow.toml");
        configurationFileLastModified = File.GetLastWriteTimeUtc(configurationFilePath);
        var config = TomlParser.LoadAndParse(configurationFilePath, parserContext);
        configurator = config.GetAppConfigurator();

        // Watch for changes to the configuration file with a FileSystemWatcher
        if (configurator.RestartWhenThisFileChanges)
        {
            webConfigToucher = new WebConfigToucher(Path.Combine(Env.ContentRootPath, "Web.config"), null);
            configurationFileWatcher = new FileSystemWatcher(Env.ContentRootPath, "imageflow.toml");
            configurationFileWatcher.Changed += (sender, args) =>
            {
                if (args.ChangeType == WatcherChangeTypes.Changed && File.Exists(configurationFilePath))
                {
                    var lastModified = File.GetLastWriteTimeUtc(configurationFilePath);
                    // Compare the last write time to the previous one approximately due to NFS/FAT etc filesystem limitations
                    if (Math.Abs((lastModified - configurationFileLastModified).TotalSeconds) > 1){
                        
                        // Edit Web.config to trigger an app restart
                        webConfigToucher.TriggerRestart();
                    }
                }
            };
            configurationFileWatcher.EnableRaisingEvents = true;
        }
    }



    private IConfiguration Configuration { get; }
    private IWebHostEnvironment Env { get; }
    
    public void ConfigureServices(IServiceCollection services)
    {
        configurator.ConfigureServices(services);
    }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Notes: X-XSS-Protection should no longer be used 
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-XSS-Protection
        // X-Frame-Options is deprecated in favor of Content-Security-Policy, however, that should be used on servers that serve HTML documents
        // Image-only servers don't benefit from it (unless they serve SVG or HTML documents themselves which are directly visited)
        // Also, Imageflow Server makes good etags, no need to remove them

        configurator.ConfigureApp(app, env);
    }


}