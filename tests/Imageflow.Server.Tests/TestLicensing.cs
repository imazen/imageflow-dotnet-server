using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Common.Licensing;
using Imazen.Common.Tests.Licensing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Imageflow.Server.Tests
{
    class RequestUrlProvider
    {
        public Uri Url { get; set; } = null;
        public Uri Get() => Url;
    }
    
    public class TestLicensing
    {

        public TestLicensing(ITestOutputHelper output) { this.output = output; }

        readonly ITestOutputHelper output;

        string GetInfo(ILicenseConfig c, LicenseManagerSingleton mgr)
        {
            var result = new Computation(c, mgr.TrustedKeys, mgr, mgr,
                mgr.Clock, true);
            var sb = new StringBuilder();
            
            sb.AppendLine($"Plugins.LicenseError = {c.LicenseEnforcement}");
            sb.AppendLine($"Plugins.LicenseScope = {c.LicenseScope}");
            sb.AppendLine($"Computation.");
            sb.AppendLine($"LicensedForAll() => {result.LicensedForAll()}");
            sb.AppendLine($"LicensedForSomething() => {result.LicensedForSomething()}");
            sb.AppendLine($"LicensedForRequestUrl(null) => {result.LicensedForRequestUrl(null)}");
            sb.AppendLine($"LicensedForRequestUrl(new Uri(\"http://other.com\")) => {result.LicensedForRequestUrl(new Uri("http://other.com"))}");
            sb.AppendLine($"LicensedForRequestUrl(new Uri(\"http://acme.com\")) => {result.LicensedForRequestUrl(new Uri("http://acme.com"))}");
            sb.AppendLine($"GetBuildDate() => {result.GetBuildDate()}");
            sb.AppendLine($"ProvideDiagnostics() => {result.ProvideDiagnostics()}");

            return sb.ToString();
        }
        
        internal Task<IHost> StartAsyncWithOptions(ImageflowMiddlewareOptions options)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    // Add TestServer
                    webHost.UseTestServer();
                    webHost.Configure(app =>
                    {
                        app.UseImageflow(options);
                    });
                });

            // Build and start the IHost
            return hostBuilder.StartAsync();
        }
        
        [Fact]
        public async void TestNoLicense()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var clock = new RealClock();
                var mgr = new LicenseManagerSingleton(ImazenPublicKeys.Test, clock, new StringCacheMem());
                var licensing = new Licensing(mgr);


                using var host = await StartAsyncWithOptions(new ImageflowMiddlewareOptions()
                    {
                        Licensing = licensing,
                        MyOpenSourceProjectUrl = null,
                        EnforcementMethod = EnforceLicenseWith.Http402Error
                    }
                    .SetDiagnosticsPageAccess(AccessDiagnosticsFrom.None)
                    .SetDiagnosticsPagePassword("pass")
                    .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                
                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();
                
                using var notLicensedResponse = await client.GetAsync("/fire.jpg?w=1");
                Assert.Equal(HttpStatusCode.PaymentRequired,notLicensedResponse.StatusCode);

                using var licensePageResponse = await client.GetAsync("/imageflow.license");
                licensePageResponse.EnsureSuccessStatusCode();
                
                using var notAuthorizedResponse = await client.GetAsync("/imageflow.debug");
                Assert.Equal(HttpStatusCode.Unauthorized,notAuthorizedResponse.StatusCode);
                
                using var debugPageResponse = await client.GetAsync("/imageflow.debug?password=pass");
                debugPageResponse.EnsureSuccessStatusCode();
                

                var page = licensing.Result.ProvidePublicLicensesPage();
                Assert.Contains("License Validation ON", page);
                Assert.Contains("No license keys found.", page);
                Assert.Contains("You must purchase a license key or comply with the AGPLv3.", page);
                Assert.Contains("To get a license key, visit", page);
                Assert.Contains("You are using EnforceLicenseWith.Http402Error", page);
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestAGPL()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var clock = new RealClock();
                var mgr = new LicenseManagerSingleton(ImazenPublicKeys.Test, clock, new StringCacheMem());
                var licensing = new Licensing(mgr);


                using var host = await StartAsyncWithOptions(new ImageflowMiddlewareOptions()
                {
                    Licensing = licensing,
                    MyOpenSourceProjectUrl = "https://github.com/username/project",
                    EnforcementMethod = EnforceLicenseWith.RedDotWatermark
                }.MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                
                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();
                
                using var licensedResponse = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse.EnsureSuccessStatusCode();

                var page = licensing.Result.ProvidePublicLicensesPage();
                Assert.Contains("License Validation OFF", page);
                Assert.Contains("No license keys found.", page);
                Assert.DoesNotContain("You must purchase a license key or comply with the AGPLv3.", page);
                Assert.DoesNotContain("To get a license key", page);
                Assert.Contains("You are using EnforceLicenseWith.RedDotWatermark", page);
                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestDomainsLicense()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var clock = new FakeClock("2017-04-25", "2017-04-25");
                var set = LicenseStrings.GetSets("PerCore2DomainsImageflow").First();
                var mgr = new LicenseManagerSingleton(ImazenPublicKeys.Test, clock, new StringCacheMem()); 
                MockHttpHelpers.MockRemoteLicense(mgr, HttpStatusCode.OK, set.Remote, null);
                var url = new RequestUrlProvider();
                var licensing = new Licensing(mgr, url.Get);
                
                using var host = await StartAsyncWithOptions(new ImageflowMiddlewareOptions()
                    {
                        Licensing = licensing,
                        MyOpenSourceProjectUrl = null
                    }
                    .SetLicenseKey(EnforceLicenseWith.Http402Error, 
                        set.Placeholder)
                    .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                
                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();
                
                mgr.WaitForTasks();

                Assert.Empty(mgr.GetIssues());
                
                url.Url = new Uri("https://unlicenseddomain.com");
                using var notLicensedResponse = await client.GetAsync("/fire.jpg?w=1");
                Assert.Equal(HttpStatusCode.PaymentRequired,notLicensedResponse.StatusCode);

                
                
                url.Url = new Uri("https://acme.com");
                using var licensedResponse1 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse1.EnsureSuccessStatusCode();

                url.Url = new Uri("https://acmestaging.com");
                using var licensedResponse2 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse2.EnsureSuccessStatusCode();
                
                url.Url = new Uri("https://subdomain.acme.com");
                using var licensedResponse3 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse3.EnsureSuccessStatusCode();
                
                Assert.Empty(mgr.GetIssues());
                
                var page = licensing.Result.ProvidePublicLicensesPage();
                Assert.Contains("License Validation ON", page);
                Assert.DoesNotContain("No license keys found.", page);
                Assert.Contains("License valid for 2 domains, missing for 1 domains", page);
                Assert.Contains("Your license needs to be upgraded", page);
                Assert.Contains("You are using EnforceLicenseWith.Http402Error", page);

                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestSiteLicense()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {

                var clock = new FakeClock("2017-04-25", "2017-04-25");
                var set = LicenseStrings.GetSets("SiteWideImageflow").First();
                var mgr = new LicenseManagerSingleton(ImazenPublicKeys.Test, clock, new StringCacheMem()); 
                MockHttpHelpers.MockRemoteLicense(mgr, HttpStatusCode.OK, set.Remote, null);
                var url = new RequestUrlProvider();
                var licensing = new Licensing(mgr, url.Get);
                
                using var host = await StartAsyncWithOptions(new ImageflowMiddlewareOptions()
                    {
                        Licensing = licensing,
                        MyOpenSourceProjectUrl = null
                    }
                    .SetLicenseKey(EnforceLicenseWith.Http402Error, 
                        set.Placeholder)
                    .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));
                
                // Create an HttpClient to send requests to the TestServer
                using var client = host.GetTestClient();
                
                mgr.WaitForTasks();

                Assert.Empty(mgr.GetIssues());


                url.Url = new Uri("https://acme.com");
                using var licensedResponse1 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse1.EnsureSuccessStatusCode();

                url.Url = new Uri("https://acmestaging.com");
                using var licensedResponse2 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse2.EnsureSuccessStatusCode();
                
                url.Url = new Uri("https://subdomain.acme.com");
                using var licensedResponse3 = await client.GetAsync("/fire.jpg?w=1");
                licensedResponse3.EnsureSuccessStatusCode();
                
                Assert.Empty(mgr.GetIssues());
                
                var page = licensing.Result.ProvidePublicLicensesPage();
                Assert.Contains("License Validation ON", page);
                Assert.Contains("License key valid for all domains.", page);
                Assert.Contains("No resale of usage. Only for organizations with less than 500 employees.", page);
                Assert.Contains("You are using EnforceLicenseWith.Http402Error", page);
                Assert.Contains("Manage your subscription at https://account.imazen.io", page);

                
                await host.StopAsync(CancellationToken.None);
            }
        }
        
        [Fact]
        public async void TestRevocations()
        {
            using (var contentRoot = new TempContentRoot()
                .AddResource("images/fire.jpg", "TestFiles.fire-umbrella-small.jpg"))
            {
                // set clock to present, and build date to far future
                var clock = new FakeClock("2017-04-25", "2022-01-01");

                foreach (var set in LicenseStrings.GetSets("CancelledImageflow", "SoftRevocationImageflow",
                    "HardRevocationImageflow"))
                {

                    output.WriteLine($"Testing revocation for {set.Name}");
                    var mgr = new LicenseManagerSingleton(ImazenPublicKeys.Test, clock, new StringCacheMem());
                    MockHttpHelpers.MockRemoteLicense(mgr, HttpStatusCode.OK, set.Remote, null);

                    var url = new RequestUrlProvider();
                    var licensing = new Licensing(mgr, url.Get);

                    using var host = await StartAsyncWithOptions(new ImageflowMiddlewareOptions()
                        {
                            Licensing = licensing,
                            MyOpenSourceProjectUrl = null
                        }
                        .SetLicenseKey(EnforceLicenseWith.Http402Error,
                            set.Placeholder)
                        .MapPath("/", Path.Combine(contentRoot.PhysicalPath, "images")));

                    // Create an HttpClient to send requests to the TestServer
                    using var client = host.GetTestClient();

                    mgr.WaitForTasks();

                    
                    url.Url = new Uri("https://domain.com");
                    using var notLicensedResponse = await client.GetAsync("/fire.jpg?w=1");
                    Assert.Equal(HttpStatusCode.PaymentRequired,notLicensedResponse.StatusCode);

                    url.Url = null;
                    using var notLicensedResponse2 = await client.GetAsync("/fire.jpg?w=1");
                    Assert.Equal(HttpStatusCode.PaymentRequired,notLicensedResponse2.StatusCode);

                    
                    Assert.NotEmpty(mgr.GetIssues());
                    
                    var page = licensing.Result.ProvidePublicLicensesPage();
                    Assert.Contains("License Validation ON", page);
                    Assert.Contains("You are using EnforceLicenseWith.Http402Error", page);
                    Assert.Contains("No valid license keys found.", page);

                    Assert.Contains(
                        "Your license is invalid. Please renew your license via the management portal or purchase a new one at",
                        page);
                    Assert.DoesNotContain("Your license needs to be upgraded.", page);

                    if (set.Name == "CancelledImageflow")
                    {
                        Assert.Contains("Your subscription has lapsed; please renew to continue using product.", page);
                    }

                    if (set.Name == "SoftRevocationImageflow")
                    {
                        Assert.Contains(
                            "This license has been compromised; please contact Vendor Gamma for an updated license",
                            page);
                    }
                    if (set.Name == "HardRevocationImageflow")
                    {
                        Assert.Contains(
                            "Please contact support; the license was shared with an unauthorized party and has been revoked.",
                            page);
                    }

                    

                    await host.StopAsync(CancellationToken.None);
                }
            }
        }

    }
}