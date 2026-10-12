using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly IConfiguration _configuration;

    public JwtAccessTokenIssuer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role)
    {
        var signingKey = AuthConfiguration.JwtSigningKey(_configuration);
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Thiếu JWT_SIGNING_KEY. Thêm khóa ký vào file .env ở thư mục oral-exam-system. Không dùng Client Secret của Google.");
        }

        var minutes = AuthConfiguration.AccessTokenMinutes(_configuration);
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: AuthConfiguration.JwtIssuer(_configuration),
            audience: AuthConfiguration.JwtAudience(_configuration),
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, fullName),
                new Claim("role", role)
            },
            expires: expires,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), minutes * 60);
    }
}
