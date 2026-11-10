using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.Infrastructure.Services;
using Polly;
using Xunit;

namespace OralExamination.UnitTests.Speech;

public class SpeechAdversarialStressTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ResiliencePipeline CreateFastNoRetryPipeline() =>
        new ResiliencePipelineBuilder().Build();

    // =========================================================================
    // 1. ADVERSARIAL CHALLENGE: BuildContextualWhisperPrompt Anti-Leak & Anti-Injection
    // =========================================================================

    [Fact(DisplayName = "ADV-01: Pass exact polluted ENT001 prompt with 4 guide questions -> NO question marks or guide questions leak")]
    public void BuildContextualWhisperPrompt_ExactEnt001Prompt_ZeroQuestionLeak()
    {
        var maliciousPrompt = "ENT001 - Look at the picture and describe what you see using the following guide questions:\n" +
                              "1. Who are they?\n" +
                              "2. Where are they?\n" +
                              "3. What are they wearing?\n" +
                              "4. What are they doing?";

        var result = SeTerminologyNormalizer.BuildContextualWhisperPrompt(maliciousPrompt);

        // Assertions: Zero question mark, zero guide questions
        result.Should().NotContain("?");
        result.Should().NotContain("Who are they");
        result.Should().NotContain("Where are they");
        result.Should().NotContain("What are they wearing");
        result.Should().NotContain("What are they doing");
        result.Should().NotContain("Look at the picture");
        result.Should().NotContain("describe what you see");
        result.Should().NotContain("following guide questions");
        result.Should().NotContain("guide questions");
        result.Should().Contain("ENT001");
    }

    [Theory(DisplayName = "ADV-02: Diverse malicious guide questions and prompt injection attacks never leak '?'")]
    [InlineData("1. Who are they? 2. Where are they? 3. What are they wearing?")]
    [InlineData("Look at the picture and describe what you see. 1) Who are they? 2) Where are they?")]
    [InlineData("Can you explain Clean Architecture? Why is SOLID important?")]
    [InlineData("DBI202 - Indexing??? Primary Key? Foreign Key?")]
    [InlineData("Bạn có biết CQRS là gì không? Dependency Injection dùng ở đâu?")]
    [InlineData("??? What is this ???")]
    [InlineData("Describe the image: (1) Who are they? (2) What is happening?")]
    [InlineData("Please explain: How does EF Core work?")]
    public void BuildContextualWhisperPrompt_AdversarialInputs_NeverContainQuestionMark(string adversarialInput)
    {
        var result = SeTerminologyNormalizer.BuildContextualWhisperPrompt(adversarialInput);

        result.Should().NotContain("?", because: "Under NO circumstances can question marks leak into the Whisper decoder initial_prompt");
    }

    [Fact(DisplayName = "ADV-03: Prompt containing ONLY questions and guide instructions collapses to empty")]
    public void BuildContextualWhisperPrompt_OnlyQuestionsAndInstructions_ReturnsEmpty()
    {
        var promptOnlyQuestions = "Look at the picture and describe what you see using guide questions: 1. Who are they? 2. Where are they?";
        var result = SeTerminologyNormalizer.BuildContextualWhisperPrompt(promptOnlyQuestions);

        result.Should().BeEmpty(because: "When all tokens are questions or guide instructions, no contextual prompt should be generated");
    }

    [Fact(DisplayName = "ADV-04: Numbered technical keywords without question marks are preserved")]
    public void BuildContextualWhisperPrompt_NumberedTechnicalKeywords_PreservesKeywords()
    {
        var technicalPrompt = "CSD201 - 1. Binary Search Tree 2. Dijkstra Algorithm 3. Red-Black Tree";
        var result = SeTerminologyNormalizer.BuildContextualWhisperPrompt(technicalPrompt);

        result.Should().Contain("CSD201");
        result.Should().Contain("Binary Search Tree");
        result.Should().Contain("Dijkstra Algorithm");
        result.Should().Contain("Red-Black Tree");
        result.Should().NotContain("?");
    }

    // =========================================================================
    // 2. ADVERSARIAL CHALLENGE: Hallucination Sanitizer
    // =========================================================================

    [Fact(DisplayName = "ADV-05: Exact problematic hallucination sentence is 100% sanitized to empty string and flagged")]
    public void SanitizeHallucinations_ExactProblematicSentence_SanitizesToEmptyAndFlagsNoise()
    {
        var exactProblematicSentence = "Looking at the picture, I see a family. They are in the kitchen room. Demonstrate the wearing t-shirt and jeans.";

        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(exactProblematicSentence);
        var isNoise = SeTerminologyNormalizer.IsHallucinationOrNoise(exactProblematicSentence);

        sanitized.Should().BeEmpty(because: "The exact hallucination sentence must be purged to empty string");
        isNoise.Should().BeTrue(because: "The sentence must be flagged as an STT hallucination");
    }

    [Theory(DisplayName = "ADV-06: Variations of picture description hallucinations are purged")]
    [InlineData("Looking at the picture, I see a family.")]
    [InlineData("In this picture, I see a family.")]
    [InlineData("In the picture, I see a family")]
    [InlineData("They are in the kitchen room.")]
    [InlineData("They are in the kitchen")]
    [InlineData("Demonstrate the wearing t-shirt and jeans.")]
    [InlineData("Demonstrate the wearing tshirt and jeans.")]
    [InlineData("They are wearing t-shirt and jeans.")]
    [InlineData("They are wearing tshirt and jeans")]
    [InlineData("LOOKING AT THE PICTURE, I SEE A FAMILY. THEY ARE IN THE KITCHEN ROOM.")]
    [InlineData("... looking at the picture, i see a family ...")]
    [InlineData("They are in the kitchen room. They are in the kitchen room. They are in the kitchen room.")]
    public void SanitizeHallucinations_HallucinationVariants_SanitizesToEmpty(string variant)
    {
        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(variant);
        var isNoise = SeTerminologyNormalizer.IsHallucinationOrNoise(variant);

        sanitized.Should().BeEmpty();
        isNoise.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-07: Mixed hallucination with legitimate student answer purges hallucination but retains answer")]
    public void SanitizeHallucinations_MixedHallucinationAndSpeech_RetainsLegitimateSpeech()
    {
        var mixed = "Looking at the picture, I see a family. Em xin trả lời về Clean Architecture gồm bốn tầng Domain, Application, Infrastructure và Web API.";

        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(mixed);
        var isNoise = SeTerminologyNormalizer.IsHallucinationOrNoise(mixed);

        sanitized.Should().NotContain("Looking at the picture, I see a family");
        sanitized.Should().Contain("Clean Architecture");
        sanitized.Should().Contain("Domain");
        isNoise.Should().BeFalse(because: "Valid student response is present, so it should not be discarded as total noise");
    }

    // =========================================================================
    // 3. ADVERSARIAL CHALLENGE: CloudflareWhisperService Confidence = 0.0 on Hallucination
    // =========================================================================

    private class MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Fact(DisplayName = "ADV-08: When Whisper returns hallucination, CloudflareWhisperService returns ConfidenceScore = 0.0 and empty text")]
    public async Task TranscribeAsync_WhenWhisperReturnsHallucination_ReturnsConfidenceZeroAndEmptyText()
    {
        var hallucinationJson = "{\"result\":{\"text\":\"Looking at the picture, I see a family. They are in the kitchen room. Demonstrate the wearing t-shirt and jeans.\"}}";

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-test-acc",
            ["Cloudflare:ApiToken"] = "cf-test-tok"
        });

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(hallucinationJson, Encoding.UTF8, "application/json")
            }));

        var whisperService = new CloudflareWhisperService(new HttpClient(httpHandler), config, CreateFastNoRetryPipeline());

        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("silence-or-noise-audio-bytes-under-vad"));
        var result = await whisperService.TranscribeDetailedAsync(audioStream, "student_answer.webm", "ENT001", CancellationToken.None);

        result.Text.Should().BeEmpty();
        result.RawText.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0.0, because: "Sanitized inaudible/hallucinatory output must have ConfidenceScore = 0.0");
    }
}
