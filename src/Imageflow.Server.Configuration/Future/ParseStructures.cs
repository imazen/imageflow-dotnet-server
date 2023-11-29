namespace Imageflow.Server.Configuration.Future.ParseStructures;

using Tomlyn.Model;
using Imageflow.Server.Configuration.Parsing;

#nullable enable


// For the TOML above, map it to plain internal C# classes. Every class should inherit from ITomlMetadataProvider, IValidationCapable and define `TomlPropertiesMetadata? PropertiesMetadata { get; set; }`
// Reuse classes for tables that have the same fields, like max_frame_resolution, max_encode_resolution, and max_decode_resolution
// Ignore prefixes such as `production.`, `staging.` and `development.` as they are preprocessed out before Tomlyn maps the toml to classes

/*
# ======  ASP.NET Server settings ======
[aspnet_server]
use_developer_exception_page = false # Don't leak error details to the public
use_exception_handler = "/error"     # Use a custom error page (defined below)
use_hsts = true                      # Allow no HTTP connections whatsoever
use_https_redirection = true         # Redirect all HTTP requests to HTTPS, if any get through

# You can specify rewrites in apache or IIS style rules
# either inline with 'text' or in a separate file with 'path'
[aspnet_server.apache_mod_rewrite]
text = ''
[aspnet_server.iis_url_rewrite]
path = "${app_root}\\iis_rewrite.config"

*/
// Map arrays to List. 
// snake_case becomes PascalCase
// Add common-sense validations 
// Make everything nullable so it's clear if values aren't specified. 
// Each class should have a Validate(ValidationContext c) method that throws an exception if the values are invalid or required values are missing

// Kind of useless since preprocessing production., development., staging. causes line numbers to be off






internal class VarSection : ITomlMetadataProvider, IValidationCapable
{
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    public required string Name { get; set; }
    public required string Value { get; set; }
    public string? Fallback { get; set; }
    public bool? FolderMustExist { get; set; }
    public bool? FileMustExist { get; set; }
    public bool? Secret { get; set; }

    public void Validate(ValidationContext c)
    {
        c.Require(this, Name);
        c.Require(this, Value);
    }
}



internal class PresetSection : ITomlMetadataProvider, IValidationCapable
{
    public string? Name { get; set; }
    public string? Overrides { get; set; }
    public string? Defaults { get; set; }
    
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    
    public void Validate(ValidationContext c)
    {
        if(string.IsNullOrEmpty(Name)) throw new Exception("Name in Presets is required.");
    }
}


// internal class SourceSection : ITomlMetadataProvider, IValidationCapable
// {
//     public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
//     public string? Name { get; set; }

//     // We should be able to register providers with the configuration system
//     // And parse them individually. Unfortunately, that would mean a dependency on Tomlyn 
//     // Wherever the provider code is located, as well as integration with ValidationContext, 
//     // or some kind of proxy system or an impl in Imazen.Common etc.
//     public FileSystemSource? FileSystem { get; set; }
//     public S3Source? S3 { get; set; }
//     public HttpClientSource? HttpClient { get; set; }
//     public RemoteReaderSignedSource? RemoteReaderSigned { get; set; }
//     public AzureBlobSource? AzureBlob { get; set; }
    

//     public void Validate(ValidationContext c)
//     {
//         c.Require(this, Name, "name is required in each [[sources]] section, otherwise sources can't be referenced by routes");
        
//         if(FileSystem == null && S3 == null && HttpClient == null && RemoteReaderSigned == null && AzureBlob == null)
//         {
//             //TODO 
//             throw new Exception("At least one source must be defined in Sources.");
//         }
//     }
// }


internal class RouteSource : ITomlMetadataProvider, IValidationCapable{
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Container { get; set; }
    public void Validate(ValidationContext c)
    {
        // Add your validations here
        c.Require(this, Name);
        c.Require(this, Path);
    }
}

internal class HeaderPair: ITomlMetadataProvider, IValidationCapable
{
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }

    public void Validate(ValidationContext c)
    {
        c.Require(this, Name);
        c.Require(this, Value);
    }
}

// internal class Watermark : ITomlMetadataProvider, IValidationCapable
// {
//     public string? Name { get; set; }
//     public string? VirtualPath { get; set; }
//     public double? Opacity { get; set; }
//     public double? AlignXPercent { get; set; }
//     public double? AlignYPercent { get; set; }
//     public int? MinCanvasWidth { get; set; }
//     public int? MinCanvasHeight { get; set; }
//     public string? FitMode { get; set; }
//     public FitBoxPercent? FitBoxPercent { get; set; }
//     public double? SharpenPercent { get; set; }
// }




/*
# ======  Define Image Sources ======
[[sources]]
name = "fs" # name is required
[sources.filesystem] # one of .filesystem, .s3, .http_client, .azure_blob, .remote_reader_signed, .are required.
fix_slashes = true
slash = "/"
root = "${vars.ImageRoot}" # required for .filesystem

[[sources]]
name = "s3-east"
[sources.s3]
region = "us-east-1" # required
endpoint = "https://s3.amazonaws.com"
access_key_id = "${vars.S3AccessID}" # required
access_key_secret = "${vars.S3AccessKey}" # required


[[sources]]
name = "client1"
[sources.http_client]
max_redirections = 6 # Max redirects to follow (0 to prevent redirections)
max_connections = 16 # Max simultaneous connections to the same host - increase unless you hit problems
max_retries = 3 # Max retries per request w/ jitter ttps://github.com/App-vNext/Polly/wiki/Retry-with-jitter
initial_retry_ms = 100 # Initial retry delay in milliseconds
require_https = true # Require HTTPS for all requests (TODO: implement)
allowed_domains = ["*.example.com", "example.com"] # Allow requests to these domains (TODO: implement)
blocked_domains = ["*.example.org", "example.org"] # Block requests to these domains (TODO: implement)
# TODO - also implement a timeout, and a max response size, and magic byte verification
# TODO - also allow allowlisting domains
request_headers = [
    # { name = "Accept-Encoding", value = "gzip, deflate, br" },
    { name = "User-Agent", value = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36" },
]

[[sources]]
name="remote-signed"
[sources.remote_reader_signed]
http_client="client1"
# Signing keys are case-sensitive - and different from full URL signing
# Using a secret key, you can ensure that only specific URLs can be requested
signing_keys = [
    "${signing_key_1}",
    "signing_key_2",
] 

[[sources]]
name = "azure"
[sources.azure_blob]
connection_string = "${AzureBlobConnection}"
*/
internal class AzureSourceSection : ITomlMetadataProvider, IValidationCapable
{
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    public string? ConnectionString { get; set; }

    public void Validate(ValidationContext c)
    {
        c.Require(this, ConnectionString);
    }
}

internal class RemoteReaderSignedSourceSection : ITomlMetadataProvider, IValidationCapable
{
    public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
    public string? HttpClient { get; set; }
    public List<string>? SigningKeys { get; set; }

    public void Validate(ValidationContext c)
    {
        c.Require(this, HttpClient);
        c.Require(this, SigningKeys);
    }
}
// TomlParserOptions.ParseAndValidate to check syntax
 
