



## Basic Configuration
```
    services.AddHttpClient();

    var remoteReaderServiceOptions = new RemoteReaderServiceOptions
    {
        SigningKey = "ChangeMe"
    }
    .AddPrefix("/remote/");

    services.AddImageflowRemoteReaderService(remoteReaderServiceOptions);
```

## Usage

```c#
// The origin file
var remoteUrl = "https://imageflow-resources.s3-us-west-2.amazonaws.com/test_inputs/imazen_400.png";
// We encode it, but this doesn't add the /remote/ prefix since that is configurable
var encodedRemoteUrl = RemoteReaderService.EncodeAndSignUrl(remoteUrl, remoteReaderKey);
// Now we add the /remote/ prefix and add some commands
var modifiedUrl = $"/remote/{encodedRemoteUrl}?width=100";
```

If we are also doing request signing (a different signing key and purpose), we would use,
```c#
var signedModifiedUrl = Imazen.Common.Helpers.Signatures.SignRequest(modifiedUrl, requestSigningKey);

//This of course assumes that in Startup.cs you set requestSigningKey as one of the valid keys
app.UseImageflow(new ImageflowMiddlewareOptions()
                    .SetRequestSignatureOptions(
                        new RequestSignatureOptions(SignatureRequired.ForAllRequests, 
                                new []{requestSigningKey})
                    ));
                                
```
## Add Custom Headers
To configure the client to send custom headers, we need to use an overload of `AddHttpClient()` that returns an `IHttpClientBuilder`.
```
    services.AddHttpClient("ImageFlow-DotNet-Server", config =>
    {
        config.DefaultRequestHeaders.Add("user-agent", "ImageFlow-DotNet-Server");
    });
```
To use the "named" client we configured above, configure a simple selector that returns its name.
```
    var remoteReaderServiceOptions = new RemoteReaderServiceOptions
    {
        SigningKey = "ChangeMe",
        HttpClientSelector = _ => "ImageFlow-DotNet-Server"
    }
    .AddPrefix("/remote/");
```

## Limiting Redirects 
The `RemoteReaderServiceOptions.RedirectLimit` has been removed in favour of configuring the `HttpClientHandler` directly. 
```
    services.AddHttpClient(nameof(RemoteReaderService))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10

        });

    var remoteReaderServiceOptions = new RemoteReaderServiceOptions
    {
        SigningKey = "ChangeMe",
        HttpClientSelector = _ => nameof(RemoteReaderService)
    }
    .AddPrefix("/remote/");
```


## Add a Retry Policy for Transient Errors 
Adding the `Microsoft.Extensions.Http.Polly` nuget package to your project will make available the Polly ClientBuilderExtensions.
```
    services.AddHttpClient(nameof(RemoteReaderService))
        .AddTransientHttpErrorPolicy(builder => builder.RetryAsync());

    var remoteReaderServiceOptions = new RemoteReaderServiceOptions
    {
        SigningKey = "ChangeMe",
        HttpClientSelector = _ => nameof(RemoteReaderService)
    }
    .AddPrefix("/remote/");
```

## Host-Specific Configuration
The `HttpClientSelector` accepts a uri and is expected to retun the name of a configured http client. This simple example shows how you could add an `authorization` header for a specific remote host.
```
    services.AddHttpClient("TrickyHttpClient", config =>
    {
        config.DefaultRequestHeaders.Add("user-agent", "Tricky Client");
        config.DefaultRequestHeaders.Add("authorization", "secret");
    });

    var remoteReaderServiceOptions = new RemoteReaderServiceOptions
    {
        SigningKey = "ChangeMe",
        HttpClientSelector = uri =>
        {
            return uri.Host switch
            {
                "tricky.endpoint.local" => "TrickyHttpClient",
                _ => nameof(RemoteReaderService)
            };
        }
    }
    .AddPrefix("/remote/");


``` 