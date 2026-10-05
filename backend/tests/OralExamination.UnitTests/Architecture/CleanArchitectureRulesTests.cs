using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Architecture;

public class CleanArchitectureRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Result).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(OralExamDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(API.Controllers.v1.QuestionsController).Assembly;

    [Fact(DisplayName = "1. Tầng Domain tuyệt đối không tham chiếu các tầng ngoài (Application, Infrastructure, API)")]
    public void Domain_Assembly_Must_Not_Reference_Outer_Layers()
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        var forbiddenAssemblies = new[]
        {
            "OralExamination.Application",
            "OralExamination.Infrastructure",
            "OralExamination.API",
            "Application",
            "Infrastructure",
            "API"
        };

        foreach (var forbidden in forbiddenAssemblies)
        {
            referencedAssemblies.Should().NotContain(forbidden, 
                $"Domain là tầng lõi thuần khiết, tuyệt đối không được tham chiếu tới {forbidden}");
        }
    }

    [Fact(DisplayName = "2. Tầng Application chỉ phụ thuộc Domain, không tham chiếu Infrastructure hay API")]
    public void Application_Assembly_Must_Not_Reference_Infrastructure_Or_API()
    {
        var referencedAssemblies = ApplicationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        var forbiddenAssemblies = new[]
        {
            "OralExamination.Infrastructure",
            "OralExamination.API",
            "Infrastructure",
            "API"
        };

        foreach (var forbidden in forbiddenAssemblies)
        {
            referencedAssemblies.Should().NotContain(forbidden, 
                $"Application chỉ điều phối nghiệp vụ qua Abstractions/Interfaces, không phụ thuộc vào {forbidden}");
        }
    }

    [Fact(DisplayName = "3. Controllers tuyệt đối KHÔNG inject trực tiếp DbContext hay IApplicationDbContext")]
    public void Controllers_Must_Not_Inject_DbContext_Directly()
    {
        var controllerTypes = ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        controllerTypes.Should().NotBeEmpty("Dự án phải có ít nhất 1 Controller để phục vụ REST API.");

        foreach (var controller in controllerTypes)
        {
            var constructors = controller.GetConstructors();
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                foreach (var param in parameters)
                {
                    var paramType = param.ParameterType;

                    paramType.Should().NotBe(typeof(OralExamDbContext),
                        $"Controller {controller.Name} không được inject trực tiếp OralExamDbContext");

                    paramType.Should().NotBe(typeof(IApplicationDbContext),
                        $"Controller {controller.Name} không được inject trực tiếp IApplicationDbContext");

                    paramType.Name.Should().NotContain("DbContext",
                        $"Controller {controller.Name} vi phạm Clean Architecture khi inject {paramType.Name}");
                }
            }
        }
    }

    [Fact(DisplayName = "4. Controllers khi khai báo dependencies bắt buộc phải ủy thác qua MediatR ISender / IMediator")]
    public void Controllers_Should_Depend_On_MediatR_ISender()
    {
        var controllerTypes = ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var parameterizedCtors = controller.GetConstructors()
                .Where(c => c.GetParameters().Length > 0)
                .ToList();

            if (parameterizedCtors.Any())
            {
                var hasSenderOrMediator = parameterizedCtors.Any(ctor =>
                    ctor.GetParameters().Any(p => p.ParameterType == typeof(ISender) || p.ParameterType == typeof(IMediator)));

                hasSenderOrMediator.Should().BeTrue(
                    $"Controller {controller.Name} khi khai báo dependencies bắt buộc phải inject ISender hoặc IMediator để gửi CQRS commands/queries");
            }
        }
    }

    [Fact(DisplayName = "5. Khóa Taboo #1: Tuyệt đối CẤM tạo bảng hoặc cột 'chapter' trong Domain")]
    public void Domain_Entities_Must_Not_Contain_Chapter_Class_Or_Property()
    {
        var domainTypes = DomainAssembly.GetTypes().ToList();

        // 1. Kiểm tra không có class nào tên Chapter hoặc chứa Chapter
        var chapterClasses = domainTypes
            .Where(t => t.Name.Contains("Chapter", StringComparison.OrdinalIgnoreCase))
            .ToList();

        chapterClasses.Should().BeEmpty(
            "CẤM tạo class hoặc entity mang tên 'Chapter'. Ngân hàng câu hỏi phân loại theo CourseId, BloomLevel và CLO.");

        // 2. Kiểm tra không có entity nào chứa property tên Chapter hoặc ChapterId
        var entityTypes = domainTypes.Where(t => t.IsClass && !t.IsAbstract).ToList();
        foreach (var entity in entityTypes)
        {
            var properties = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var chapterProps = properties
                .Where(p => p.Name.Contains("Chapter", StringComparison.OrdinalIgnoreCase))
                .ToList();

            chapterProps.Should().BeEmpty(
                $"Thực thể {entity.Name} chứa property vi phạm Taboo #1: {string.Join(", ", chapterProps.Select(p => p.Name))}");
        }
    }

    [Fact(DisplayName = "6. Khóa Taboo #2: Tuyệt đối CẤM dùng tên cột 'workstation_ip', bắt buộc dùng 'ip_address'")]
    public void Domain_Entities_Must_Not_Contain_WorkstationIp_Property()
    {
        var domainTypes = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var entity in domainTypes)
        {
            var properties = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var forbiddenProps = properties
                .Where(p => p.Name.Contains("WorkstationIp", StringComparison.OrdinalIgnoreCase) ||
                            p.Name.Equals("Workstation_Ip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            forbiddenProps.Should().BeEmpty(
                $"Thực thể {entity.Name} chứa thuộc tính {string.Join(", ", forbiddenProps.Select(p => p.Name))}. Bắt buộc dùng thống nhất 'IpAddress' / 'ip_address'.");
        }
    }

    [Fact(DisplayName = "7. Tất cả MediatR Handlers phải có hậu tố 'Handler' và triển khai IRequestHandler")]
    public void Handlers_Must_Have_Suffix_Handler_And_Implement_IRequestHandler()
    {
        var handlerTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        handlerTypes.Should().NotBeEmpty("Dự án phải có các MediatR Request Handlers trong Application Layer");

        foreach (var handler in handlerTypes)
        {
            handler.Name.Should().EndWith("Handler",
                $"Handler {handler.Name} bắt buộc phải có hậu tố 'Handler' theo chuẩn CQRS MediatR");
        }
    }

    [Fact(DisplayName = "8. Tất cả Commands và Queries trong Application Layer phải triển khai IRequest<T>")]
    public void Commands_And_Queries_Must_Implement_IRequest()
    {
        var featureTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && (t.Name.EndsWith("Command") || t.Name.EndsWith("Query")))
            .ToList();

        featureTypes.Should().NotBeEmpty("Dự án phải có các Commands và Queries trong Features");

        foreach (var type in featureTypes)
        {
            var implementsRequest = typeof(IBaseRequest).IsAssignableFrom(type);
            implementsRequest.Should().BeTrue(
                $"CQRS request {type.Name} bắt buộc phải implement IRequest<T> hoặc IBaseRequest của MediatR");
        }
    }

    [Fact(DisplayName = "9. Tất cả Validators phải kế thừa từ AbstractValidator<T>")]
    public void Validators_Must_Inherit_From_AbstractValidator()
    {
        var validatorTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Validator"))
            .ToList();

        validatorTypes.Should().NotBeEmpty("Dự án phải có các FluentValidation Validators");

        foreach (var validator in validatorTypes)
        {
            var inheritsAbstractValidator = validator.BaseType != null &&
                validator.BaseType.IsGenericType &&
                validator.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>);

            inheritsAbstractValidator.Should().BeTrue(
                $"Validator {validator.Name} bắt buộc phải kế thừa từ AbstractValidator<T>");
        }
    }
}
