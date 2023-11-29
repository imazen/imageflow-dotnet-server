
Scenarios

Routing to promise
Browser -> IServer -> Disk (returns promise)

Promise ?Pipeline?

DiskPromise -> Caching -> Imaging -> Caching -> IServer (Etag/304)

But what if caching is blob caching? source caching wouldn't make sense.
What if we are using Azure Functions or AWS Lambda?



Design task:
Preview editions of results, for long-running jobs. Say an AI upscale is requested, but it takes 10 minutes.

Figure out how response HTTP headers and browser caching hints are determined - does routing? another layer?
Promise-based authentication?
Extracting data from a request in the most organized way?
- Azure, AWS, OpenWhisk serverless support?

JSON Job endpoint
- pre-signed HTTPS GET and PUT URLs
- If there's just one output, can respond with it, depending on timeout.
- Can execute a callback on completion instead of waiting for response.
- cached license file (for azure fn)
- Can run an Imageflow job, a super-crunch, salience analysis, bonus format conversion, or a different backend job, or AI (replicate?) 

