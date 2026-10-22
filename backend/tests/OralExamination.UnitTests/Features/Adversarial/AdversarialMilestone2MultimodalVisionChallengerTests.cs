using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Empirical Challenger 1 for Milestone M2 (Gemini Multimodal Vision & Image Resolution).
/// Adversarial stress testing for:
/// 1. Static image resolution from varied Current Working Directories (CWDs).
/// 2. Gemini Multimodal payload structure (inline_data, mime_type, byte-exact base64).
/// 3. Visual fidelity scoring prompt and hallucination penalties.
/// 4. Path traversal and malicious URL security boundaries.
/// 5. English Refusal Guard and StopWords filtration.
/// </summary>
public class AdversarialMilestone2MultimodalVisionChallengerTests
{
    private static readonly string[] KnownEntImageFiles =
    [
        "ENT001_p3_Im2.png",
        "ENT104_p2_Im1.png",
        "ENT203_p2_IM28.png",
        "ENT203_p2_IM40.png"
    ];

    private static readonly List<AiGradingCriterion> StandardFptuSpeakingCriteria =
    [
        new() { Id = "crit-ideas", Name = "Ideas & Content Development", Description = "Relevant and coherent ideas", MaxScore = 2.0m, Weight = 0.2m },
        new() { Id = "crit-pron", Name = "Pronunciation", Description = "Intelligible pronunciation and intonation", MaxScore = 2.0m, Weight = 0.2m },
        new() { Id = "crit-gram", Name = "Grammar & Accuracy", Description = "Accurate grammatical structures", MaxScore = 2.0m, Weight = 0.2m },
        new() { Id = "crit-vocab", Name = "Vocabulary & Lexical Resource", Description = "Varied and appropriate vocabulary", MaxScore = 2.0m, Weight = 0.2m },
        new() { Id = "crit-fluency", Name = "Fluency & Cohesion", Description = "Smooth delivery and cohesive devices", MaxScore = 2.0m, Weight = 0.2m }
    ];

    private static (GeminiAiGradingService Service, HttpPayloadCapturer Capturer) CreateTestServiceWithCapturer(
        Func<string, HttpResponseMessage>? customResponder = null)
    {
        var capturer = new HttpPayloadCapturer(customResponder ?? (_ =>
        {
            var fakeResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = JsonSerializer.Serialize(new
                                    {
                                        Score = 8.5m,
                                        ConfidenceScore = 0.95m,
                                        IsSuspicious = false,
                                        Feedback = "Good picture description.",
                                        CotTrace = "Observed image and transcript accurately.",
                                        Details = StandardFptuSpeakingCriteria.Select(c => new
                                        {
                                            CriterionId = c.Id,
                                            Score = 1.7m,
                                            Comment = $"Satisfies {c.Name}"
                                        }).ToArray()
                                    })
                                }
                            }
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(fakeResponse), Encoding.UTF8, "application/json")
            };
        }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "fake-api-key-for-empirical-testing"
            })
            .Build();

        var httpClient = new HttpClient(capturer);
        var service = new GeminiAiGradingService(httpClient, configuration);
        return (service, capturer);
    }

    [Fact(DisplayName = "CHALLENGE-M2-01: Static images in uploads/questions/ent/ must exist and be non-empty valid PNGs")]
    public void All_Known_Ent_Static_Images_Must_Exist_And_Have_Valid_Png_Header()
    {
        var assemblyDir = AppContext.BaseDirectory;
        var relativeCandidates = new[]
        {
            Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "src", "API", "wwwroot", "uploads", "questions", "ent"),
            Path.Combine(assemblyDir, "..", "..", "..", "..", "src", "API", "wwwroot", "uploads", "questions", "ent"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions", "ent"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "API", "wwwroot", "uploads", "questions", "ent"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "src", "API", "wwwroot", "uploads", "questions", "ent")
        };

        string? foundDir = null;
        foreach (var candidate in relativeCandidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                foundDir = full;
                break;
            }
        }

        foundDir.Should().NotBeNull("Thư mục static images uploads/questions/ent phải tồn tại");

        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        foreach (var fileName in KnownEntImageFiles)
        {
            var filePath = Path.Combine(foundDir!, fileName);
            File.Exists(filePath).Should().BeTrue($"File {fileName} phải tồn tại");

            var fileBytes = File.ReadAllBytes(filePath);
            fileBytes.Length.Should().BeGreaterThan(1000, $"File {fileName} không được rỗng hoặc quá nhỏ");
            fileBytes.Length.Should().BeLessThanOrEqualTo(5 * 1024 * 1024, $"File {fileName} không được vượt quá 5MB");

            fileBytes.Take(8).Should().BeEquivalentTo(pngHeader, $"File {fileName} phải có PNG magic bytes chuẩn");
        }
    }

    [Theory(DisplayName = "CHALLENGE-M2-02: Multi-Root Image Resolution from Varied Working Directories")]
    [InlineData("CurrentDir_At_ApiRoot")]
    [InlineData("CurrentDir_At_BackendRoot")]
    [InlineData("CurrentDir_At_SourceCodeRoot")]
    [InlineData("CurrentDir_At_TempDir")]
    public async Task Image_Resolution_Must_Succeed_Across_Different_Working_Directories(string cwdScenario)
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            // Simulate different CWDs
            var targetCwd = cwdScenario switch
            {
                "CurrentDir_At_ApiRoot" => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API")),
                "CurrentDir_At_BackendRoot" => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
                "CurrentDir_At_SourceCodeRoot" => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..")),
                "CurrentDir_At_TempDir" => Path.GetTempPath(),
                _ => originalCwd
            };

            if (Directory.Exists(targetCwd))
            {
                Directory.SetCurrentDirectory(targetCwd);
            }

            var (service, capturer) = CreateTestServiceWithCapturer();

            // Test resolving ENT001_p3_Im2.png with leading slash
            var request = new AiGradingRequest
            {
                CourseCode = "ENT001",
                QuestionContent = "Describe the picture in detail.",
                QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
                Transcript = "In this picture, I can see a woman in a laundry room sorting clothes near the washing machine.",
                Criteria = StandardFptuSpeakingCriteria
            };

            var result = await service.GradeAnswerAsync(request, CancellationToken.None);

            result.Should().NotBeNull();
            capturer.CapturedRequestBody.Should().NotBeNull();
            capturer.CapturedRequestBody.Should().Contain("inline_data",
                $"Image resolution phải thành công trong kịch bản CWD = '{cwdScenario}'");
            capturer.CapturedRequestBody.Should().Contain("image/png");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Theory(DisplayName = "CHALLENGE-M2-03: URL Format Flexibility (Leading slash, without slash, and absolute HTTP/HTTPS)")]
    [InlineData("/uploads/questions/ent/ENT104_p2_Im1.png")]
    [InlineData("uploads/questions/ent/ENT104_p2_Im1.png")]
    [InlineData("http://localhost:5000/uploads/questions/ent/ENT104_p2_Im1.png")]
    [InlineData("https://oralexam.fpt.edu.vn/uploads/questions/ent/ENT104_p2_Im1.png")]
    public async Task Image_Resolution_Must_Handle_Diverse_Url_Prefixes(string imageUrl)
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "ENT104",
            QuestionContent = "Look at the picture and describe what is happening.",
            QuestionImageUrl = imageUrl,
            Transcript = "A team of software engineers is having a discussion in front of a whiteboard in an office.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().Contain("inline_data",
            $"Url dạng '{imageUrl}' phải được resolve và đưa vào inline_data");
        capturer.CapturedRequestBody.Should().Contain("image/png");
    }

    [Fact(DisplayName = "CHALLENGE-M2-04: Gemini Multimodal Payload Structure & Exact Base64 Fidelity")]
    public async Task Gemini_Payload_Must_Match_Official_InlineData_Schema_And_Exact_Byte_Hash()
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var imageRelativePath = "/uploads/questions/ent/ENT203_p2_IM28.png";
        var request = new AiGradingRequest
        {
            CourseCode = "ENT203",
            QuestionContent = "Describe the picture.",
            QuestionImageUrl = imageRelativePath,
            QuestionImageDescription = "A customer is ordering food at a restaurant counter.",
            Transcript = "There is a customer standing at the counter and talking to the cashier.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        var capturedJson = capturer.CapturedRequestBody;
        capturedJson.Should().NotBeNullOrWhiteSpace();

        // Parse captured JSON strictly
        using var doc = JsonDocument.Parse(capturedJson!);
        var root = doc.RootElement;

        // Verify contents structure
        root.TryGetProperty("contents", out var contentsElem).Should().BeTrue();
        contentsElem.GetArrayLength().Should().Be(1);

        var firstContent = contentsElem[0];
        firstContent.TryGetProperty("parts", out var partsElem).Should().BeTrue();
        partsElem.GetArrayLength().Should().Be(2, "Multimodal request phải có đúng 2 parts: text và inline_data");

        // Part 0: text
        var textPart = partsElem[0];
        textPart.TryGetProperty("text", out var textElem).Should().BeTrue();
        textElem.GetString().Should().NotBeNullOrWhiteSpace();

        // Part 1: inline_data
        var inlineDataPart = partsElem[1];
        inlineDataPart.TryGetProperty("inline_data", out var inlineDataElem).Should().BeTrue();
        inlineDataElem.TryGetProperty("mime_type", out var mimeTypeElem).Should().BeTrue();
        mimeTypeElem.GetString().Should().Be("image/png");

        inlineDataElem.TryGetProperty("data", out var dataElem).Should().BeTrue();
        var base64String = dataElem.GetString();
        base64String.Should().NotBeNullOrWhiteSpace();

        // Verify Base64 decodes byte-for-byte to the actual file
        var decodedBytes = Convert.FromBase64String(base64String!);

        // Find file on disk and compare SHA-256
        var candidateRoots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API", "wwwroot", "uploads", "questions", "ent", "ENT203_p2_IM28.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions", "ent", "ENT203_p2_IM28.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "API", "wwwroot", "uploads", "questions", "ent", "ENT203_p2_IM28.png")
        };
        var fullPath = candidateRoots.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        fullPath.Should().NotBeNull();

        var diskBytes = await File.ReadAllBytesAsync(fullPath!);
        using var sha256 = SHA256.Create();
        var diskHash = sha256.ComputeHash(diskBytes);
        var payloadHash = sha256.ComputeHash(decodedBytes);

        payloadHash.Should().BeEquivalentTo(diskHash,
            "Dữ liệu base64 trong Gemini payload phải trùng khớp 100% từng byte với file trên ổ đĩa");

        // Verify generationConfig
        root.TryGetProperty("generationConfig", out var genConfigElem).Should().BeTrue();
        genConfigElem.TryGetProperty("response_mime_type", out var respMimeElem).Should().BeTrue();
        respMimeElem.GetString().Should().Be("application/json");
    }

    [Fact(DisplayName = "CHALLENGE-M2-05: Direct Base64 Injection in AiGradingRequest")]
    public async Task Direct_Base64_Input_Must_Be_Accepted_And_Placed_In_InlineData()
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        byte[] fakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03];
        var base64Raw = Convert.ToBase64String(fakePng);
        var dataUri = $"data:image/png;base64,{base64Raw}";

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe the picture.",
            QuestionImageBase64 = dataUri,
            Transcript = "A girl is riding a bicycle in a sunny park.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().Contain("inline_data");
        capturer.CapturedRequestBody.Should().Contain(base64Raw);
        capturer.CapturedRequestBody.Should().NotContain("data:image/png;base64,",
            "Tiền tố data:image/png;base64, phải được bóc tách sạch sẽ, chỉ giữ raw base64 trong inline_data.data");
    }

    [Theory(DisplayName = "CHALLENGE-M2-06: Path Traversal Attack Security Boundary")]
    [InlineData("/uploads/questions/../../../../appsettings.json")]
    [InlineData("/uploads/questions/..\\..\\..\\Windows\\System32\\cmd.exe")]
    [InlineData("/uploads/questions/ent/../../../../etc/passwd")]
    [InlineData("/uploads/questions/ent/../../src/API/appsettings.json")]
    public async Task Path_Traversal_Attempts_Must_Be_Blocked_And_Never_Injected_Into_Payload(string maliciousUrl)
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe what you see.",
            QuestionImageUrl = maliciousUrl,
            Transcript = "I see a picture.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotContain("inline_data",
            $"Đường dẫn độc hại '{maliciousUrl}' phải bị chặn duyệt file và tuyệt đối không xuất hiện inline_data");
    }

    [Fact(DisplayName = "CHALLENGE-M2-07: Missing or Non-Existent Image Falls Back Gracefully to Text-Only")]
    public async Task Missing_Image_File_Must_Fall_Back_To_Text_Only_Without_Crashing()
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe the scene.",
            QuestionImageUrl = "/uploads/questions/ent/does_not_exist_404.png",
            Transcript = "People are sitting at a round table having a business lunch.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotBeNull();
        capturer.CapturedRequestBody.Should().NotContain("inline_data",
            "File không tồn tại thì không được tạo khối inline_data");

        using var doc = JsonDocument.Parse(capturer.CapturedRequestBody!);
        var parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts");
        parts.GetArrayLength().Should().Be(1, "Chỉ có 1 text part khi không tìm thấy ảnh");
    }

    [Fact(DisplayName = "CHALLENGE-M2-08: Visual Fidelity & Anti-Hallucination Prompts for English Courses")]
    public async Task English_Multimodal_Prompt_Must_Enforce_Visual_Fidelity_And_Penalize_Hallucination()
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Look at the picture and describe it.",
            QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
            QuestionImageDescription = "A woman is doing laundry with a front-load washing machine.",
            SampleAnswer = "The picture depicts a woman doing laundry in a domestic setting.",
            Transcript = "A woman is putting clothes into a modern washing machine in a laundry room.",
            Criteria = StandardFptuSpeakingCriteria
        };

        await service.GradeAnswerAsync(request, CancellationToken.None);

        capturer.CapturedRequestBody.Should().NotBeNull();
        var payload = capturer.CapturedRequestBody!;

        // 1. Must use English Oral Examiner persona
        payload.Should().Contain("Giảng viên / Giám khảo chấm thi Vấn đáp Tiếng Anh (English Oral Exam) ĐH FPT");

        // 2. Must explicitly eliminate SE domain bias
        payload.Should().Contain("LOẠI BỎ HOÀN TOÀN THIÊN KIẾN CHUYÊN NGÀNH SE");

        // 3. Must enforce visual fidelity and accuracy
        payload.Should().Contain("VISUAL FIDELITY & ACCURACY");
        payload.Should().Contain("PICTURE DESCRIPTION VISION");

        // 4. Must heavily penalize hallucinations
        payload.Should().Contain("HEAVILY PENALIZE HALLUCINATION");
        payload.Should().Contain("Ideas");
        payload.Should().Contain("0.5/2.0 hoặc 0.0/2.0");

        // 5. Must require all 5 FPTU criteria
        payload.Should().Contain("Ideas & Content Development (2.0)");
        payload.Should().Contain("Pronunciation (2.0)");
        payload.Should().Contain("Grammar & Accuracy (2.0)");
        payload.Should().Contain("Vocabulary & Lexical Resource (2.0)");
        payload.Should().Contain("Fluency & Cohesion (2.0)");

        // 6. Must contain anti-prompt injection tag
        payload.Should().Contain("<student_transcript>");
        payload.Should().Contain("</student_transcript>");
    }

    [Fact(DisplayName = "CHALLENGE-M2-09: SE Course with Image Retains Software Engineering Persona")]
    public async Task Software_Engineering_Course_With_Image_Must_Retain_Se_Lecturer_Persona()
    {
        var (service, capturer) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "SWD392",
            QuestionContent = "Quan sát sơ đồ kiến trúc và giải thích luồng gọi hàm giữa các tầng.",
            QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png", // using an existing file as dummy image
            QuestionImageDescription = "Sơ đồ kiến trúc Clean Architecture",
            SampleAnswer = "Tầng API gọi Use Case của Application, Application điều phối Domain và dùng Interface của Infrastructure.",
            Transcript = "Theo sơ đồ, tầng API tiếp nhận request, chuyển vào Application layer và không phụ thuộc trực tiếp vào Infrastructure.",
            Criteria = new List<AiGradingCriterion>
            {
                new() { Id = "se-1", Name = "Hiểu sơ đồ", Description = "Đọc đúng các tầng", MaxScore = 5.0m, Weight = 0.5m },
                new() { Id = "se-2", Name = "Chiều phụ thuộc", Description = "Quy tắc Dependency Inversion", MaxScore = 5.0m, Weight = 0.5m }
            }
        };

        await service.GradeAnswerAsync(request, CancellationToken.None);

        capturer.CapturedRequestBody.Should().NotBeNull();
        var payload = capturer.CapturedRequestBody!;

        payload.Should().Contain("Bạn là một Giảng viên chuyên ngành Kỹ thuật Phần mềm (SE)");
        payload.Should().NotContain("LOẠI BỎ HOÀN TOÀN THIÊN KIẾN CHUYÊN NGÀNH SE");
        payload.Should().NotContain("Barem Speaking ĐH FPT");
    }

    [Theory(DisplayName = "CHALLENGE-M2-10: English Refusal Guard - Zero Score & Suspicious on Non-Answers")]
    [InlineData("i don't know")]
    [InlineData("I have no idea about this question")]
    [InlineData("pass this question please")]
    [InlineData("skip this question")]
    [InlineData("I cannot answer this")]
    [InlineData("I don't understand, sorry teacher")]
    [InlineData("no answer")]
    public async Task English_Refusal_Phrases_Must_Trigger_Zero_Score_And_Suspicious_Flag(string refusalTranscript)
    {
        var (service, _) = CreateTestServiceWithCapturer();

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe the picture in detail.",
            QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
            QuestionImageDescription = "A woman washing clothes.",
            SampleAnswer = "A woman is doing laundry.",
            Transcript = refusalTranscript,
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Score.Should().Be(0.0m, $"Câu từ chối '{refusalTranscript}' phải nhận điểm 0.0");
        result.IsSuspicious.Should().BeTrue($"Câu từ chối '{refusalTranscript}' phải kích hoạt cờ IsSuspicious");
        result.Details.Should().AllSatisfy(d => d.Score.Should().Be(0.0m));
        result.Feedback.Should().Contain("Thí sinh không trả lời hoặc bài nói không thể hiện năng lực giao tiếp Tiếng Anh");
    }

    [Fact(DisplayName = "CHALLENGE-M2-11: English StopWords Prevent False Refusal Guard Bypass")]
    public async Task Common_English_Words_In_Refusal_Should_Not_Count_As_Technical_Keywords()
    {
        var (service, _) = CreateTestServiceWithCapturer();

        // The student says "sorry teacher, I cannot answer this question from the book"
        // Without English stopwords ("sorry", "teacher", "question", "from", "the"),
        // words like "teacher", "question" would be counted as keywords, potentially bypassing refusal guard!
        var request = new AiGradingRequest
        {
            CourseCode = "ENT104",
            QuestionContent = "What is the team doing in the office?",
            QuestionImageUrl = "/uploads/questions/ent/ENT104_p2_Im1.png",
            QuestionImageDescription = "A team meeting in the office.",
            SampleAnswer = "The team is discussing a project around a whiteboard.",
            Transcript = "Sorry teacher, I cannot answer this question from the lesson.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Score.Should().Be(0.0m, "Câu xin lỗi bỏ qua câu hỏi không được tính điểm dù có từ 'teacher', 'question'");
        result.IsSuspicious.Should().BeTrue();
    }

    [Fact(DisplayName = "CHALLENGE-M2-12: End-to-End Evaluation Flow - Accurate Description Awards High Score")]
    public async Task End_To_End_Accurate_Picture_Description_Parses_High_Scores_Correctly()
    {
        var (service, _) = CreateTestServiceWithCapturer(_ =>
        {
            var response = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = JsonSerializer.Serialize(new
                                    {
                                        Score = 9.2m,
                                        ConfidenceScore = 0.98m,
                                        IsSuspicious = false,
                                        Feedback = "Excellent visual description with rich vocabulary and fluent delivery.",
                                        CotTrace = "Candidate accurately described the woman, laundry room, washing machine and clothing items.",
                                        Details = new[]
                                        {
                                            new { CriterionId = "crit-ideas", Score = 1.9m, Comment = "Vivid and accurate details of the laundry setting." },
                                            new { CriterionId = "crit-pron", Score = 1.8m, Comment = "Clear pronunciation." },
                                            new { CriterionId = "crit-gram", Score = 1.8m, Comment = "Accurate present continuous tense usage." },
                                            new { CriterionId = "crit-vocab", Score = 1.9m, Comment = "Varied vocabulary: front-loader, detergent, basket." },
                                            new { CriterionId = "crit-fluency", Score = 1.8m, Comment = "Smooth speech without pauses." }
                                        }
                                    })
                                }
                            }
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
            };
        });

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe the picture in detail.",
            QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
            Transcript = "In this picture, I can see a woman crouching down in front of a white front-load washing machine in a laundry room. She is holding colorful clothes and loading them into the machine.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Score.Should().Be(9.2m);
        result.ConfidenceScore.Should().Be(0.98m);
        result.IsSuspicious.Should().BeFalse();
        result.Details.Should().HaveCount(5);
        result.Details.Sum(d => d.Score).Should().Be(9.2m);
    }

    [Fact(DisplayName = "CHALLENGE-M2-13: End-to-End Evaluation Flow - Severe Hallucination Penalization")]
    public async Task End_To_End_Hallucinated_Description_Parses_Low_Score_With_Penalized_Ideas()
    {
        var (service, _) = CreateTestServiceWithCapturer(_ =>
        {
            var response = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = JsonSerializer.Serialize(new
                                    {
                                        Score = 2.5m,
                                        ConfidenceScore = 0.90m,
                                        IsSuspicious = false,
                                        Feedback = "Candidate spoke fluently in English but completely hallucinated the content (described a sunny beach instead of a laundry room).",
                                        CotTrace = "Image depicts a laundry room. Candidate described swimming at the beach. Heavy hallucination penalty applied to Ideas criterion.",
                                        Details = new[]
                                        {
                                            new { CriterionId = "crit-ideas", Score = 0.2m, Comment = "Severe visual hallucination: described beach instead of laundry." },
                                            new { CriterionId = "crit-pron", Score = 0.7m, Comment = "Clear pronunciation." },
                                            new { CriterionId = "crit-gram", Score = 0.6m, Comment = "Basic grammatical structures correct." },
                                            new { CriterionId = "crit-vocab", Score = 0.5m, Comment = "Off-topic beach vocabulary." },
                                            new { CriterionId = "crit-fluency", Score = 0.5m, Comment = "Spoke smoothly despite being completely off-topic." }
                                        }
                                    })
                                }
                            }
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
            };
        });

        var request = new AiGradingRequest
        {
            CourseCode = "ENT001",
            QuestionContent = "Describe the picture in detail.",
            QuestionImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
            QuestionImageDescription = "A woman is doing laundry in a laundry room.",
            Transcript = "I can see a beautiful sunny beach with golden sand and coconut trees. People are swimming in the ocean and enjoying the sunshine.",
            Criteria = StandardFptuSpeakingCriteria
        };

        var result = await service.GradeAnswerAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Score.Should().Be(2.5m);
        var ideasCriterion = result.Details.First(d => d.CriterionId == "crit-ideas");
        ideasCriterion.Score.Should().BeLessThanOrEqualTo(0.5m, "Tiêu chí Ideas phải bị phạt nặng (<= 0.5) khi bị ảo giác thị giác");
        result.CotTrace.Should().Contain("hallucination");
    }

    private sealed class HttpPayloadCapturer(Func<string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? CapturedRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return responder(CapturedRequestBody ?? string.Empty);
        }
    }
}
