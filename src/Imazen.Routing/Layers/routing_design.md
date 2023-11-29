# Routing Design

For fallback, we want to maintain compatibility with the C# rewrite/auth event handlers, watermarking, and traditional 
blob providers that specify their prefixes, can throw exceptions, and work based on just the virtual path.


Ideally we want a config that can be evaluated by a TypeScript, Rust, or C# library. Edge functions typically only support TypeScript/Rust, although we wouldn't be doing image processing at edge, we would want to handle caching and routing. Or, we allow the C# configuration to generate TypeScript/Javascript code that can be deployed to the edge.


Conditions and Actions

Conditions can be nested, and have a parent condition.

For example, a condition might be:

* Filter by the host and/or port value
* Filter by the path extension
* Filter by the querystring version number or something
* Filter by keys/values in the ParsedData dictionary (this contains named groups captured as well as those set by actions directily)


Routes have unique identifiers. This allows them to be referenced by other routes and be used symmetrically for url generation.

Routes have a parent route. This allows them to be nested.
Routes can rewrite the path, querystring, host, or headers. This can be used to set a base URL for a group of routes. 
For multi-tenanting, this can be especially useful. 

Conditions can be grouped into a condition group. This allows them to be combined with AND or OR logic.
For example, a condition group might be:
* Extension is Imageflow Supported for reading
* OR there is no extension. 

Route patterns can be described with Regex (the fallback), or using a custom pattern language.

Named groups are the core of route patterns. A named group will match all characters until a character matches the one that follows the named group. For example, the pattern:

*    `/s3/{container}/{key}` does not allow containers to contain '/', but any other character is allowed.

Routes can specify 'where' clauses on the named groups. For example
* where {container} matches [a-z0-9] AND ({key} has supported image extenstion) or null.

Route definitions can be described in C# data structures, in TOML, or in JSON/MessagePack/MemoryPack. 

Actions can adjust paths, inserting prefixes, removing prefixes, suffixes, overriding querystring values or setting them.
It can also scale numerical values with a from-range and to-range, with underflow and overflow-handling defined. 

A priority is fast startup (for action use), but also fast evaluation. MessagePack

# To consider

How to support remote reader signing, full url signing, and multi-tenanting support with different key sets. Add 
support for seeds?  And pub/private key support would allow untrusted edge functions to verify signatures.

Minimizing utf32 overhead, perhaps keep utf8 version of string types? Or make the data type framework-conditional, with overloads so 
strings can always be specified when using the C# interface?

It can also execute static C# methods that are fully qualified. TODO: figure out how to manage this with AOT.

Extending to support for POST/PUT and multipart uploads?


