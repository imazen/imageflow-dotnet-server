# TOML Configuration for Imageflow.Server.Host

Hooray! You no longer need a build step to deploy Imageflow Server. We're publishing a precompiled project that uses Imageflow.Server.Configuration, and auto-restarts when you edit imageflow.toml (we hope).

That said, having a repository with your configuration and some automated deploy/CI is still a GREAT idea, so you may copy/paste Imageflow.Server.Host into your own project, or use it as a reference for your own deployment. 

**NOTE: This is a work-in-progress, bugs may exist, and error messages when you mess the config file up may be confusing. Please report any issues.**

## General ASP.NET deployment help.

Imageflow.Server.Host can be copy/pasted into your IIS site, and configured with imageflow.toml Don't forget to install the ASP.NET Hosting Bundle if you're deploying to windows.

[Deploying an ASP.NET app](https://learn.microsoft.com/en-us/visualstudio/deployment/deploying-applications-services-and-components-resources?view=vs-2022)

![](https://github.com/imazen/imageflow-dotnet-server/blob/c294027bfa1dbe7962758c5c7b21647523d7c518/examples/hosting_bundle.png)


## imageflow.toml reference

```toml

[imageflow_server] # must be valid TOML https://toml.io/en/ 
config_schema = '1.0' # Breaking changes in new versions may require this to be incremented

[license]
enforcement = "watermark" # or http_402_error/http_422_error, for fast failures
key = """
[PUT YOUR LICENSE KEY HERE]
""" # triple-quoted strings can span multiple lines


## Define routes, and map them to physical folders on disk
[route_defaults]
prefix_case_sensitive = true
lowercase_path_remainder = false
cache_control = "public, max-age=2592000"
# The following commands will be applied to all images, unless overridden
apply_default_commands = "quality=76&webp.quality=70&f.sharpen=23&down.filter=mitchell"

[[routes]]
prefix = '/images/'
map_to_physical_folder='${app.approot}\images\'

# ======  Caching to disk ======
[disk_cache] # HybridCache
enabled = true #required
# Don't put this on high-latency network storage, it will be slow
# Also cannot be inside an IIS website (unlike ImageResizer)
folder = '${env.TEMP}\ImageflowCache' # required
cache_size_mb = 30_000           # Disk space usage limit in MB
#write_queue_ram_mb = 200          # How much RAM to permit for async writes

# The number of shards to split the metabase into (default: 8)
# More shards = more open write-ahead-log files, slower shutdown,
# but less lock contention and faster cache hits
# database_shards = 8 # Manually delete the cache dir if you change this
# seconds_until_evictable = 30 # How long before a newly-written file can be deleted


# ======  Diagnostics access ======
# Prefix keys with 'production.', 'staging.', or 'development.' 
# to merge with the appropriate section based on the current environment
# Defaults to 'production' if DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT are not set
[diagnostics]
# Access anywhere with /imageflow.debug?password=[[redacted]]
#allow_with_password = "[[redacted_must_be_12+chars]]"
# Allow localhost without a password
allow_localhost = true

# In development mode, allow any host without a password
[development.diagnostics]
allow_anyhost = true

# ======  ASP.NET Server settings ======
[aspnet_server]
use_developer_exception_page = false # Don't leak error details to the public
use_exception_handler = "/error"     # Use a custom error page (defined below)
use_hsts = true                      # Allow no HTTP connections whatsoever
use_https_redirection = true         # Redirect all HTTP requests to HTTPS, if any get through

# ASP.NET supports a subset of the apache mod_rewrite syntax
# [aspnet_server.apache_mod_rewrite]
# file = "${app.approot}\\apache_rewrites.ini"

[development.aspnet_server]
use_developer_exception_page = true # Error details in dev please
use_hsts = false                    # Don't ban unencrypted HTTP in development
use_https_redirection = false       # Or redirect to HTTPS

[[static_response]]
for = "/error" # required
status_code = 500 # required
cache_control = "no-cache"
content_type = "text/html" # required
content = '<p>An error has occurred while processing the request.</p>' # required
# file = "/var/www/error.html"

[security.max_decode_resolution]
width = 12000
height = 12000
megapixels = 80

[security.max_encode_resolution]
width = 12000
height = 12000
megapixels = 40

[security.max_frame_resolution]
width = 12000
height = 12000
megapixels = 80

```