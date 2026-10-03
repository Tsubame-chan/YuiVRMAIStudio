using NUnit.Framework;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Api;
namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiBackendAddressPolicyTests
    {
        [TestCase(null)] [TestCase("http://127.0.0.1:8000")]
        [TestCase("http://localhost:8000")] [TestCase("http://[::1]:8000")]
        public void MobileDoesNotUseDeviceLoopback(string saved)
        {
            Assert.That(YuiBackendAddressPolicy.Resolve(saved, YuiBackendAddressPolicy.DesktopDefault, true), Is.Empty);
        }
        [Test]
        public void PreserveSelectedVpnAndDesktopDefaults()
        {
            var url = "http://100.64.0.9:8000";
            Assert.That(YuiBackendAddressPolicy.Resolve(url, YuiBackendAddressPolicy.DesktopDefault, true), Is.EqualTo(url));
            Assert.That(YuiBackendAddressPolicy.Resolve(null, YuiBackendAddressPolicy.DesktopDefault, false), Is.EqualTo(YuiBackendAddressPolicy.DesktopDefault));
            Assert.That(YuiBackendAddressPolicy.Resolve("", YuiBackendAddressPolicy.DesktopDefault, true), Is.Empty);
        }
        [Test]
        public void UnconfiguredClientCanBeCreatedButNeverSends()
        {
            var client = new YuiBackendClient("", allowUnconfigured: true);
            Assert.That(client.BaseUrl, Is.Empty);
            Assert.ThrowsAsync<System.InvalidOperationException>(async () => await client.GetHealthAsync());
            Assert.Throws<System.ArgumentException>(() => new YuiBackendClient(""));
        }
    }
}
