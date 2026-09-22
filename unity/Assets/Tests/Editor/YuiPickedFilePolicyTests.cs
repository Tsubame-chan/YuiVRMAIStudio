using System;
using System.IO;
using NUnit.Framework;
using YuiPhysicalAI.Platform;

public class YuiPickedFilePolicyTests
{
    private string directory;
    [SetUp] public void Setup() { directory=Path.Combine(Path.GetTempPath(),"yui-picker-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory); }
    [TearDown] public void Teardown() { Directory.Delete(directory,true); }
    private string FileWith(string name) { var path=Path.Combine(directory,name);File.WriteAllText(path,"fixture");return path; }
    [Test] public void ProviderFilterCannotMakeOtherDocumentsValidAvatars()
    {
        Assert.IsNull(YuiPickedFilePolicy.Validate(FileWith("日本語 アバター.VRM"),YuiFilePurpose.Avatar));
        Assert.IsNull(YuiPickedFilePolicy.Validate(FileWith("bridge.zip"),YuiFilePurpose.Avatar));
        Assert.IsNotNull(YuiPickedFilePolicy.Validate(FileWith("bought.unitypackage"),YuiFilePurpose.Avatar));
        Assert.IsNotNull(YuiPickedFilePolicy.Validate(FileWith("photo.png"),YuiFilePurpose.Avatar));
        Assert.IsNotNull(YuiPickedFilePolicy.Validate(FileWith("bridge.zip"),YuiFilePurpose.Vrm));
        Assert.IsNotNull(YuiPickedFilePolicy.Validate(FileWith("renamed.jpg.exe"),YuiFilePurpose.Image));
    }
    [Test] public void EmptyMissingAndOversizedFilesHaveActionableErrors()
    {
        var path=FileWith("photo.jpg");
        using(var stream=File.OpenWrite(path)) stream.SetLength(0);
        StringAssert.Contains("empty",YuiPickedFilePolicy.Validate(path,YuiFilePurpose.Image));
        using(var stream=File.OpenWrite(path)) stream.SetLength(YuiPickedFilePolicy.MaxImageBytes+1);
        StringAssert.Contains("64 MB",YuiPickedFilePolicy.Validate(path,YuiFilePurpose.Image));
        File.Delete(path);Assert.IsNotNull(YuiPickedFilePolicy.Validate(path,YuiFilePurpose.Image));
    }
    [Test] public void CleanupOnlyRemovesPickerOwnedCopies()
    {
        var original=FileWith("avatar.vrm");var copy=FileWith("copy.vrm");
        new YuiFilePicker.Result(true,original,null).Dispose();
        Assert.IsTrue(File.Exists(original));
        new YuiFilePicker.Result(true,copy,null,true).Dispose();
        Assert.IsFalse(File.Exists(copy));
    }
}
