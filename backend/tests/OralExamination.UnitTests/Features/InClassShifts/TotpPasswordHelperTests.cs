using System;
using OralExamination.Application.Common.Security;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public class TotpPasswordHelperTests
{
    private const string TestSecret = "4F52414C5F4558414D5F544553545F5345435245545F4B45595F313233343536";

    [Fact]
    public void GenerateSecretKey_ReturnsValidHexStringWithExpectedLength()
    {
        // Act
        string key = TotpPasswordHelper.GenerateSecretKey(32);

        // Assert
        Assert.NotNull(key);
        Assert.Equal(64, key.Length); // 32 bytes = 64 hex chars
    }

    [Fact]
    public void ComputeTotp_GeneratesExactlySixDigits()
    {
        // Arrange
        long unixTime = 1730000000L;

        // Act
        string otp = TotpPasswordHelper.ComputeTotp(TestSecret, unixTime, stepSeconds: 300, digits: 6);

        // Assert
        Assert.NotNull(otp);
        Assert.Equal(6, otp.Length);
        Assert.True(int.TryParse(otp, out _), "OTP should contain only numeric digits.");
    }

    [Fact]
    public void ComputeTotp_IsDeterministicForSameTimestamp()
    {
        // Arrange
        long unixTime = 1730001234L;

        // Act
        string otp1 = TotpPasswordHelper.ComputeTotp(TestSecret, unixTime, 300, 6);
        string otp2 = TotpPasswordHelper.ComputeTotp(TestSecret, unixTime, 300, 6);

        // Assert
        Assert.Equal(otp1, otp2);
    }

    [Fact]
    public void ComputeTotp_DifferentTimeSteps_YieldDifferentCodes()
    {
        // Arrange
        long timeStep1 = 1730000000L; // step 1
        long timeStep2 = 1730000300L; // step 2 (+300s)

        // Act
        string otp1 = TotpPasswordHelper.ComputeTotp(TestSecret, timeStep1, 300, 6);
        string otp2 = TotpPasswordHelper.ComputeTotp(TestSecret, timeStep2, 300, 6);

        // Assert
        Assert.NotEqual(otp1, otp2);
    }

    [Fact]
    public void ValidateCode_ValidCurrentCode_ReturnsTrue()
    {
        // Arrange
        var testTime = new DateTime(2026, 11, 5, 10, 15, 30, DateTimeKind.Utc);
        string validCode = TotpPasswordHelper.GenerateCode(TestSecret, testTime);

        // Act
        bool isValid = TotpPasswordHelper.ValidateCode(TestSecret, validCode, testTime);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateCode_GracePeriod_PreviousStepValidWithin60Seconds()
    {
        // Arrange: step boundary at 10:15:00
        // Step 1: 10:10:00 - 10:14:59
        // Step 2: 10:15:00 - 10:19:59
        var step1Time = new DateTime(2026, 11, 5, 10, 14, 50, DateTimeKind.Utc);
        string step1Code = TotpPasswordHelper.GenerateCode(TestSecret, step1Time);

        // Now advance 30 seconds into Step 2 (10:15:20) -> within 60s grace period
        var step2TimeWithinGrace = new DateTime(2026, 11, 5, 10, 15, 20, DateTimeKind.Utc);

        // Act
        bool isValid = TotpPasswordHelper.ValidateCode(TestSecret, step1Code, step2TimeWithinGrace, gracePeriodSeconds: 60);

        // Assert
        Assert.True(isValid, "Previous step code should be accepted within 60 seconds grace period.");
    }

    [Fact]
    public void ValidateCode_ExpiredAfterGracePeriod_ReturnsFalse()
    {
        // Arrange
        var step1Time = new DateTime(2026, 11, 5, 10, 14, 50, DateTimeKind.Utc);
        string step1Code = TotpPasswordHelper.GenerateCode(TestSecret, step1Time);

        // Now advance 61 seconds into Step 2 (10:16:01) -> past 60s grace period
        var step2TimePastGrace = new DateTime(2026, 11, 5, 10, 16, 1, DateTimeKind.Utc);

        // Act
        bool isValid = TotpPasswordHelper.ValidateCode(TestSecret, step1Code, step2TimePastGrace, gracePeriodSeconds: 60);

        // Assert
        Assert.False(isValid, "Previous step code must NOT be accepted after 60 seconds grace period.");
    }

    [Theory]
    [InlineData("999999")]
    [InlineData("000000")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidateCode_InvalidInputs_ReturnsFalse(string? invalidInput)
    {
        // Arrange
        var testTime = new DateTime(2026, 11, 5, 10, 15, 30, DateTimeKind.Utc);
        string actualCurrentCode = TotpPasswordHelper.GenerateCode(TestSecret, testTime);

        if (invalidInput == actualCurrentCode)
        {
            return; // skip if accidental collision
        }

        // Act
        bool isValid = TotpPasswordHelper.ValidateCode(TestSecret, invalidInput, testTime);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GetCurrentPasswordDetails_ReturnsAccurateRemainingSecondsAndTimestamps()
    {
        // Arrange: 10:12:00 -> 120 seconds into 300s window (remaining = 180s)
        var testTime = new DateTime(2026, 11, 5, 10, 12, 0, DateTimeKind.Utc);

        // Act
        var details = TotpPasswordHelper.GetCurrentPasswordDetails(TestSecret, testTime, stepSeconds: 300);

        // Assert
        Assert.NotNull(details.Password);
        Assert.Equal(6, details.Password.Length);
        Assert.Equal(180, details.RemainingSeconds);
        Assert.Equal(new DateTime(2026, 11, 5, 10, 10, 0, DateTimeKind.Utc), details.ValidFrom);
        Assert.Equal(new DateTime(2026, 11, 5, 10, 15, 0, DateTimeKind.Utc), details.ValidTo);
    }
}
