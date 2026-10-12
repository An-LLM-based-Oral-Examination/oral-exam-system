using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddOralExamAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signingKey = AuthConfiguration.JwtSigningKey(configuration);
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Thiếu JWT_SIGNING_KEY. Thêm khóa ký vào file .env ở thư mục oral-exam-system. Không dùng Client Secret của Google.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = AuthConfiguration.JwtIssuer(configuration),
                    ValidAudience = AuthConfiguration.JwtAudience(configuration),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        return services;
    }
}
