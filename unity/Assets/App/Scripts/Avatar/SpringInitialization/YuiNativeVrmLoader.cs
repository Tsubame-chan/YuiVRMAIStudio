using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    // Keep UniVRM's non-auto-referenced utility assembly behind this assembly boundary.
    public static class YuiNativeVrmLoader
    {
        public static Task<Vrm10Instance> LoadPathAsync(string path, bool canLoadVrm0X=true,
            ControlRigGenerationOption controlRigGenerationOption=ControlRigGenerationOption.None,
            bool showMeshes=true, bool immediate=false, IMaterialDescriptorGenerator materialGenerator=null,
            Vrm10.VrmMetaInformationCallback vrmMetaInformationCallback=null, CancellationToken ct=default)
        {
            return Vrm10.LoadPathAsync(path,canLoadVrm0X:canLoadVrm0X,controlRigGenerationOption:controlRigGenerationOption,
                showMeshes:showMeshes,awaitCaller:immediate?(IAwaitCaller)new ImmediateCaller():new RuntimeOnlyAwaitCaller(),
                materialGenerator:materialGenerator,vrmMetaInformationCallback:vrmMetaInformationCallback,ct:ct);
        }
        public static Task NextFrameAsync()=>new RuntimeOnlyAwaitCaller().NextFrame();
    }
}
