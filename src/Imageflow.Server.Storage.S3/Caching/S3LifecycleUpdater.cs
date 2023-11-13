
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imageflow.Server.Storage.S3.Caching
{
    internal class S3LifecycleUpdater
    {

        private readonly ILogger<S3Service> logger;
        public S3LifecycleUpdater(NamedCacheConfiguration config, IAmazonS3 defaultClient, ILogger<S3Service> logger){
            this.config = config;
            this.logger = logger;
            this.defaultClient = defaultClient;
        }
        private NamedCacheConfiguration config;

        private IAmazonS3 defaultClient;
        internal async Task CreateBucketsAsync(){
            var buckets = config.BlobGroupConfigurations.Values.ToList();
            // Filter out those with the same bucket name
            var distinctBuckets = buckets.GroupBy(v => v.Location.BucketName).Select(v => v.First()).ToList();

            // await all simultaneous requests
            await Task.WhenAll(distinctBuckets.Select(async (v) => {
                try{
                    var client = v.Location.S3Client ?? defaultClient;
                    await client.DoesS3BucketExistAsync(v.Location.BucketName).ContinueWith(async (task) => {
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
    }
}

