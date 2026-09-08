using NUnit.Framework;
using YuiPhysicalAI.EditorTools;
namespace YuiPhysicalAI.Tests.Editor {
 public sealed class YuiPublicBuildPrivacyGuardTests {
  [TestCase("Assets/App/Scripts/Avatar/Example.cs", true)]
  [TestCase("Assets/UnityChan/Models/example.fbx", true)]
  [TestCase("Packages/com.example/avatar.asset", true)]
  [TestCase("Assets/PrivateAvatar/avatar.prefab", false)]
  [TestCase("Assets/Resources/private.asset", false)]
  [TestCase("Assets/AppPrivate/avatar.asset", false)]
  public void AssetRootsAreExplicit(string path,bool approved) {
   Assert.AreEqual(approved,YuiPublicBuildPrivacyGuard.IsApprovedAssetPath(path));
  }
 }
}
