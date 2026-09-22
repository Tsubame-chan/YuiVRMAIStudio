using System;

namespace Yui.AvatarBridge.Editor
{
    [Serializable]
    public sealed class YuiAvatarBridgeManifest
    {
        public int schemaVersion = 1;
        public string format = "unity-avatar-package";
        public string exporterVersion = "0.2.0";
        public string minimumYuiVersion = "0.2.0-beta.4";
        public string avatarId;
        public string displayName;
        public string unityVersion;
        public string createdUtc;
        public bool rightsAcknowledged;
        public string prefabAddress = "avatar/prefab";
        public YuiAvatarBundlePayload[] payloads = Array.Empty<YuiAvatarBundlePayload>();
        public YuiAvatarDiagnostics diagnostics = new YuiAvatarDiagnostics();
    }

    [Serializable]
    public sealed class YuiAvatarBundlePayload
    {
        public string platform;
        public string filename;
        public string sha256;
        public long sizeBytes;
    }

    [Serializable]
    public sealed class YuiAvatarDiagnostics
    {
        public bool hasVrcAvatarDescriptor;
        public bool hasHumanoidAnimator;
        public int skinnedMeshCount;
        public int materialCount;
        public int blendShapeCount;
        public YuiAvatarMaterialDiagnostic[] materials = Array.Empty<YuiAvatarMaterialDiagnostic>();
        public YuiAvatarVisemeMapping[] visemes = Array.Empty<YuiAvatarVisemeMapping>();
        public YuiAvatarExpressionClip[] expressionClips = Array.Empty<YuiAvatarExpressionClip>();
        public YuiAvatarPhysBoneMapping[] physBones = Array.Empty<YuiAvatarPhysBoneMapping>();
        public YuiAvatarDiagnosticIssue[] issues = Array.Empty<YuiAvatarDiagnosticIssue>();
    }

    [Serializable]
    public sealed class YuiAvatarVisemeMapping
    {
        public string vowel;
        public string rendererPath;
        public string blendShape;
        public bool found;
        public string source;
    }

    [Serializable]
    public sealed class YuiAvatarExpressionClip
    {
        public string name;
        public string category;
        public string emotion;
        public string address;
        public string assetGuid;
        public string sourcePath;
    }

    [Serializable]
    public sealed class YuiAvatarMaterialDiagnostic
    {
        public string name;
        public string shader;
        public bool shaderFound;
        public string assetGuid;
    }

    [Serializable]
    public sealed class YuiAvatarPhysBoneMapping
    {
        public string componentPath;
        public string rootPath;
        public int affectedTransformCount;
        public int colliderCount;
        public float pull;
        public float spring;
        public float stiffness;
        public float immobile;
        public float gravity;
        public float radius;
        public string[] ignorePaths = Array.Empty<string>();
        public string conversionStatus = "yui_secondary_motion_approximation";
    }

    [Serializable]
    public sealed class YuiAvatarDiagnosticIssue
    {
        public string severity;
        public string code;
        public string message;
    }
}
