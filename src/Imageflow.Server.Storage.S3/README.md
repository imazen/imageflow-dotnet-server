
## Overview
This release moves construction of the S3 client outside of `ImageFlowServer` leveraging the configuration helpers provided by `AWSSDK.Extensions.NETCore.Setup`.  In most cases, this is all that should be needed to configure the S3Service.


```xml
<PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.3.101" />
```


```c#

services.AddAWSService<IAmazonS3>();
services.AddImageflowS3Service(new S3ServiceOptions()
  .MapPrefix("/images/", "image-bucket")
);
```

## Breaking Change - S3ServiceOptions Constructors

The `S3ServiceOptions` parameterless constructor is now the only constructor, and it no longer assumes anonymous credentials.  If you previously used the default constructor you *might* need to explicitly use anonymous credentials.  If you used either of the other two constructors that accepted credentials, you should now specify them when configuring the S3 client instead.
```c#
services.AddAWSService<IAmazonS3>(new AWSOptions 
{
    Credentials = new AnonymousAWSCredentials(),
    Region = RegionEndpoint.USEast1
});

- or -

services.AddAWSService<IAmazonS3>(new AWSOptions 
{
    Credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey),
    Region = RegionEndpoint.USEast1
});

```

## Breaking Change - MapPrefix RegionEndpoint Removed

The `RegionEndpoint` parameter has been removed from the `S3ServiceOptions.MapPrefix()` methods in favour of leveraging the standard AWS configuration options.  The `AddAWSService<>` method can pull configuration from a varienty of places such as the SDK store, instance profiles, configuration files, etc.  You can also set explicitly in code as in the example above.


## Breaking Change - MapPrefix S3ClientFactory Removed
There may be cases where different prefix mapping require different s3 client credentials.  In the previous release this could be accomplished by passing a `Func<IAmazonS3> s3ClientFactory` that would be called to create a client for the mapping on every request, and disposing the client at the end of the request.  This has been replaced by a `IAmazonS3 s3Client` parameter that will be used for the lifetime of the application.  Create an s3 client by any means supported by the AWSSDK and pass it into `MapPrefix()` 
```c#
var s3client1 = new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
var s3client2 = Configuration.GetAWSOptions().CreateServiceClient<IAmazonS3>();
var s3client3 = Configuration.GetAWSOptions("ClientConfig3").CreateServiceClient<IAmazonS3>();

services.AddImageflowS3Service(new S3ServiceOptions()
    .MapPrefix("/path1/", s3client1, "bucket1", "", false, false)
    .MapPrefix("/path2/", s3client2, "bucket2", "", false, false)
    .MapPrefix("/path3/", s3client3, "bucket3", "", false, false)
);
```

## Reference
https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-netcore.html