using Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace Framework.Editor
{
    /// <summary>
    /// File → Build 之前自动做两件事：
    /// 1. 把 AppCoreConfig.resourceMode 临时改成 OfflinePlayMode（打包后必须用它，EditorSimulateMode 在包里无效）
    /// 2. 用 YooAsset 构建资源包，并把首包拷进 StreamingAssets
    /// 构建结束后再把 resourceMode 改回 1，编辑器里继续用模拟模式。
    /// </summary>
    public class PreBuildYooAsset : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string PackageName = "DefaultPackage";
        private const string PackageVersion = "1.0";
        private const string ConfigPath = "Assets/Resources/AppCoreConfig.asset";

        private const int ResourceModeEditorSimulate = 1;
        private const int ResourceModeOfflinePlay = 2;

        public int callbackOrder => 0;

        #region 构建前

        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            SetResourceMode(ResourceModeOfflinePlay);

            // 资源包不能在 OnPreprocessBuild 里构建：Unity 已在构建流程中，
            // YooAsset 的 TaskPrepare_SBP 会失败。这里只检查有没有构建过。
            string root = BundleBuilderHelper.GetStreamingAssetsRoot();
            bool hasBundle = System.IO.Directory.Exists(root)
                && System.IO.Directory.GetFiles(root, "*.bytes", System.IO.SearchOption.AllDirectories).Length > 0;

            if (!hasBundle)
            {
                throw new BuildFailedException(
                    "[PreBuild] 还没有资源包。请先执行菜单 Tools/打包用：构建资源包 + 切 OfflinePlayMode，再 File → Build");
            }

            Debug.Log($"[PreBuild] 资源包已就绪：{root}");
        }

        /// <summary>
        /// 用 ScriptableBuildPipeline 构建资源包，并清空后拷贝到 StreamingAssets
        /// </summary>
        private static bool BuildBundles(BuildTarget target)
        {
            Debug.Log($"[PreBuild] 开始构建资源包：{PackageName} / {target}");

            var parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = PackageName,
                PackageVersion = PackageVersion,
                EnableSharePackRule = true,
                VerifyBuildingResult = false,
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = EBundledCopyOption.ClearAndCopyAll,
                BundledCopyParams = string.Empty,
                CompressOption = ECompressOption.LZ4,
                ClearBuildCacheFiles = true,
                UseAssetDependencyDB = true,
            };

            var pipeline = new ScriptableBuildPipeline();
            YooAsset.Editor.BuildResult result = pipeline.Run(parameters, false);

            if (!result.Success)
            {
                Debug.LogError($"[PreBuild] 构建失败：{result.FailedTask}\n{result.ErrorInfo}\n{result.ErrorStack}");
                return false;
            }

            Debug.Log($"[PreBuild] 资源包构建完成：{result.OutputPackageDirectory}\n" +
                      $"[PreBuild] 首包已拷到 {parameters.BundledFileRoot}");
            return true;
        }

        #endregion

        #region 构建后

        public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            // 编辑器里的开发体验要回到模拟模式，否则改资产不生效
            SetResourceMode(ResourceModeEditorSimulate);
            AssetDatabase.SaveAssets();
        }

        private static void SetResourceMode(int mode)
        {
            var config = AssetDatabase.LoadAssetAtPath<AppCoreConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[PreBuild] 找不到 {ConfigPath}");
                return;
            }

            var so = new SerializedObject(config);
            SerializedProperty prop = so.FindProperty("resourceMode");
            if (prop == null)
            {
                Debug.LogError("[PreBuild] AppCoreConfig 上没有 resourceMode 字段");
                return;
            }

            if (prop.intValue == mode) return;

            prop.intValue = mode;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PreBuild] resourceMode -> {mode}");
        }

        #endregion

        #region 手动入口

        [MenuItem("Tools/打包用：构建资源包 + 切 OfflinePlayMode")]
        private static void BuildBundlesManual()
        {
            SetResourceMode(ResourceModeOfflinePlay);
            bool ok = BuildBundles(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log(ok ? "[PreBuild] 手动构建资源包完成，现在可以 File → Build 出包" : "[PreBuild] 手动构建失败");
        }

        [MenuItem("Tools/打包用：切回 EditorSimulateMode")]
        private static void RestoreEditorMode()
        {
            SetResourceMode(ResourceModeEditorSimulate);
        }

        #endregion
    }
}
