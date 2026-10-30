using System;
using System.Collections.Generic;
using System.Linq;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Common;

/// <summary>
/// Bảo toàn thứ tự câu hỏi gốc (OrderIndex 1..N) cho MockExamAnswer mà không cần thay đổi schema CSDL.
/// Mã hóa chữ ký 4-byte "MKOR" (0x4D, 0x4B, 0x4F, 0x52) và 2-byte OrderIndex (big-endian ushort)
/// vào 6 byte đầu của MockExamAnswer.Id khi tạo đề thi thử.
/// </summary>
public static class MockExamAnswerOrdering
{
    private const byte Magic0 = 0x4D; // 'M'
    private const byte Magic1 = 0x4B; // 'K'
    private const byte Magic2 = 0x4F; // 'O'
    private const byte Magic3 = 0x52; // 'R'

    public static Guid CreateOrderedAnswerId(int oneBasedOrderIndex)
    {
        var clampedIndex = (ushort)Math.Clamp(oneBasedOrderIndex, 1, ushort.MaxValue);
        var bytes = Guid.NewGuid().ToByteArray();
        bytes[0] = Magic0;
        bytes[1] = Magic1;
        bytes[2] = Magic2;
        bytes[3] = Magic3;
        bytes[4] = (byte)(clampedIndex >> 8);
        bytes[5] = (byte)(clampedIndex & 0xFF);
        return new Guid(bytes);
    }

    public static bool TryGetEmbeddedOrderIndex(Guid answerId, out int orderIndex)
    {
        var bytes = answerId.ToByteArray();
        if (bytes[0] == Magic0 && bytes[1] == Magic1 && bytes[2] == Magic2 && bytes[3] == Magic3)
        {
            orderIndex = (bytes[4] << 8) | bytes[5];
            if (orderIndex > 0)
            {
                return true;
            }
        }

        orderIndex = int.MaxValue;
        return false;
    }

    public static List<MockExamAnswer> OrderAnswers(IEnumerable<MockExamAnswer> answers)
    {
        var list = (answers ?? Enumerable.Empty<MockExamAnswer>()).ToList();
        var mainAnswers = list.Where(a => !a.IsFollowUp).ToList();
        var hasDistinctExplicitOrderIndex = mainAnswers.Count >= 2
            && mainAnswers.All(a => a.OrderIndex > 0)
            && mainAnswers.Select(a => a.OrderIndex).Distinct().Count() == mainAnswers.Count;

        return list
            .OrderBy(a =>
            {
                if (hasDistinctExplicitOrderIndex && a.OrderIndex > 0)
                {
                    return a.OrderIndex;
                }

                return TryGetEmbeddedOrderIndex(a.Id, out var idx)
                    ? idx
                    : (a.OrderIndex > 0 ? a.OrderIndex : int.MaxValue);
            })
            .ThenBy(a => a.IsFollowUp)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();
    }
}
