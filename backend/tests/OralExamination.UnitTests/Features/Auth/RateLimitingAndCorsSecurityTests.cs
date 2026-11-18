using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OralExamination.API.Configuration;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class RateLimitingAndCorsSecurityTests
{
    private static TestServer CreateRateLimiterTestServer(Dictionary<string, string?>? additionalConfig = null)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Security:Abuse:LoginPermits"] = "15",
            ["Security:Abuse:AuthLoginPolicyPermits"] = "15",
            ["Security:Abuse:RefreshPermits"] = "30",
            ["Security:Abuse:SpeechPermits"] = "30",
            ["Security:Abuse:UploadPermits"] = "20",
            ["Security:Abuse:AiPermits"] = "15",
            ["Security:Abuse:SubmitPermits"] = "60",
            ["Security:Abuse:GeneralPermits"] = "600",
            ["Security:Abuse:WindowSeconds"] = "60",
            ["Security:Abuse:MaxUploadBytes"] = "27262976",
            ["Security:Abuse:ExpensiveRequestSeconds"] = "90"
        };
        if (additionalConfig != null)
        {
            foreach (var kvp in additionalConfig)
            {
                configData[kvp.Key] = kvp.Value;
            }
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddApiAbuseProtection(configuration);
                services.AddOralExamRateLimiting(configuration);
            })
            .Configure(app =>
            {
                app.Use((context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Simulated-Remote-Ip", out var simulatedIp) &&
                        IPAddress.TryParse(simulatedIp, out var parsedIp))
                    {
                        context.Connection.RemoteIpAddress = parsedIp;
                    }
                    else if (context.Connection.RemoteIpAddress == null)
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Loopback;
                    }
                    return next(context);
                });

                // TSYNC-F001: Dùng options từ DI được AddApiAbuseProtection đăng ký, không Clear KnownNetworks/KnownProxies
                app.UseForwardedHeaders();

                app.UseRouting();
                app.UseRateLimiter();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/auth/test-login", () => "OK")
                        .RequireRateLimiting(OralExamRateLimiterExtensions.LoginPolicy);
                    endpoints.MapGet("/auth/test-refresh", () => "OK")
                        .RequireRateLimiting(OralExamRateLimiterExtensions.RefreshPolicy);
                    endpoints.MapPost("/api/v1/auth/google-challenge", () => "OK")
                        .RequireRateLimiting(OralExamRateLimiterExtensions.LoginPolicy);
                    endpoints.MapPost("/api/v1/auth/google-login", () => "OK")
                        .RequireRateLimiting(OralExamRateLimiterExtensions.LoginPolicy);
                    endpoints.MapGet("/auth/test-ip", (HttpContext ctx) => OralExamRateLimiterExtensions.GetClientIp(ctx));
                });
            });

        return new TestServer(builder);
    }

    [Fact(DisplayName = "T3-F007: GetClientIp lấy IP từ Connection.RemoteIpAddress và tuyệt đối không tin cậy trực tiếp header client")]
    public void RateLimiter_GetClientIp_UsesConnectionRemoteIp_AndIgnoresUntrustedXForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.55");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 5.6.7.8";

        var clientIp = OralExamRateLimiterExtensions.GetClientIp(context);

        // Client IP phải là 198.51.100.55 từ Connection, không được phép là 1.2.3.4 từ header client
        clientIp.Should().Be("198.51.100.55");
        clientIp.Should().NotBe("1.2.3.4");
    }

    [Fact(DisplayName = "T3-F007 & T3-F011: Rate Limiter auth-login từ production registration giới hạn 15 req/phút")]
    public async Task RateLimiter_LoginPolicy_PermitLimit15_EnforcedViaProductionRegistration()
    {
        using var server = CreateRateLimiterTestServer();
        var client = server.CreateClient();

        for (int i = 0; i < 15; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
            req.Headers.Add("X-Forwarded-For", "198.51.100.10");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Lần gọi login thứ {i + 1} phải thành công");
        }

        var exhaustedReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
        exhaustedReq.Headers.Add("X-Forwarded-For", "198.51.100.10");
        var exhaustedResp = await client.SendAsync(exhaustedReq);

        exhaustedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "Lần thứ 16 phải bị rate-limit 429");
        exhaustedResp.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact(DisplayName = "T3-F007 & T3-F011: Rate Limiter auth-refresh từ production registration giới hạn 30 req/phút")]
    public async Task RateLimiter_RefreshPolicy_PermitLimit30_EnforcedViaProductionRegistration()
    {
        using var server = CreateRateLimiterTestServer();
        var client = server.CreateClient();

        for (int i = 0; i < 30; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-refresh");
            req.Headers.Add("X-Forwarded-For", "198.51.100.20");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Lần gọi refresh thứ {i + 1} phải thành công");
        }

        var exhaustedReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-refresh");
        exhaustedReq.Headers.Add("X-Forwarded-For", "198.51.100.20");
        var exhaustedResp = await client.SendAsync(exhaustedReq);

        exhaustedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "Lần thứ 31 phải bị rate-limit 429");
        exhaustedResp.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact(DisplayName = "T3-F007: Kẻ tấn công đổi X-Forwarded-For qua Nginx vẫn dùng chung bucket và bị rate-limit")]
    public async Task RateLimiter_SpoofedXForwardedFor_SharesSameBucket_WhenSameRealClientIp()
    {
        using var server = CreateRateLimiterTestServer();
        var client = server.CreateClient();

        // 15 request với các spoofed IP khác nhau ở đầu chuỗi do client tự gửi, nhưng real IP 198.51.100.99 ở cuối chuỗi do Nginx thêm
        for (int i = 0; i < 15; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
            req.Headers.Add("X-Forwarded-For", $"10.0.0.{i + 1}, 198.51.100.99");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Lần thứ 16 với một spoofed IP hoàn toàn mới -> VẪN BỊ CHẶN 429 vì ForwardLimit=1 chỉ bóc real IP cuối cùng
        var attackReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
        attackReq.Headers.Add("X-Forwarded-For", "99.99.99.99, 198.51.100.99");
        var attackResp = await client.SendAsync(attackReq);

        attackResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "Kẻ tấn công đổi X-Forwarded-For không được cấp bucket mới và phải bị từ chối 429");
    }

    [Fact(DisplayName = "T3-F007: Hai Client với RemoteIpAddress khác nhau có hạn ngạch bucket độc lập")]
    public async Task RateLimiter_DifferentRemoteIps_HaveIndependentBuckets()
    {
        using var server = CreateRateLimiterTestServer();
        var client = server.CreateClient();

        // Client A gọi hết 15 lần
        for (int i = 0; i < 15; i++)
        {
            var reqA = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
            reqA.Headers.Add("X-Forwarded-For", "198.51.100.1");
            var respA = await client.SendAsync(reqA);
            respA.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Lần thứ 16 của Client A bị chặn
        var reqAExhausted = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
        reqAExhausted.Headers.Add("X-Forwarded-For", "198.51.100.1");
        var respAExhausted = await client.SendAsync(reqAExhausted);
        respAExhausted.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Client B gọi lần đầu -> vẫn thành công vì bucket độc lập
        var reqB = new HttpRequestMessage(HttpMethod.Get, "/auth/test-login");
        reqB.Headers.Add("X-Forwarded-For", "198.51.100.2");
        var respB = await client.SendAsync(reqB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "Client B có IP khác nên không bị ảnh hưởng bởi Client A");
    }

    [Fact(DisplayName = "Rate Limiter: OnRejected thiết lập Header Retry-After và chuẩn ProblemDetails RFC 7807 từ production registration")]
    public async Task RateLimiter_OnRejected_SetsRetryAfterHeader_And_Rfc7807ProblemDetails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOralExamRateLimiting();

        var provider = services.BuildServiceProvider();
        var rateLimiterOptions = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var leaseMock = new Mock<RateLimitLease>();
        leaseMock.Setup(l => l.IsAcquired).Returns(false);

        var onRejectedContext = new OnRejectedContext
        {
            HttpContext = httpContext,
            Lease = leaseMock.Object
        };

        await rateLimiterOptions.OnRejected!(onRejectedContext, default);

        httpContext.Response.ContentType.Should().Contain("application/problem+json");
        httpContext.Response.Headers.RetryAfter.ToString().Should().Be("60");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("https://tools.ietf.org/html/rfc7807");
        body.Should().Contain("429");
    }

    [Fact(DisplayName = "TSYNC-F001: X-Forwarded-For từ remote IP không nằm trong allowlist bị bỏ qua")]
    public async Task TSYNC_F001_UntrustedRemoteIp_IgnoresXForwardedFor_AndDoesNotSpoofClientIp()
    {
        var additionalConfig = new Dictionary<string, string?>
        {
            ["Security:TrustedProxies:0"] = "10.0.0.50"
        };
        using var server = CreateRateLimiterTestServer(additionalConfig);
        var client = server.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        req.Headers.Add("X-Simulated-Remote-Ip", "192.0.2.100");
        req.Headers.Add("X-Forwarded-For", "203.0.113.195");

        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var clientIp = await resp.Content.ReadAsStringAsync();

        clientIp.Should().Be("192.0.2.100");
        clientIp.Should().NotBe("203.0.113.195");
    }

    [Fact(DisplayName = "TSYNC-F001: X-Forwarded-For qua đúng trusted proxy trong allowlist được xử lý")]
    public async Task TSYNC_F001_TrustedProxy_ProcessesXForwardedFor_AndResolvesClientIp()
    {
        var additionalConfig = new Dictionary<string, string?>
        {
            ["Security:TrustedProxies:0"] = "10.0.0.50"
        };
        using var server = CreateRateLimiterTestServer(additionalConfig);
        var client = server.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        req.Headers.Add("X-Simulated-Remote-Ip", "10.0.0.50");
        req.Headers.Add("X-Forwarded-For", "203.0.113.195");

        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var clientIp = await resp.Content.ReadAsStringAsync();

        clientIp.Should().Be("203.0.113.195");
    }

    [Fact(DisplayName = "TSYNC-F001: Trusted proxy với chuỗi IP giả mạo tuân thủ ForwardLimit=1 chỉ lấy hop cuối cùng")]
    public async Task TSYNC_F001_TrustedProxy_WithSpoofedChain_HonorsForwardLimit1()
    {
        var additionalConfig = new Dictionary<string, string?>
        {
            ["Security:TrustedProxies:0"] = "10.0.0.50"
        };
        using var server = CreateRateLimiterTestServer(additionalConfig);
        var client = server.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        req.Headers.Add("X-Simulated-Remote-Ip", "10.0.0.50");
        req.Headers.Add("X-Forwarded-For", "1.1.1.1, 203.0.113.195");

        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var clientIp = await resp.Content.ReadAsStringAsync();

        clientIp.Should().Be("203.0.113.195");
        clientIp.Should().NotBe("1.1.1.1");
    }

    [Fact(DisplayName = "T3-F011: CORS production thiếu allowlist bắt buộc phải fail startup (ném InvalidOperationException)")]
    public void Cors_Production_MissingAllowlist_ThrowsInvalidOperationException()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        var act = () =>
        {
            services.AddOralExamCors(config, mockEnv.Object);
            var sp = services.BuildServiceProvider();
            _ = sp.GetRequiredService<IOptions<CorsOptions>>().Value;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CORS_ALLOWED_ORIGINS*");
    }

    [Fact(DisplayName = "T3-F011: CORS production có cấu hình nguồn gốc tin cậy thiết lập WithOrigins chính xác từ extension")]
    public void Cors_Production_WithConfiguredOrigins_SetsAllowlist()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CORS_ALLOWED_ORIGINS"] = "https://oralexam.io.vn,https://app.oralexam.io.vn"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOralExamCors(config, mockEnv.Object);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = corsOptions.GetPolicy(OralExamCorsExtensions.PolicyName)!;

        policy.Should().NotBeNull();
        policy.Origins.Should().Contain("https://oralexam.io.vn");
        policy.Origins.Should().Contain("https://app.oralexam.io.vn");
        policy.Origins.Should().NotContain("*");
        policy.SupportsCredentials.Should().BeTrue();
    }

    [Fact(DisplayName = "T3-F011: CORS development fallback chỉ cho phép localhost, không dùng wildcard")]
    public void Cors_Development_Fallback_AllowsLocalhost_NeverWildcard()
    {
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddOralExamCors(config, mockEnv.Object);

        var provider = services.BuildServiceProvider();
        var corsOptions = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = corsOptions.GetPolicy(OralExamCorsExtensions.PolicyName)!;

        policy.Should().NotBeNull();
        policy.Origins.Should().Contain("http://localhost:3000");
        policy.Origins.Should().Contain("http://localhost:5173");
        policy.Origins.Should().NotContain("*");
        policy.Origins.Should().NotContain("https://evil.com");
        policy.SupportsCredentials.Should().BeTrue();
    }

    [Fact(DisplayName = "TASK-THANH-01: Security:TrustedNetworks hỗ trợ chuỗi nhiều CIDR phân tách dấu phẩy và giữ ForwardLimit=1")]
    public async Task TrustedNetworks_CommaSeparatedCidrs_ProcessesXForwardedFor_AndHonorsForwardLimit1()
    {
        var additionalConfig = new Dictionary<string, string?>
        {
            ["Security:TrustedNetworks:0"] = "172.18.0.5/16,192.168.0.0/16"
        };
        using var server = CreateRateLimiterTestServer(additionalConfig);
        var client = server.CreateClient();

        // Container Nginx nằm trong dải 172.18.0.0/16 -> X-Forwarded-For được xử lý nhưng chỉ lấy hop cuối cùng
        var trustedReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        trustedReq.Headers.Add("X-Simulated-Remote-Ip", "172.18.4.9");
        trustedReq.Headers.Add("X-Forwarded-For", "8.8.8.8, 203.0.113.77");
        var trustedResp = await client.SendAsync(trustedReq);
        trustedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await trustedResp.Content.ReadAsStringAsync()).Should().Be("203.0.113.77");

        // Container nằm trong dải thứ hai 192.168.0.0/16 -> cũng được xử lý an toàn với ForwardLimit=1
        var secondPoolReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        secondPoolReq.Headers.Add("X-Simulated-Remote-Ip", "192.168.32.4");
        secondPoolReq.Headers.Add("X-Forwarded-For", "9.9.9.9, 198.51.100.88");
        var secondPoolResp = await client.SendAsync(secondPoolReq);
        secondPoolResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondPoolResp.Content.ReadAsStringAsync()).Should().Be("198.51.100.88");

        // Peer nằm ngoài cả hai dải -> X-Forwarded-For bị bỏ qua hoàn toàn
        var untrustedReq = new HttpRequestMessage(HttpMethod.Get, "/auth/test-ip");
        untrustedReq.Headers.Add("X-Simulated-Remote-Ip", "172.19.0.2");
        untrustedReq.Headers.Add("X-Forwarded-For", "203.0.113.77");
        var untrustedResp = await client.SendAsync(untrustedReq);
        untrustedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await untrustedResp.Content.ReadAsStringAsync()).Should().Be("172.19.0.2");
    }

    [Fact(DisplayName = "TASK-THANH-01: 120 SV ẩn danh cùng Campus NAT thực hiện Google OAuth 2 bước (240 requests) với cấu hình Production 360 permits đều đạt 200 OK")]
    public async Task ProductionBudget_120StudentsTwoStepGoogleOAuthOnSameCampusNat_All240RequestsSucceed()
    {
        var prodBudget = new Dictionary<string, string?>
        {
            ["Security:TrustedNetworks:0"] = "172.16.0.0/12,192.168.0.0/16",
            ["Security:Abuse:LoginPermits"] = "360",
            ["Security:Abuse:AuthLoginPolicyPermits"] = "360"
        };
        using var server = CreateRateLimiterTestServer(prodBudget);
        var client = server.CreateClient();

        for (var student = 0; student < 120; student++)
        {
            using var challengeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/google-challenge");
            challengeReq.Headers.Add("X-Simulated-Remote-Ip", "172.18.0.5");
            challengeReq.Headers.Add("X-Forwarded-For", "118.69.10.20");
            var challengeResp = await client.SendAsync(challengeReq);
            challengeResp.StatusCode.Should().Be(HttpStatusCode.OK, $"SV #{student + 1} google-challenge phải đạt 200 OK");

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/google-login");
            loginReq.Headers.Add("X-Simulated-Remote-Ip", "172.18.0.5");
            loginReq.Headers.Add("X-Forwarded-For", "118.69.10.20");
            var loginResp = await client.SendAsync(loginReq);
            loginResp.StatusCode.Should().Be(HttpStatusCode.OK, $"SV #{student + 1} google-login phải đạt 200 OK");
        }
    }

    [Theory(DisplayName = "TASK-THANH-01: Cấu hình TrustedNetworks (bao gồm wildcard /0) hoặc TrustedProxies sai định dạng phải fail-closed ngay khi khởi tạo")]
    [InlineData("Security:TrustedNetworks:0", "0.0.0.0/0")]
    [InlineData("Security:TrustedNetworks:0", "::/0")]
    [InlineData("Security:TrustedNetworks:0", "172.18.0.0/99")]
    [InlineData("Security:TrustedNetworks:0", "not-a-cidr")]
    [InlineData("Security:TrustedProxies:0", "invalid-ip")]
    public void ForwardedHeaders_InvalidTrustedProxyOrNetwork_ThrowsInvalidOperationException(string key, string badValue)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = badValue })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiAbuseProtection(config);
        var provider = services.BuildServiceProvider();

        var act = () => _ = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "TASK-THANH-01: appsettings.Production.json khóa chặt AllowedHosts cho domain oralexam.io.vn và internal healthcheck")]
    public void ProductionAppSettings_ConfiguresExplicitAllowedHosts_NeverWildcard()
    {
        var apiDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API"));
        var prodSettingsPath = Path.Combine(apiDir, "appsettings.Production.json");
        File.Exists(prodSettingsPath).Should().BeTrue();

        var prodConfig = new ConfigurationBuilder()
            .AddJsonFile(prodSettingsPath, optional: false)
            .Build();

        var allowedHosts = prodConfig["AllowedHosts"];
        allowedHosts.Should().NotBeNullOrWhiteSpace();
        allowedHosts.Should().NotBe("*");

        var hosts = allowedHosts!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        hosts.Should().Contain(["oralexam.io.vn", "www.oralexam.io.vn", "localhost", "127.0.0.1", "backend", "web-api"]);
        hosts.Should().NotContain("*");
    }

    [Fact(DisplayName = "M4-Item10: AbuseProtectionOptions.HubNegotiatePermits mặc định là 360 và BindAndValidateOptions kiểm tra hợp lệ")]
    public void AbuseProtection_HubNegotiatePermits_DefaultsTo360_AndValidatesPositiveBounds()
    {
        var defaultOptions = new AbuseProtectionOptions();
        defaultOptions.HubNegotiatePermits.Should().Be(360);

        var boundDefault = AbuseProtection.BindAndValidateOptions(new ConfigurationBuilder().Build());
        boundDefault.HubNegotiatePermits.Should().Be(360);

        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Abuse:HubNegotiatePermits"] = "500"
            })
            .Build();
        AbuseProtection.BindAndValidateOptions(customConfig).HubNegotiatePermits.Should().Be(500);

        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Abuse:HubNegotiatePermits"] = "0"
            })
            .Build();
        var act = () => AbuseProtection.BindAndValidateOptions(invalidConfig);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*positive*");
    }
}
