using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Services;

public class GeminiQuestionGenerationContractTests
{
    public static IEnumerable<object[]> CourseProviders()
    {
        foreach (var provider in new[] { "gemini", "cloudflare" })
        {
            yield return new object[] { provider, "SWR302", "Identify appropriate requirements elicitation techniques." };
            yield return new object[] { provider, "SWT301", "Giải thích mục đích kiểm thử tĩnh và rà soát yêu cầu." };
            yield return new object[] { provider, "PRF192", "Explain variables and control flow in a C program." };
            yield return new object[] { provider, "CSD201", "Compare array and linked list operations." };
            yield return new object[] { provider, "DBI202", "Explain relational keys and referential integrity." };
        }
    }

    private static AiQuestionGenerationParams Parameters(string course = "SWT301", string description = "Explain the purpose of static testing and reviews.") =>
        new(course, "Course " + course, new() { "CLO1" }, new() { "Core concepts" }, 1, UsageScope.Exam,
            new() { BloomLevel.Understand }, 1, 0, 0,
            new() { new AiQuestionCloContext("CLO1", description, BloomLevel.Understand) });

    private static JsonArray ValidBatch() => JsonNode.Parse("""
        [{
          "cloCode":"CLO1", "title":"Purpose of testing",
          "content":"What is the purpose of testing? Explain how it informs a release decision.",
          "difficulty":"easy", "bloomLevel":"Understand", "source":"untrusted-provider-source",
          "modelAnswer":"Testing reveals failures and provides evidence of product quality to inform a release decision; it cannot prove the absence of all defects.",
          "keyPoints":["Reveals failures", "Provides quality evidence"],
          "rubric":{"name":"Purpose and evidence", "description":"Assess only the two requested explanations", "totalMaxScore":10,
            "criteria":[
              {"criterionName":"Purpose", "maxScore":6, "weight":99, "bloomLevel":"Understand", "description":"Explains that testing reveals failures", "orderIndex":1},
              {"criterionName":"Decision evidence", "maxScore":4, "weight":99, "bloomLevel":"Understand", "description":"Explains how quality evidence informs release", "orderIndex":2}
            ]}
        }]
        """)!.AsArray();

    private sealed class Transport : HttpMessageHandler
    {
        private readonly string _provider;
        private readonly string _batch;
        public List<string> Prompts { get; } = new();
        public List<string> Providers { get; } = new();
        public int Calls { get; private set; }
        public Transport(string provider, string batch) { _provider = provider; _batch = batch; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var isGemini = request.RequestUri!.Host == "generativelanguage.googleapis.com";
            Providers.Add(isGemini ? "gemini" : "cloudflare");
            if (_provider == "cloudflare" && isGemini)
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Prompts.Add(isGemini
                ? body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!
                : body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var response = isGemini
                ? JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = _batch } } } } } })
                : JsonSerializer.Serialize(new { result = new { response = _batch } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private static GeminiQuestionGenerationService Service(Transport transport)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-generation-contract-key",
            ["Gemini:ApiKeys"] = "",
            ["Gemini:FallbackApiKey"] = "",
            ["Cloudflare:AccountId"] = "test-account",
            ["Cloudflare:ApiToken"] = "test-token"
        }).Build();
        return new GeminiQuestionGenerationService(config, NullLogger<GeminiQuestionGenerationService>.Instance, new HttpClient(transport));
    }

    [Theory]
    [MemberData(nameof(CourseProviders))]
    public async Task ValidBatch_UsesActualCloContextAndPreservesRubric(string provider, string course, string description)
    {
        using var transport = new Transport(provider, ValidBatch().ToJsonString());
        var result = await Service(transport).GenerateQuestionsAsync(Parameters(course, description));

        result.Should().ContainSingle();
        var question = result.Single();
        question.CloCode.Should().Be("CLO1");
        question.Source.Should().Be(QuestionSource.AiGenerated);
        question.UsageScope.Should().Be(UsageScope.Exam);
        question.Rubric.Criteria.Select(c => c.MaxScore).Should().Equal(6m, 4m);
        question.Rubric.Criteria.Select(c => c.Weight).Should().Equal(.6m, .4m);
        question.Rubric.Criteria.Select(c => c.CriterionName).Should().Equal("Purpose", "Decision evidence");
        transport.Prompts.Should().NotBeEmpty();
        foreach (var prompt in transport.Prompts)
        {
            prompt.Should().Contain(course).And.Contain(description).And.Contain("CLO1").And.Contain("Understand")
                .And.Contain("Bạn là giảng viên");
        }
    }

    [Theory]
    [InlineData("gemini")]
    [InlineData("cloudflare")]
    public async Task SwrGuidance_ReachesBothProviders_WithoutUnselectedClos(string provider)
    {
        var guidance = new QuestionGenerationGuidanceProvider().Get("SWR302", new[] { "CLO6" });
        var parameters = Parameters("SWR302") with
        {
            SelectedCloCodes = new() { "CLO6" },
            SelectedClos = new() { new("CLO6", "Design and plan software solutions using an object-oriented strategy.", BloomLevel.Apply) },
            Guidance = guidance
        };
        var batch = ValidBatch();
        batch[0]!["cloCode"] = "CLO6";
        using var transport = new Transport(provider, batch.ToJsonString());
        var result = await Service(transport).GenerateQuestionsAsync(parameters);
        result.Should().ContainSingle().Which.CloCode.Should().Be("CLO6");
        transport.Prompts.Should().NotBeEmpty();
        foreach (var prompt in transport.Prompts)
        {
            prompt.Should().Contain(guidance!.CourseScope)
                .And.Contain(guidance.Clos.Single().Boundaries)
                .And.Contain("Use Case").And.Contain("Unit Testing")
                .And.NotContain("Requirements Elicitation")
                .And.NotContain("LEGACY-");
        }
    }

    [Theory]
    [InlineData("missing-rubric")]
    [InlineData("single-criterion")]
    [InlineData("wrong-score")]
    [InlineData("wrong-total")]
    [InlineData("nonpositive-score")]
    [InlineData("unknown-clo")]
    [InlineData("wrong-bloom")]
    [InlineData("wrong-count")]
    [InlineData("wrong-distribution")]
    [InlineData("missing-keypoints")]
    [InlineData("short-answer")]
    [InlineData("missing-title")]
    [InlineData("missing-criterion-description")]
    [InlineData("unsupported-score-precision")]
    [InlineData("unsupported-weight-precision")]
    public async Task InvalidBatch_IsRejectedByBothProviderPathsWithoutSyntheticRepair(string defect)
    {
        var batch = ValidBatch();
        var q = batch[0]!.AsObject();
        switch (defect)
        {
            case "missing-rubric": q.Remove("rubric"); break;
            case "single-criterion": q["rubric"]!["criteria"]!.AsArray().RemoveAt(1); break;
            case "wrong-score": q["rubric"]!["criteria"]![0]!["maxScore"] = 3; break;
            case "wrong-total": q["rubric"]!["totalMaxScore"] = 20; break;
            case "nonpositive-score": q["rubric"]!["criteria"]![0]!["maxScore"] = 0; break;
            case "unknown-clo": q["cloCode"] = "CLO99"; break;
            case "wrong-bloom": q["bloomLevel"] = "Create"; break;
            case "wrong-count": batch.Add(q.DeepClone()); break;
            case "wrong-distribution": q["difficulty"] = "hard"; break;
            case "missing-keypoints": q.Remove("keyPoints"); break;
            case "short-answer": q["modelAnswer"] = "Too short"; break;
            case "missing-title": q.Remove("title"); break;
            case "missing-criterion-description": q["rubric"]!["criteria"]![0]!.AsObject().Remove("description"); break;
            case "unsupported-score-precision":
            case "unsupported-weight-precision":
                var criteria = q["rubric"]!["criteria"]!.AsArray();
                var third = criteria[1]!.DeepClone();
                third["criterionName"] = "Release limitations";
                third["description"] = "Explains limitations of the available release evidence";
                criteria.Add(third);
                criteria[0]!["maxScore"] = defect == "unsupported-score-precision" ? 3.333m : 3.33m;
                criteria[1]!["maxScore"] = defect == "unsupported-score-precision" ? 3.333m : 3.33m;
                criteria[2]!["maxScore"] = defect == "unsupported-score-precision" ? 3.334m : 3.34m;
                break;
        }
        using var transport = new Transport("gemini", batch.ToJsonString());
        Func<Task> act = () => Service(transport).GenerateQuestionsAsync(Parameters());
        await act.Should().ThrowAsync<HttpRequestException>();
        transport.Providers.Should().Contain("gemini").And.Contain("cloudflare",
            "both providers must reject the malformed batch rather than inventing a valid rubric");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("blank-description")]
    [InlineData("wrong-code")]
    public async Task IncompleteCloContext_IsRejectedBeforeHttp(string defect)
    {
        List<AiQuestionCloContext> context = defect switch
        {
            "missing" => new List<AiQuestionCloContext>(),
            "blank-description" => new() { new("CLO1", " ", "Understand") },
            _ => new() { new("CLO99", "Description", "Understand") }
        };
        using var transport = new Transport("gemini", ValidBatch().ToJsonString());
        Func<Task> act = () => Service(transport).GenerateQuestionsAsync(Parameters() with { SelectedClos = context });
        await act.Should().ThrowAsync<ArgumentException>();
        transport.Calls.Should().Be(0);
    }
}
