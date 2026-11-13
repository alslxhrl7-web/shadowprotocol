using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>메뉴 ShadowProtocol/Build Windows → Builds/Windows/ShadowProtocol.exe</summary>
    public static class ShadowProtocolBuild
    {
        public const string OutputPath = "Builds/Windows/ShadowProtocol.exe";

        [MenuItem("ShadowProtocol/Build Windows")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ShadowProtocol] 플레이 모드에서는 빌드할 수 없습니다.");
                return;
            }
            PlayerSettings.productName = "ShadowProtocol";
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ShadowProtocolSceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string line = $"[ShadowProtocol] Windows 빌드 {s.result}: {s.outputPath}  크기 {s.totalSize / (1024f * 1024f):0.0}MB  오류 {s.totalErrors}  경고 {s.totalWarnings}  시간 {s.totalTime.TotalSeconds:0}s";
            if (s.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
        }
    }
}
