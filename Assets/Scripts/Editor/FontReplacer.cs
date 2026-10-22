#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Framework.Editor
{
    /// <summary>
    /// 字体替换工具：把工程里 TMP 使用的字体统一换成指定字体。
    /// <para>
    /// 背景：原来的 AaHouDiHei-2（厚底黑）商用授权有问题，需要换成
    /// LogoSCUnboundedSans-Regular-2。TMP 字体资产（图集 / 字形表 / 材质）
    /// 只能在 Editor 里生成，所以用菜单一次做完：
    /// 生成字体资产 → 设为 TMP 默认字体 → 替换所有预制体里的文本组件。
    /// </para>
    /// 菜单：Tools/聚光灯/字体/① 生成 TMP 字体资产并全量替换
    ///      Tools/聚光灯/字体/② 检查旧字体残留引用
    /// </summary>
    public static class FontReplacer
    {
        /// <summary>要替换掉的老字体（厚底黑）资产 guid，用于残留检查</summary>
        private const string OldFontGuid = "47443405f0ee3eb4aadee19e6e390539";

        /// <summary>新字体 ttf</summary>
        private const string NewTtfPath = "Assets/Art/Fonts/LogoSCUnboundedSans-Regular-2.ttf";

        /// <summary>生成的 TMP 字体资产路径</summary>
        private const string NewFontAssetPath = "Assets/Art/Fonts/LogoSCUnboundedSans SDF.asset";

        /// <summary>要扫描的预制体根目录</summary>
        private static readonly string[] PrefabRoots = { "Assets/Prefabs" };

        private const int SamplingPointSize = 60;   // 中文至少 40，60 更稳
        private const int AtlasPadding = 6;         // ≈ 点大小 × 0.1
        private const int AtlasSize = 1024;         // 动态字体按需扩展，起始给 1024 即可

        [MenuItem("Tools/聚光灯/字体/① 生成 TMP 字体资产并全量替换", false, 100)]
        public static void CreateAndReplace()
        {
            Font ttf = AssetDatabase.LoadAssetAtPath<Font>(NewTtfPath);
            if (ttf == null)
            {
                Debug.LogError($"[字体] 找不到源字体：{NewTtfPath}，请先确认 ttf 已导入");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontAssetPath);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(
                    ttf, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                    AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);

                if (font == null)
                {
                    Debug.LogError("[字体] 生成 TMP 字体资产失败");
                    return;
                }

                font.name = Path.GetFileNameWithoutExtension(NewFontAssetPath);
                AssetDatabase.CreateAsset(font, NewFontAssetPath);

                // 材质与图集作为子资产一并存进同一个 .asset
                if (font.material != null)
                {
                    font.material.name = font.name + " Material";
                    AssetDatabase.AddObjectToAsset(font.material, font);
                }
                Texture2D[] atlas = font.atlasTextures;
                if (atlas != null)
                {
                    for (int i = 0; i < atlas.Length; i++)
                    {
                        if (atlas[i] == null) continue;
                        atlas[i].name = $"{font.name} Atlas {i + 1}";
                        AssetDatabase.AddObjectToAsset(atlas[i], font);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(NewFontAssetPath);
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontAssetPath);
                Debug.Log($"[字体] 已生成 TMP 字体资产：{NewFontAssetPath}（Dynamic / 多图集 / SDFAA）");
            }
            else
            {
                Debug.Log($"[字体] 复用已有 TMP 字体资产：{NewFontAssetPath}");
            }

            // 1) TMP 默认字体
            bool settingsChanged = SetDefaultFont(font);

            // 2) 预制体里的所有文本组件
            int prefabs = 0, texts = 0;
            List<string> skipped = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                int changed = ReplaceIn(root, font);
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                    texts += changed;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[字体] 完成：TMP 默认字体{(settingsChanged ? "已更换" : "未改动")}，" +
                      $"预制体 {prefabs} 个、文本组件 {texts} 个已换字体。");
            Debug.Log("[字体] 请再执行一次「② 检查旧字体残留引用」，确认没有残留后再删除厚底黑。");
            if (skipped.Count > 0)
                Debug.LogWarning($"[字体] 跳过 {skipped.Count} 个预制体：{string.Join("、", skipped)}");
        }

        [MenuItem("Tools/聚光灯/字体/② 检查旧字体残留引用", false, 101)]
        public static void ReportOldReferences()
        {
            List<string> hits = new List<string>();
            string[] exts = { ".prefab", ".unity", ".asset", ".mat", ".controller", ".overrideController" };
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/")) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (System.Array.IndexOf(exts, ext) < 0) continue;

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }
                if (text.Contains(OldFontGuid)) hits.Add(path);
            }

            if (hits.Count == 0)
                Debug.Log("[字体] 没有发现旧字体引用，可以删除 AaHouDiHei-2.ttf 与 AaHouDiHei-2 SDF.asset（含 .meta）。");
            else
                Debug.LogWarning($"[字体] 仍有 {hits.Count} 处引用厚底黑：\n{string.Join("\n", hits)}");
        }

        /// <summary>把 TMP Settings 里的默认字体改成新字体</summary>
        private static bool SetDefaultFont(TMP_FontAsset font)
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[字体] 找不到 TMP Settings，跳过默认字体设置");
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(path);
            SerializedObject so = new SerializedObject(settings);
            SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogWarning($"[字体] {path} 里没有 m_defaultFontAsset 字段");
                return false;
            }
            if (prop.objectReferenceValue == font) return false;

            prop.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[字体] TMP 默认字体 → {font.name}（{path}）");
            return true;
        }

        /// <summary>替换一个对象树里所有 TMP 文本的字体，返回改动的组件数</summary>
        private static int ReplaceIn(GameObject root, TMP_FontAsset font)
        {
            int count = 0;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font) continue;

                // 文本原本用的材质属于旧字体（或没设），一起换成新字体自带的材质，
                // 否则字会按旧图集采样、显示成糊字或空字。
                Material oldMat = text.fontSharedMaterial;
                text.font = font;
                if (font.material != null && (oldMat == null || oldMat != font.material))
                    text.fontSharedMaterial = font.material;

                EditorUtility.SetDirty(text);
                count++;
            }
            return count;
        }
    }
}
#endif
