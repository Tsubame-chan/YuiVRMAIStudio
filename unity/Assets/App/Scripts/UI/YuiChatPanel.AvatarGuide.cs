using UnityEngine;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private void ShowAvatarImportGuide()
        {
            if (composerMenu != null) composerMenu.gameObject.SetActive(false);
            var root = CreateSavedDataPanel(YuiUiLocalization.Text("Bring your avatar"));
            SavedDataText(root, YuiUiTypography.JapaneseParagraph(AvatarImportInstructions()));
            ComposerButton(root, "Add", YuiUiLocalization.Text("Import avatar"), () => { Destroy(root.gameObject); ImportCustomVrmFromFilePicker(); }, .03f, .07f, .6f, .19f);
            ComposerButton(root, "Library", YuiUiLocalization.Text("My characters"), () => { Destroy(root.gameObject); ShowAvatarLibrary(); }, .64f, .07f, .97f, .19f);
        }
        public static string AvatarImportInstructions()
        {
            var platform = YuiAvatarPackageLoader.CurrentPlatform();
            var transfer = platform == "ios"
                ? YuiUiLocalization.Text("From Windows: save the VRM to iCloud Drive, then select it in Files on your iPhone. For USB, use Apple Devices file sharing. From Mac, AirDrop also works.")
                : platform == "android"
                ? YuiUiLocalization.Text("From PC: use USB file transfer to copy the VRM into Download, then select it in Yui.")
                : YuiUiLocalization.Text("Save the VRM on this PC. The same file also works on your phone.");
            return
                YuiUiLocalization.Text("Import a VRM\nOpen Settings → Character → Import avatar and choose your .vrm file. VRM 0.x and 1.0 are supported.\n\n")
                + YuiUiLocalization.Text("If your download is a ZIP, extract it first and choose the .vrm inside.\n\n")
                + YuiUiLocalization.Text("Need to create a VRM? (PC required)\n1. For a .unitypackage or FBX, set up the avatar in Unity following its author’s instructions. You can use your customized project.\n")
                + YuiUiLocalization.Text("2. Export a VRM with NDMF VRM Exporter or a compatible converter.\n")
                + "3. " + transfer + YuiUiLocalization.Text("\nIn Yui, choose Settings → Character → Import avatar.\n\n")
                + YuiUiLocalization.Text("Compatibility\nCheck appearance, lip sync, blinking and motion after import. Shading and physics may differ. VRChat menus, grab actions and custom animations are not reproduced.\n\n")
                + YuiUiLocalization.Text("To change outfits, open My characters → Change appearance. Select a saved avatar or a new file. The character keeps its name, personality, voice and memories.");
        }
    }
}
