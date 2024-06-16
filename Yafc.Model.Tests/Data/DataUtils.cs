using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace Yafc.Model.Data.Tests;

public class DataUtilsTests {
    public DataUtilsTests() {
        Project.current = new();
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void TryParseAmount_IsInverseOfFormatValue() {
        // Hammer the formatter and parser with lots of random but repeatable values, making sure TryParseAmount can correctly read anything FormatAmount generates.
        Random r = new Random(0);
        byte[] bytes = new byte[4];
        for (int i = 0; i < 1000; i++) {
            for (UnitOfMeasure unit = 0; unit < UnitOfMeasure.Celsius; unit++) {
                float value;
                int count = 1;
                do {
                    r.NextBytes(bytes);
                    value = BitConverter.ToSingle(bytes, 0);
                    count++;
                    // TryParseAmount refuses values above 1e15, and FormatAmount has large relative errors for tiny values. But 1e-7 is 1 item every 116 days, so we can ignore that.
                } while (MathF.Abs(value) is < 1e-7f or > 9.9e14f || float.IsNaN(value));

                Project.current.preferences.time = r.Next(4) switch {
                    0 => 1,
                    1 => 60,
                    2 => 3600,
                    3 => r.Next(100000),
                    _ => throw new Exception("r.Next returned an out of range value.") // Can't happen, but it suppresses the warning.
                };

                string formattedValue = DataUtils.FormatAmount(value, unit, precise: true);
                Assert.True(DataUtils.TryParseAmount(formattedValue, out float parsedValue, unit), $"Could not parse {value} after being formatted precisely as {formattedValue}.");
                Assert.True(Math.Abs(value - parsedValue) <= Math.Abs(value * .00001), $"Incorrectly parsed {value}, formatted precisely as {formattedValue}, to {parsedValue}.");

                formattedValue = DataUtils.FormatAmount(value, unit);
                Assert.True(DataUtils.TryParseAmount(formattedValue, out parsedValue, unit), $"Could not parse {value} after being formatted as {formattedValue}.");
                // Even within the allowed range, imprecise formatting is extra imprecise when the value is just over a power of 10; 0.0010209 is formatted as "0.001"
                // This can't exceed a 5% error: 0.00104999 would also round down to 0.001, but .00105 rounds up.
                Assert.True(Math.Abs(value - parsedValue) <= Math.Abs(value * .05), $"Incorrectly parsed {value}, formatted as {formattedValue}, to {parsedValue}.");
            }
        }
    }

    [Fact]
    public void TryParseAmount_IsInverseOfFormatValue_WithBeltsAndPipes() {
        // Hammer the formatter and parser with lots of random but repeatable values, making sure TryParseAmount can correctly read anything FormatAmount generates.
        // This time, include b and p suffixes. These suffixes noticably reduce precision, so do them separately.
        Random r = new Random(0);
        byte[] bytes = new byte[4];
        for (int i = 0; i < 1000; i++) {
            for (UnitOfMeasure unit = 0; unit < UnitOfMeasure.Celsius; unit++) {
                float value;
                int count = 1;
                do {
                    r.NextBytes(bytes);
                    value = BitConverter.ToSingle(bytes, 0);
                    count++;
                    // TryParseAmount refuses values above 1e15, and FormatAmount has large relative errors for tiny values. But 1e-7 is 1 item every 116 days, so we can ignore that.
                } while (MathF.Abs(value) is < 1e-7f or > 9.9e14f || float.IsNaN(value));

                Project.current.preferences.itemUnit = r.Next(6) switch {
                    0 or 1 => 0,
                    int x => (x - 1) * 15
                };
                Project.current.preferences.fluidUnit = r.Next(6) switch {
                    0 or 1 => 0,
                    int x => (x - 1) * 60
                };

                string formattedValue = DataUtils.FormatAmount(value, unit, precise: true);
                Assert.True(DataUtils.TryParseAmount(formattedValue, out float parsedValue, unit), $"Could not parse {value} after being formatted precisely as {formattedValue}.");
                // Precise formatting loses a lot of precision when formatting 'N belts' or 'N pipes'.
                Assert.True(Math.Abs(value - parsedValue) <= Math.Abs(value * .001), $"Incorrectly parsed {value}, formatted precisely as {formattedValue}, to {parsedValue}.");

                formattedValue = DataUtils.FormatAmount(value, unit);
                // Skip testing if the formatted value is less than 0.1μ; we've lost too much precision for this to be meaningful.
                if (((!formattedValue.StartsWith("-0.0") && !formattedValue.StartsWith("0.0")) || !formattedValue.Contains('μ')) && !formattedValue.StartsWith("0μ") && !formattedValue.StartsWith("-0μ")) {
                    Assert.True(DataUtils.TryParseAmount(formattedValue, out parsedValue, unit), $"Could not parse {value} after being formatted as {formattedValue}.");
                    // Allow slightly more rounding error when parsing imprecise belt and pipe counts.
                    Assert.True(Math.Abs(value - parsedValue) <= Math.Abs(value * .06), $"Incorrectly parsed {value}, formatted as {formattedValue}, to {parsedValue}.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TryParseAmount_TestData))]
    public void TryParseAmount_WhenGivenInputs_ShouldProduceCorrectValues(string input, UnitOfMeasure unitOfMeasure, bool expectedReturn, float expectedOutput, int time, int itemUnit, int fluidUnit, int defaultBeltSpeed) {
        Project.current.preferences.time = time;
        Project.current.preferences.itemUnit = itemUnit;
        Project.current.preferences.fluidUnit = fluidUnit;
        Project.current.preferences.defaultBelt ??= new();
        typeof(EntityBelt).GetProperty(nameof(EntityBelt.beltItemsPerSecond)).SetValue(Project.current.preferences.defaultBelt, defaultBeltSpeed);

        Assert.Equal(expectedReturn, DataUtils.TryParseAmount(input, out float result, unitOfMeasure));
        if (expectedReturn) {
            double error = (result - expectedOutput) / (double)expectedOutput;
            Assert.True(Math.Abs(error) < .00001, $"Parsing {input} produced {result}, which differs from the expected {expectedOutput} by {error:0.00%}.");
        }
    }

    public static object[][] TryParseAmount_TestData => [
        new DataItem("1", UnitOfMeasure.None, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.None, true, 100),
        new DataItem("-10e-2k", UnitOfMeasure.None, true, -100),
        new DataItem(".1e2u", UnitOfMeasure.None, true, .00001f),
        new DataItem("10%", UnitOfMeasure.None, false),
        new DataItem("10j", UnitOfMeasure.None, false),
        new DataItem("10 M", UnitOfMeasure.None, true, 10000000),
        new DataItem("10/t", UnitOfMeasure.None, false),
        new DataItem("10u/s", UnitOfMeasure.None, false),
        new DataItem("10m/m", UnitOfMeasure.None, false),
        new DataItem("10g/h", UnitOfMeasure.None, false),
        new DataItem("10/ks", UnitOfMeasure.None, false),
        new DataItem("10 s", UnitOfMeasure.None, false),
        new DataItem("10 b", UnitOfMeasure.None, false),
        new DataItem("10 p", UnitOfMeasure.None, false),

        new DataItem("1", UnitOfMeasure.Percent, true, .01f),
        new DataItem("10e-2k", UnitOfMeasure.Percent, true, 1),
        new DataItem("-10e-2k", UnitOfMeasure.Percent, true, -1),
        new DataItem(".1e2u", UnitOfMeasure.Percent, true, .0000001f),
        new DataItem("10%", UnitOfMeasure.Percent, true, .1f),
        new DataItem("10j", UnitOfMeasure.Percent, false),
        new DataItem("10 M", UnitOfMeasure.Percent, true, 100000),
        new DataItem("10/t", UnitOfMeasure.Percent, false),
        new DataItem("10u/s", UnitOfMeasure.Percent, false),
        new DataItem("10m/m", UnitOfMeasure.Percent, false),
        new DataItem("10g/h", UnitOfMeasure.Percent, false),
        new DataItem("10/ks", UnitOfMeasure.Percent, false),
        new DataItem("10 s", UnitOfMeasure.Percent, false),
        new DataItem("10 b", UnitOfMeasure.Percent, false),
        new DataItem("10 p", UnitOfMeasure.Percent, false),

        new DataItem("1", UnitOfMeasure.Second, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.Second, true, 100),
        new DataItem("-10e-2k", UnitOfMeasure.Second, true, -100),
        new DataItem(".1e2u", UnitOfMeasure.Second, true, .00001f),
        new DataItem("10%", UnitOfMeasure.Second, false),
        new DataItem("10j", UnitOfMeasure.Second, false),
        new DataItem("10 M", UnitOfMeasure.Second, true, 10000000),
        new DataItem("10/t", UnitOfMeasure.Second, false),
        new DataItem("10u/s", UnitOfMeasure.Second, false),
        new DataItem("10m/m", UnitOfMeasure.Second, false),
        new DataItem("10g/h", UnitOfMeasure.Second, false),
        new DataItem("10/ks", UnitOfMeasure.Second, false),
        new DataItem("10 s", UnitOfMeasure.Second, true, 10),
        new DataItem("10 b", UnitOfMeasure.Second, false),
        new DataItem("10 p", UnitOfMeasure.Second, false),

        new DataItem("1", UnitOfMeasure.PerSecond, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.PerSecond, true, 100),
        new DataItem("-10e-2k", UnitOfMeasure.PerSecond, true, -100),
        new DataItem(".1e2u", UnitOfMeasure.PerSecond, true, .00001f),
        new DataItem("10%", UnitOfMeasure.PerSecond, false),
        new DataItem("10j", UnitOfMeasure.PerSecond, false),
        new DataItem("10 M", UnitOfMeasure.PerSecond, true, 10000000),
        new DataItem("10/t", UnitOfMeasure.PerSecond, true, 10),
        new DataItem("10u/s", UnitOfMeasure.PerSecond, true, .00001f),
        new DataItem("10m/m", UnitOfMeasure.PerSecond, true, 166666.6666666667f),
        new DataItem("10g/h", UnitOfMeasure.PerSecond, true, 2777777.777777778f),
        new DataItem("10/ks", UnitOfMeasure.PerSecond, false),
        new DataItem("10 s", UnitOfMeasure.PerSecond, false),
        new DataItem("10 b", UnitOfMeasure.PerSecond, false),
        new DataItem("10 p", UnitOfMeasure.PerSecond, false),

        new DataItem("1", UnitOfMeasure.PerSecond, true, 1f/30, time: 30),
        new DataItem("10e-2k", UnitOfMeasure.PerSecond, true, 100f/30, time: 30),
        new DataItem("-10e-2k", UnitOfMeasure.PerSecond, true, -100f/30, time: 30),
        new DataItem(".1e2u", UnitOfMeasure.PerSecond, true, .00001f/30, time: 30),
        new DataItem("10%", UnitOfMeasure.PerSecond, false, time: 30),
        new DataItem("10j", UnitOfMeasure.PerSecond, false, time: 30),
        new DataItem("10 M", UnitOfMeasure.PerSecond, true, 10000000f/30, time: 30),
        new DataItem("10/t", UnitOfMeasure.PerSecond, true, 10f/30, time: 30),
        new DataItem("10u/s", UnitOfMeasure.PerSecond, true, .00001f, time: 30),
        new DataItem("10m/m", UnitOfMeasure.PerSecond, true, 166666.6666666667f, time: 30),
        new DataItem("10g/h", UnitOfMeasure.PerSecond, true, 2777777.777777778f, time: 30),
        new DataItem("10/ks", UnitOfMeasure.PerSecond, false, time: 30),
        new DataItem("10 s", UnitOfMeasure.PerSecond, false, time: 30),
        new DataItem("10 b", UnitOfMeasure.PerSecond, false, time: 30),
        new DataItem("10 p", UnitOfMeasure.PerSecond, false, time: 30),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 100),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -100),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00001f),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 10000000),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 150),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 1f/30, time: 30),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 100f/30, time: 30),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -100f/30, time: 30),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00001f/30, time: 30),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, time: 30),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, time: 30),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 10000000f/30, time: 30),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10f/30, time: 30),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, time: 30),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, time: 30),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, time: 30),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, time: 30),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, time: 30),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 150, time: 30),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, time: 30),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 1, defaultBeltSpeed: 45),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 100, defaultBeltSpeed: 45),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -100, defaultBeltSpeed: 45),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00001f, defaultBeltSpeed: 45),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 10000000, defaultBeltSpeed: 45),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10, defaultBeltSpeed: 45),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, defaultBeltSpeed: 45),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, defaultBeltSpeed: 45),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, defaultBeltSpeed: 45),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 450, defaultBeltSpeed: 45),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 1f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 100f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -100f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00001f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 10000000f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10f/30, time: 30, defaultBeltSpeed: 45),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, time: 30, defaultBeltSpeed: 45),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, time: 30, defaultBeltSpeed: 45),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, time: 30, defaultBeltSpeed: 45),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 450, time: 30, defaultBeltSpeed: 45),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 22, itemUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 2200, itemUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -2200, itemUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00022f, itemUnit: 22),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, itemUnit: 22),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, itemUnit: 22),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 220000000, itemUnit: 22),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10, itemUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, itemUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, itemUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, itemUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, itemUnit: 22),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, itemUnit: 22),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 220, itemUnit: 22),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, itemUnit: 22),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 22, time: 30, itemUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 2200, time: 30, itemUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -2200, time: 30, itemUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00022f, time: 30, itemUnit: 22),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, time: 30, itemUnit: 22),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, time: 30, itemUnit: 22),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 220000000f, time: 30, itemUnit: 22),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10f/30, time: 30, itemUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, time: 30, itemUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, time: 30, itemUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, time: 30, itemUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, time: 30, itemUnit: 22),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, time: 30, itemUnit: 22),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 220, time: 30, itemUnit: 22),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, time: 30, itemUnit: 22),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 22, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 2200, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -2200, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00022f, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 220000000, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 220, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, defaultBeltSpeed: 45, itemUnit: 22),

        new DataItem("1", UnitOfMeasure.ItemPerSecond, true, 22, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.ItemPerSecond, true, 2200, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.ItemPerSecond, true, -2200, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.ItemPerSecond, true, .00022f, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10%", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10j", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 M", UnitOfMeasure.ItemPerSecond, true, 220000000f, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10/t", UnitOfMeasure.ItemPerSecond, true, 10f/30, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.ItemPerSecond, true, .00001f, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.ItemPerSecond, true, 166666.6666666667f, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.ItemPerSecond, true, 2777777.777777778f, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 s", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 b", UnitOfMeasure.ItemPerSecond, true, 220, time: 30, defaultBeltSpeed: 45, itemUnit: 22),
        new DataItem("10 p", UnitOfMeasure.ItemPerSecond, false, time: 30, defaultBeltSpeed: 45, itemUnit: 22),

        new DataItem("1", UnitOfMeasure.FluidPerSecond, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.FluidPerSecond, true, 100),
        new DataItem("-10e-2k", UnitOfMeasure.FluidPerSecond, true, -100),
        new DataItem(".1e2u", UnitOfMeasure.FluidPerSecond, true, .00001f),
        new DataItem("10%", UnitOfMeasure.FluidPerSecond, false),
        new DataItem("10j", UnitOfMeasure.FluidPerSecond, false),
        new DataItem("10 M", UnitOfMeasure.FluidPerSecond, true, 10000000),
        new DataItem("10/t", UnitOfMeasure.FluidPerSecond, true, 10),
        new DataItem("10u/s", UnitOfMeasure.FluidPerSecond, true, .00001f),
        new DataItem("10m/m", UnitOfMeasure.FluidPerSecond, true, 166666.6666666667f),
        new DataItem("10g/h", UnitOfMeasure.FluidPerSecond, true, 2777777.777777778f),
        new DataItem("10/ks", UnitOfMeasure.FluidPerSecond, false),
        new DataItem("10 s", UnitOfMeasure.FluidPerSecond, false),
        new DataItem("10 b", UnitOfMeasure.FluidPerSecond, false),
        new DataItem("10 p", UnitOfMeasure.FluidPerSecond, false),

        new DataItem("1", UnitOfMeasure.FluidPerSecond, true, 1f/30, time: 30),
        new DataItem("10e-2k", UnitOfMeasure.FluidPerSecond, true, 100f/30, time: 30),
        new DataItem("-10e-2k", UnitOfMeasure.FluidPerSecond, true, -100f/30, time: 30),
        new DataItem(".1e2u", UnitOfMeasure.FluidPerSecond, true, .00001f/30, time: 30),
        new DataItem("10%", UnitOfMeasure.FluidPerSecond, false, time: 30),
        new DataItem("10j", UnitOfMeasure.FluidPerSecond, false, time: 30),
        new DataItem("10 M", UnitOfMeasure.FluidPerSecond, true, 10000000f/30, time: 30),
        new DataItem("10/t", UnitOfMeasure.FluidPerSecond, true, 10f/30, time: 30),
        new DataItem("10u/s", UnitOfMeasure.FluidPerSecond, true, .00001f, time: 30),
        new DataItem("10m/m", UnitOfMeasure.FluidPerSecond, true, 166666.6666666667f, time: 30),
        new DataItem("10g/h", UnitOfMeasure.FluidPerSecond, true, 2777777.777777778f, time: 30),
        new DataItem("10/ks", UnitOfMeasure.FluidPerSecond, false, time: 30),
        new DataItem("10 s", UnitOfMeasure.FluidPerSecond, false, time: 30),
        new DataItem("10 b", UnitOfMeasure.FluidPerSecond, false, time: 30),
        new DataItem("10 p", UnitOfMeasure.FluidPerSecond, false, time: 30),

        new DataItem("1", UnitOfMeasure.FluidPerSecond, true, 22, fluidUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.FluidPerSecond, true, 2200, fluidUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.FluidPerSecond, true, -2200, fluidUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.FluidPerSecond, true, .00022f, fluidUnit: 22),
        new DataItem("10%", UnitOfMeasure.FluidPerSecond, false, fluidUnit: 22),
        new DataItem("10j", UnitOfMeasure.FluidPerSecond, false, fluidUnit: 22),
        new DataItem("10 M", UnitOfMeasure.FluidPerSecond, true, 220000000, fluidUnit: 22),
        new DataItem("10/t", UnitOfMeasure.FluidPerSecond, true, 10, fluidUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.FluidPerSecond, true, .00001f, fluidUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.FluidPerSecond, true, 166666.6666666667f, fluidUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.FluidPerSecond, true, 2777777.777777778f, fluidUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.FluidPerSecond, false, fluidUnit: 22),
        new DataItem("10 s", UnitOfMeasure.FluidPerSecond, false, fluidUnit: 22),
        new DataItem("10 b", UnitOfMeasure.FluidPerSecond, false, fluidUnit: 22),
        new DataItem("10 p", UnitOfMeasure.FluidPerSecond, true, 220, fluidUnit: 22),

        new DataItem("1", UnitOfMeasure.FluidPerSecond, true, 22, time: 30, fluidUnit: 22),
        new DataItem("10e-2k", UnitOfMeasure.FluidPerSecond, true, 2200, time: 30, fluidUnit: 22),
        new DataItem("-10e-2k", UnitOfMeasure.FluidPerSecond, true, -2200, time: 30, fluidUnit: 22),
        new DataItem(".1e2u", UnitOfMeasure.FluidPerSecond, true, .00022f, time: 30, fluidUnit: 22),
        new DataItem("10%", UnitOfMeasure.FluidPerSecond, false, time: 30, fluidUnit: 22),
        new DataItem("10j", UnitOfMeasure.FluidPerSecond, false, time: 30, fluidUnit: 22),
        new DataItem("10 M", UnitOfMeasure.FluidPerSecond, true, 220000000f, time: 30, fluidUnit: 22),
        new DataItem("10/t", UnitOfMeasure.FluidPerSecond, true, 10f/30, time: 30, fluidUnit: 22),
        new DataItem("10u/s", UnitOfMeasure.FluidPerSecond, true, .00001f, time: 30, fluidUnit: 22),
        new DataItem("10m/m", UnitOfMeasure.FluidPerSecond, true, 166666.6666666667f, time: 30, fluidUnit: 22),
        new DataItem("10g/h", UnitOfMeasure.FluidPerSecond, true, 2777777.777777778f, time: 30, fluidUnit: 22),
        new DataItem("10/ks", UnitOfMeasure.FluidPerSecond, false, time: 30, fluidUnit: 22),
        new DataItem("10 s", UnitOfMeasure.FluidPerSecond, false, time: 30, fluidUnit: 22),
        new DataItem("10 b", UnitOfMeasure.FluidPerSecond, false, time: 30, fluidUnit: 22),
        new DataItem("10 p", UnitOfMeasure.FluidPerSecond, true, 220, time: 30, fluidUnit: 22),

        new DataItem("1", UnitOfMeasure.Megawatt, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.Megawatt, true, .0001f),
        new DataItem("-10e-2k", UnitOfMeasure.Megawatt, true, -.0001f),
        new DataItem(".1e2u", UnitOfMeasure.Megawatt, true, .00000000001f),
        new DataItem("10%", UnitOfMeasure.Megawatt, false),
        new DataItem("10w", UnitOfMeasure.Megawatt, true, 10e-6f),
        new DataItem("10 M", UnitOfMeasure.Megawatt, true, 10),
        new DataItem("10/t", UnitOfMeasure.Megawatt, false),
        new DataItem("10u/s", UnitOfMeasure.Megawatt, false),
        new DataItem("10m/m", UnitOfMeasure.Megawatt, false),
        new DataItem("10g/h", UnitOfMeasure.Megawatt, false),
        new DataItem("10/ks", UnitOfMeasure.Megawatt, false),
        new DataItem("10 s", UnitOfMeasure.Megawatt, false),
        new DataItem("10 b", UnitOfMeasure.Megawatt, false),
        new DataItem("10 p", UnitOfMeasure.Megawatt, false),

        new DataItem("1", UnitOfMeasure.Megajoule, true, 1),
        new DataItem("10e-2k", UnitOfMeasure.Megajoule, true, .0001f),
        new DataItem("-10e-2k", UnitOfMeasure.Megajoule, true, -.0001f),
        new DataItem(".1e2u", UnitOfMeasure.Megajoule, true, .00000000001f),
        new DataItem("10%", UnitOfMeasure.Megajoule, false),
        new DataItem("10j", UnitOfMeasure.Megajoule, true, 10e-6f),
        new DataItem("10 M", UnitOfMeasure.Megajoule, true, 10),
        new DataItem("10/t", UnitOfMeasure.Megajoule, false),
        new DataItem("10u/s", UnitOfMeasure.Megajoule, false),
        new DataItem("10m/m", UnitOfMeasure.Megajoule, false),
        new DataItem("10g/h", UnitOfMeasure.Megajoule, false),
        new DataItem("10/ks", UnitOfMeasure.Megajoule, false),
        new DataItem("10 s", UnitOfMeasure.Megajoule, false),
        new DataItem("10 b", UnitOfMeasure.Megajoule, false),
        new DataItem("10 p", UnitOfMeasure.Megajoule, false),
    ];

    private class DataItem(string input, UnitOfMeasure unit, bool expectedReturn, float expectedOutput = 0, int time = 1, int itemUnit = 0, int fluidUnit = 0, int defaultBeltSpeed = 15) {
        private object[] ToArray() => [input, unit, expectedReturn, expectedOutput, time, itemUnit, fluidUnit, defaultBeltSpeed];
        public static implicit operator object[](DataItem item) => item.ToArray();
    }
}
