using System.Security.Cryptography;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Bộ sinh số nguyên ngẫu nhiên chuẩn mật mã học (CSPRNG) phục vụ xáo trộn sinh viên và bộ đề khảo thí MF-04,
/// đồng thời cho phép truyền bản hiện thực tất định (seeded) trong Unit Test.
/// </summary>
public interface IOfficialExamRandomGenerator
{
    int NextInt32(int toExclusive);
}

public sealed class CryptoOfficialExamRandomGenerator : IOfficialExamRandomGenerator
{
    public static readonly CryptoOfficialExamRandomGenerator Instance = new();

    public int NextInt32(int toExclusive) => RandomNumberGenerator.GetInt32(toExclusive);
}
