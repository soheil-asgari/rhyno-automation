using Microsoft.ML.OnnxRuntime.Tensors;
using OfficeAutomation.Modules.MowadiyanIntegration;
using Xunit;

namespace OfficeAutomation.Tests;

/// <summary>
/// Unit tests for the in-process ddddocr CAPTCHA solver (<see cref="DdddCaptchaOcrClient"/>).
/// These cover the deterministic pure helpers (CTC decode + code cleaning) that do not
/// require loading the native ONNX runtime / model — keeping CI fast and hermetic.
/// </summary>
public class DdddCaptchaOcrClientTests
{
    [Theory]
    [InlineData("mrV5g", "mrV5g")]
    [InlineData("MRV5G", "MRV5G")]
    [InlineData("9B vvp\u00009", "9Bvvp9")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("abc-def_123", "abcdef123")]
    public void CleanCaptcha_KeepsOnlyAlphanumerics(string? input, string expected)
    {
        Assert.Equal(expected, DdddCaptchaOcrClient.CleanCaptcha(input));
    }

    [Fact]
    public void DecodeGreedyOcr_CollapsesRepeatsSkipsBlank()
    {
        // charset: index 0 = blank/space, so char at index i is charset[i-1].
        // Use a short known charset so indices are deterministic.
        const string charset = "abc";
        // sequence -> decode:
        //   [0 blank, 1->'a', 2->'b', 2 repeat(collapse), 0, 3->'c', 0, 1, 1 repeat]
        // expected: a, b, c, a
        var tensor = new DenseTensor<long>(new long[] { 0, 1, 2, 2, 0, 3, 0, 1, 1 }, new[] { 1, 9 });

        var result = DdddCaptchaOcrClient.DecodeGreedyOcr(tensor, new string(charset));

        Assert.Equal("abca", result);
    }

    [Fact]
    public void DecodeGreedyOcr_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DdddCaptchaOcrClient.DecodeGreedyOcr(new DenseTensor<long>(new long[0], new[] { 1, 0 }), "abc"));
    }
}
