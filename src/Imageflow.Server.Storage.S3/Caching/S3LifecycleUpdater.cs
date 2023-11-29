
using System;
using Amazon.S3;
using Amazon.S3.Model;
using Imazen.Common.Concurrency;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Imazen.Abstractions.Logging;
using Imazen.Abstractions.Resulting;

namespace Imageflow.Server.Storage.S3.Caching
{
    internal class S3LifecycleUpdater
    {

        private readonly IReLogger logger;
        public S3LifecycleUpdater(NamedCacheConfiguration config, IAmazonS3 defaultClient, IReLogger logger){
            this.config = config;
            this.logger = logger.WithSubcategory(nameof(S3LifecycleUpdater));
            this.defaultClient = defaultClient;
        }
        private NamedCacheConfiguration config;

        private IAmazonS3 defaultClient;


        private BasicAsyncLock updateLock = new BasicAsyncLock();
        private bool updateComplete = false;
        internal async Task UpdateIfIncompleteAsync(){
            if (updateComplete){
                return;
            }
            using (var unused = await updateLock.LockAsync())
            {
                if (updateComplete){
                    return;
                }
                await CreateBucketsAsync();
                await UpdateLifecycleRulesAsync();
                await CreateAndReadTestFilesAsync(false);
                updateComplete = true;
            }
        }
        internal async Task CreateBucketsAsync(){
            var buckets = config.BlobGroupConfigurations.Values.ToList();
            // Filter out those with the same bucket name
            var distinctBuckets = buckets.GroupBy(v => v.Location.BucketName).Select(v => v.First()).ToList();

            // await all simultaneous requests
            await Task.WhenAll(distinctBuckets.Select(async (v) => {
                try{
                    if (v.CreateBucketIfMissing == false){
                        return;
                    }
                    var client = v.Location.S3Client ?? defaultClient;
                    
                    await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, v.Location.BucketName)
                        .ContinueWith(async (task) => {
                        if (!task.Result){
                            // log missing
                            logger.LogInformation($"S3 Bucket {v.Location.BucketName} does not exist. Creating...");
                            var putBucketRequest = new PutBucketRequest
                            {
                                BucketName = v.Location.BucketName,
                            };
                            try{
                            await client.PutBucketAsync(putBucketRequest);
                                // log created
                                logger.LogInformation($"S3 Bucket {v.Location.BucketName} created.");
                            } catch (AmazonS3Exception e){
                                logger.LogError(e, $"Error creating S3 bucket {v.Location.BucketName}: {e.Message}");
                            }
                        }
                    });
                } catch (AmazonS3Exception e){
                    logger.LogError(e, $"Error checking S3 bucket exists {v.Location.BucketName}: {e.Message}");
                }
            }));
        }

        
        internal async Task UpdateLifecycleRulesAsync(){
            // https://docs.aws.amazon.com/AmazonS3/latest/userguide/how-to-set-lifecycle-configuration-intro.html

            var buckets = config.BlobGroupConfigurations.Values.ToList();
            // Filter out those with the same bucket name
            var bucketGroups = buckets.GroupBy(v => v.Location.BucketName).ToList();

            // await all simultaneous requests
            await Task.WhenAll(bucketGroups.Select(async (group) => {
                if (group.First().UpdateLifecycleRules == false){
                    return;
                }
                var client = group.First().Location.S3Client ?? defaultClient;
                // Fetch the lifecycle rules
                GetLifecycleConfigurationResponse lifecycleConfigurationResponse;
                try{
                    lifecycleConfigurationResponse = await client.GetLifecycleConfigurationAsync(new GetLifecycleConfigurationRequest
                        {
                            BucketName = group.First().Location.BucketName
                        });
                }catch(AmazonS3Exception e){
                    logger.LogError(e, $"Error fetching lifecycle configuration for bucket {group.First().Location.BucketName}: {e.Message}");
                    return;
                }
                var rules = lifecycleConfigurationResponse.Configuration.Rules ?? new List<LifecycleRule>();
                var originalCopy = rules.ToList();
                // remove rules with a predicate that matches the prefix but don't match the configuration
                rules.RemoveAll((rule) => {
                    if (rule.Filter == null || rule.Filter.LifecycleFilterPredicate == null){
                        return false;
                    }
                    if (rule.Filter.LifecycleFilterPredicate is LifecyclePrefixPredicate prefixPredicate){
                        return group.Any((v) => v.Location.BlobPrefix == prefixPredicate.Prefix);
                    }
                    return false;
                });
                // Add the new rules
                foreach(var groupItem in group){
                    if (groupItem.Lifecycle.DaysBeforeExpiry == null){
                        continue;
                    }
                    rules.Add(new LifecycleRule
                    {
                        Id = $"Imageflow-Cache-Expiry-{groupItem.Location.BlobPrefix}-Days-{groupItem.Lifecycle.DaysBeforeExpiry}",
                        Filter = new LifecycleFilter()
                        {
                            LifecycleFilterPredicate = new LifecyclePrefixPredicate()
                            {
                                Prefix = groupItem.Location.BlobPrefix
                            }
                        },
                        Status = LifecycleRuleStatus.Enabled,
                        Expiration = new LifecycleRuleExpiration()
                        {
                            Days = groupItem.Lifecycle.DaysBeforeExpiry.Value
                        }
                    });
                }
                // Now compare the set of rule IDs that are enabled. Since we encode the config in the rule ID we can just compare the rule IDs
                var originalRuleIds = originalCopy.Where((v) => v.Status == LifecycleRuleStatus.Enabled).Select((v) => v.Id).OrderBy((v) => v).ToList();
                var newRuleIds = rules.Where((v) => v.Status == LifecycleRuleStatus.Enabled).Select((v) => v.Id).OrderBy((v) => v).ToList();
                if (originalRuleIds.SequenceEqual(newRuleIds)){
                    return; //Nothing to change
                }
                // Update the lifecycle rules
                var putLifecycleConfigurationRequest = new PutLifecycleConfigurationRequest
                {
                    BucketName = group.First().Location.BucketName,
                    Configuration = new LifecycleConfiguration
                    {
                        Rules = rules
                    }
                };
            }));
        }

        internal record class TestFilesResult( List<CodeResult> Results, bool ReadsFailed, bool WritesFailed, bool ListFailed, bool DeleteFailed);
        /// <summary>
        /// Returns all errors encountered
        /// </summary>
        /// <param name="forceAll"></param>
        /// <returns></returns>
        internal async Task<TestFilesResult> CreateAndReadTestFilesAsync(bool forceAll)
        {
            var buckets = config.BlobGroupConfigurations.Values.ToList();
            // Filter out those with the same bucket name
            var bucketGroups = buckets.GroupBy(v => v.Location.BucketName).ToList();
            
            var results = new List<CodeResult>();
            bool readsFailed = false;
            bool writesFailed = false;
            bool listFailed = false;
            bool deleteFailed = false;
            // await all simultaneous requests
            await Task.WhenAll(config.BlobGroupConfigurations.Values.Select(async (blobGroupConfig) =>
            {
                if (!forceAll && blobGroupConfig.TestBucketCapabilities == false)
                {
                    return;
                }
                var client = blobGroupConfig.Location.S3Client ?? defaultClient;
                var bucket = blobGroupConfig.Location.BucketName;
                var key = blobGroupConfig.Location.BlobPrefix + "/empty-healthcheck-file";
                
                var putResult = await TryS3OperationAsync(bucket, key, "Put", async () =>
                {
                    var putObjectRequest = new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = key,
                        ContentBody = ""
                    };
                    return await client.PutObjectAsync(putObjectRequest);
                });
                if (putResult.IsError)
                {
                    writesFailed = true;
                    results.Add(putResult);
                }
                // now try listing
                var listResult = await TryS3OperationAsync(bucket, key, "List", async () =>
                    await client.ListObjectsAsync(new ListObjectsRequest
                    {
                        BucketName = bucket,
                        Prefix = key
                    }));
                if (listResult.IsError){
                    listFailed = true;
                    results.Add(listResult);
                }
                // now try reading
                var getResult = await TryS3OperationAsync(bucket, key, "Get", async () =>
                    await client.GetObjectAsync(new GetObjectRequest
                    {
                        BucketName = bucket,
                        Key = key
                    }));
                if (getResult.IsError){
                    readsFailed = true;
                    results.Add(getResult);
                }
                
                var key2 = blobGroupConfig.Location.BlobPrefix + "/empty-healthcheck-file2";
                
                var putResult2 = await TryS3OperationAsync(bucket, key2, "Put", async () =>
                {
                    var putObjectRequest = new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = key2,
                        ContentBody = ""
                    };
                    return await client.PutObjectAsync(putObjectRequest);
                });
                // now try deleting
                var deleteResult = await TryS3OperationAsync(bucket, key, "Delete", async () =>
                    await client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = bucket,
                        Key = key
                    }));
                if (deleteResult.IsError){
                    deleteFailed = true;
                    results.Add(deleteResult);
                }
                    
            }));
            return new TestFilesResult(results, readsFailed, writesFailed, listFailed, deleteFailed);
        }

        private async Task<CodeResult> TryS3OperationAsync(string bucketName, string key, string operationName,
            Func<Task<AmazonWebServiceResponse>> operation)
        {
            try
            {
                var response = await operation();
                if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    var err = CodeResult.Err(
                        new HttpStatus(response.HttpStatusCode).WithAddFrom($"S3 {operationName} {bucketName} {key}"));
                    logger.LogAsError(err);
                    return err;
                }

                return CodeResult.Ok();
            }
            catch (AmazonS3Exception e)
            {
                var err = CodeResult.FromException(e, $"S3 {operationName} {bucketName} {key}");
                logger.LogAsError(err);
                return err;
            }
        }

    }
}

