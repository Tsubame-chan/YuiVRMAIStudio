using System.Reflection;
using NUnit.Framework;
using YuiPhysicalAI.Editor;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiReleaseVersionTests
    {
        [Test]
        public void BuildDefaultsReadShippedMetadata()
        {
            Assert.That(YuiReleaseVersionMetadata.Read("desktop_tag"), Does.StartWith("v"));
            Assert.That(YuiReleaseVersionMetadata.Read("ios_version"), Does.Match(@"^[0-9]+\.[0-9]+\.[0-9]+$"));
            Assert.That(YuiReleaseVersionMetadata.Read("ios_build"), Does.Match(@"^[1-9][0-9]*$"));
            var previous = System.Environment.GetEnvironmentVariable("YUI_RELEASE_VERSION");
            try
            {
                System.Environment.SetEnvironmentVariable("YUI_RELEASE_VERSION", null);
                var method = typeof(YuiPublicWindowsBuildTools).GetMethod("PublicBuildVersionTag", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.AreEqual(YuiReleaseVersionMetadata.Read("desktop_tag"), method.Invoke(null, null));
            }
            finally { System.Environment.SetEnvironmentVariable("YUI_RELEASE_VERSION", previous); }
        }
    }
}
