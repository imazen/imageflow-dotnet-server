



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

## Add a Retry Policy for Transient Errors 
Add the `Microsoft.Extensions.Http.Polly` nuget package to your project
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

## Endpoint-Specific Configuration
The `HttpClientSelector` accepts a uri and is expected to retun the name of a configured http client.
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