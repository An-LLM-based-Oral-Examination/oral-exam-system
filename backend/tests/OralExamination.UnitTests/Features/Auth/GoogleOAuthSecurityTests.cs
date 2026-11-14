using FluentAssertions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Auth;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Domain.Exceptions;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public sealed class GoogleOAuthSecurityTests
{
    [Fact]
    public async Task Nonce_IsUnpredictableSingleUse_AndConcurrentConsumeHasOneWinner()
    {
        var store = new GoogleLoginNonceStore();
        var nonce = store.Issue();
        nonce.Should().HaveLength(43);
        store.Issue().Should().NotBe(nonce);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => store.TryConsume(nonce))));
        outcomes.Count(value => value).Should().Be(1);
    }

    [Fact]
    public void ExpiredNonce_AndStoreRestart_FailClosed()
    {
        var clock = new TestClock();
        var store = new GoogleLoginNonceStore(clock);
        var nonce = store.Issue();
        new GoogleLoginNonceStore(clock).TryConsume(nonce).Should().BeFalse();
        clock.Now = clock.Now.AddMinutes(5);
        store.TryConsume(nonce).Should().BeFalse();
        store.TryConsume("").Should().BeFalse();
    }

    [Theory]
    [InlineData("issuer")][InlineData("audience")][InlineData("expired")][InlineData("verified")]
    [InlineData("domain")][InlineData("hosted-domain")][InlineData("subject")][InlineData("nonce")]
    public async Task VerifiedPayloadPolicy_RejectsInvalidIdentity(string invalid)
    {
        var payload = ValidPayload();
        switch (invalid)
        {
            case "issuer": payload.Issuer = "https://attacker.test"; break;
            case "audience": payload.Audience = "other-client"; break;
            case "expired": payload.ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); break;
            case "verified": payload.EmailVerified = false; break;
            case "domain": payload.Email = "person@evilfpt.edu.vn"; break;
            case "hosted-domain": payload.HostedDomain = null; break;
            case "subject": payload.Subject = ""; break;
            case "nonce": payload.Nonce = null; break;
        }
        var validator = new GoogleIdTokenValidator(Config(), new SignatureStub(payload));
        await validator.Invoking(value => value.ValidateAsync("opaque-id-token", default)).Should().ThrowAsync<GoogleIdTokenRejectedException>();
    }

    [Fact]
    public void NonceStore_RejectsCapacityOverflow_ThenReclaimsExpiredEntries()
    {
        var clock = new TestClock();
        var store = new GoogleLoginNonceStore(clock, capacity: 2);
        var first = store.Issue();
        store.Issue();
        store.Invoking(value => value.Issue()).Should().Throw<QuotaExceededException>();
        clock.Now = clock.Now.AddMinutes(5);
        var renewed = store.Issue();
        store.TryConsume(first).Should().BeFalse();
        store.TryConsume(renewed).Should().BeTrue();
    }

    [Fact]
    public async Task ValidWorkspacePayload_PreservesSignedNonceAndSubject()
    {
        var verifier = new SignatureStub(ValidPayload());
        var identity = await new GoogleIdTokenValidator(Config(), verifier).ValidateAsync("opaque", default);
        identity.Email.Should().Be("person@fpt.edu.vn");
        identity.Nonce.Should().Be("signed-nonce");
        identity.Subject.Should().Be("google-subject");
        verifier.Audience.Should().Be("test-client");
    }

    [Fact]
    public async Task DevEmailBypass_IsRejectedEvenWhenLegacySwitchIsEnabled()
    {
        var settings = new Dictionary<string,string?> { ["Authentication:AllowDevGoogleEmail"] = "true", ["ASPNETCORE_ENVIRONMENT"] = "Development" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        await new GoogleIdTokenValidator(config).Invoking(value => value.ValidateAsync("dev-google:admin@fpt.edu.vn", default))
            .Should().ThrowAsync<GoogleIdTokenRejectedException>();
    }

    [Theory]
    [InlineData("student@gmail.com", null)]
    [InlineData("student@other-school.edu", "other-school.edu")]
    public async Task StudentDataCollection_DefaultPolicyAcceptsGoogleOwnedOrWorkspaceEmail(string email, string? hostedDomain)
    {
        var payload = ValidPayload();
        payload.Email = email;
        payload.HostedDomain = hostedDomain;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["Authentication:GoogleClientId"] = "test-client" }).Build();
        var identity = await new GoogleIdTokenValidator(config, new SignatureStub(payload)).ValidateAsync("opaque", default);
        identity.Email.Should().Be(email);
    }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Authentication:GoogleClientId"] = "test-client", ["Authentication:GoogleAllowedDomains:0"] = "fpt.edu.vn" }).Build();
    private static GoogleJsonWebSignature.Payload ValidPayload() => new()
    {
        Issuer = "https://accounts.google.com", Audience = "test-client", Subject = "google-subject",
        Email = "person@fpt.edu.vn", EmailVerified = true, HostedDomain = "fpt.edu.vn", Name = "QA",
        ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(), Nonce = "signed-nonce"
    };
    private sealed class SignatureStub(GoogleJsonWebSignature.Payload payload) : IGoogleTokenSignatureVerifier
    {
        public string? Audience { get; private set; }
        public Task<GoogleJsonWebSignature.Payload> VerifyAsync(string token, string audience, CancellationToken cancellationToken)
        { Audience = audience; return Task.FromResult(payload); }
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
