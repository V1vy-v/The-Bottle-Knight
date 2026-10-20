using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// 自动填充敌人的 AnimatorOverrideController。
    /// <para>
    /// 背景：所有敌人的 override controller 都是 <c>m_Clips: []</c> 空表，
    /// 即使烘焙出了 <c>enemy_coffee_idle</c> 这类 clip，Animator 也不会用上。
    /// </para>
    /// <para>
    /// 规则：<c>enemy_&lt;form&gt;.overrideController</c> 的基类 clip 按「动作名」匹配
    /// <c>enemy_&lt;form&gt;_&lt;action&gt;</c>，写成覆盖。
    /// 匹配不到的一律只打警告，不动原资产。
    /// </para>
    /// 菜单：Tools/聚光灯/自动填充敌人 Animator 覆盖
    /// </summary>
    public static class EnemyAnimatorOverrideFiller
    {
        private const string EnemyControllerRoot = "Assets/Animations/Animation Controller/enemy";
        private const string ClipRoot = "Assets/Animations/Animations/enemy";

        /// <summary>控制器文件名里的形态 → clip 文件名里的形态（中文/特殊命名在这里映射）</summary>
        private static readonly Dictionary<string, string> FormAliases = new Dictionary<string, string>
        {
            { "农夫一拳", "nongfu1" },
            { "农夫二拳", "nongfu2" },
            { "农夫三拳", "sanquan" },
        };

        [MenuItem("Tools/聚光灯/自动填充敌人 Animator 覆盖")]
        public static void FillAll()
        {
            // 1) 收集所有敌人 clip：form_action → path
            var clipMap = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);   // enemy_coffee_idle
                if (!name.StartsWith("enemy_")) continue;

                string rest = name.Substring("enemy_".Length);
                int cut = rest.LastIndexOf('_');
                if (cut <= 0 || cut >= rest.Length - 1) continue;

                string key = rest.Substring(0, cut) + "_" + rest.Substring(cut + 1);
                clipMap[key] = path;
            }

            int touched = 0;
            int pairs = 0;
            var failed = new List<string>();

            // 2) 遍历所有 override controller
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorOverrideController", new[] { EnemyControllerRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var oac = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (oac == null) continue;

                // 控制器文件名 → 形态名（enemy_Coffee → coffee）
                string baseName = System.IO.Path.GetFileNameWithoutExtension(path);   // enemy_Coffee
                if (!baseName.StartsWith("enemy_")) continue;
                string form = baseName.Substring("enemy_".Length);
                if (FormAliases.TryGetValue(form, out string alias)) form = alias;
                form = form.ToLowerInvariant();

                AnimatorController baseController = oac.runtimeAnimatorController as AnimatorController;
                if (baseController == null)
                {
                    failed.Add($"{baseName}（找不到基类 controller）");
                    continue;
                }

                // 基类用到的 clip（去重），按动作名匹配对应形态的 clip
                var baseClips = baseController.animationClips
                    .Where(c => c != null)
                    .Distinct()
                    .ToList();

                int appliedHere = 0;
                foreach (AnimationClip baseClip in baseClips)
                {
                    string action = ExtractAction(baseClip.name);
                    if (action == null) continue;

                    if (!clipMap.TryGetValue(form + "_" + action, out string targetPath))
                        continue;                                   // 这个形态还没烘焙该动作，跳过

                    var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
                    if (target == null || target == baseClip) continue;

                    oac[baseClip] = target;
                    appliedHere++;
                }

                if (appliedHere > 0)
                {
                    EditorUtility.SetDirty(oac);
                    touched++;
                    pairs += appliedHere;
                    Debug.Log($"[覆盖] {baseName} <- {form}_* 共 {appliedHere} 个动作");
                }
                else
                {
                    failed.Add($"{baseName}（form={form}，没有匹配到 enemy_{form}_<动作> 的 clip）");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (string reason in failed)
                Debug.LogWarning($"[覆盖] 跳过 {reason}");

            Debug.Log($"[覆盖] 完成：改动 {touched} 个 override controller，共 {pairs} 条覆盖" +
                      (failed.Count > 0 ? $"，{failed.Count} 个被跳过" : string.Empty));
        }

        /// <summary>从 clip 名里取动作：enemy_water_idle → idle；enemy_农夫三拳_atk → atk</summary>
        private static string ExtractAction(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;

            int cut = clipName.LastIndexOf('_');
            if (cut < 0 || cut >= clipName.Length - 1) return null;
            return clipName.Substring(cut + 1).ToLowerInvariant();
        }
    }
}
