using System.Globalization;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Yafc.Model.Tests;

[assembly: TestFramework("Yafc.Model.Tests." + nameof(PrepareForTests), "Yafc.Model.Tests")]

namespace Yafc.Model.Tests;
public class PrepareForTests : XunitTestFramework {
    public PrepareForTests(IMessageSink messageSink)
        : base(messageSink) {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
