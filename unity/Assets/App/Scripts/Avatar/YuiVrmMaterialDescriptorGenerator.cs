using System;
using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace YuiPhysicalAI.Avatar
{
    // These are the three branches of UniVRM's Built-in RP importer, including
    // migrated VRM0 and the glTF material-less default. Keep them in player builds.
    public sealed class YuiVrmMaterialDescriptorGenerator : IMaterialDescriptorGenerator
    {
        public static readonly string[] RequiredShaders = { "VRM10/MToon10", "UniGLTF/UniUnlit", "Standard" };
        private readonly BuiltInVrm10MaterialDescriptorGenerator inner = new BuiltInVrm10MaterialDescriptorGenerator();
        public MaterialDescriptor Get(GltfData data, int index) => Validate(inner.Get(data, index));
        public MaterialDescriptor GetGltfDefault(string materialName = null) => Validate(inner.GetGltfDefault(materialName));
        private static MaterialDescriptor Validate(MaterialDescriptor descriptor)
        {
            if (descriptor.Shader == null || !descriptor.Shader.isSupported)
                throw new InvalidOperationException("このアプリの描画機能が不足しています。アプリの更新が必要です。材質: " + descriptor.Name + " / " + Availability());
            return descriptor;
        }
        public static string Availability()
        {
            return string.Join(", ", Array.ConvertAll(RequiredShaders, name => {
                var shader = Shader.Find(name);
                return name + "=" + (shader == null ? "missing" : shader.isSupported ? "ready" : "unsupported");
            }));
        }
    }
}
