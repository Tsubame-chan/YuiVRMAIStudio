using NUnit.Framework;
using UnityEditor.iOS.Xcode;
using YuiPhysicalAI.Editor;
namespace YuiPhysicalAI.Tests.Editor
{
    public class YuiIOSBackendNetworkingTests
    {
        [Test]
        public void PrivateVpnHttpExceptionsKeepPublicHostsProtected()
        {
            var plist = new PlistDocument();
            var ats = plist.root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            YuiIOSBuildTools.ConfigureBackendNetworking(plist.root);
            Assert.IsFalse(ats.values.ContainsKey("NSAllowsArbitraryLoads"));
            Assert.IsTrue(ats["NSAllowsLocalNetworking"].AsBoolean());
            var ranges = ats["NSExceptionDomains"].AsDict();
            Assert.That(ranges.values.Count, Is.EqualTo(4));
            foreach(var range in new[] {"100.64.0.0/10", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"})
            {
                Assert.IsTrue(ranges[range].AsDict()["NSExceptionAllowsInsecureHTTPLoads"].AsBoolean());
                Assert.IsFalse(ranges[range].AsDict()["NSIncludesSubdomains"].AsBoolean());
            }
            YuiIOSBuildTools.ConfigureBackendNetworking(plist.root);
            Assert.That(ranges.values.Count, Is.EqualTo(4));
        }
    }
}
