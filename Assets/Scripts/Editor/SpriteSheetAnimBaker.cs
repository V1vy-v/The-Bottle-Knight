using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// 把序列帧图集烘焙进 AnimationClip：图集里一张切片 = 一帧。
    /// <para>
    /// 依赖统一命名：图集 <c>&lt;owner&gt;_&lt;form&gt;_&lt;action&gt;.png</c>，
    /// 动画 <c>&lt;owner&gt;_&lt;form&gt;_&lt;action&gt;.anim</c>（同名自动配对）。
    /// 目录名不参与匹配，所以形态文件夹大小写不一致也不影响。
    /// </para>
    /// <para>
    /// 用 <c>EditorCurveBinding</c> + <c>AnimationUtility.SetObjectReferenceCurve</c> 写入，
    /// 这样 Unity 会一并生成 <c>m_ClipBindingConstant</c>（手改 YAML 只能写曲线，
    /// 绑定表是空的，结果就是 Animation 窗口看得见关键帧、运行时却播不出来）。
    /// </para>
    /// 菜单：
    /// <list type="bullet">
    /// <item>Tools/聚光灯/烘焙主角序列帧动画</item>
    /// <item>Tools/聚光灯/烘焙敌人序列帧动画</item>
    /// </list>
    /// </summary>
    public static class SpriteSheetAnimBaker
    {
        private const float FrameRate = 60f;
        private const string PlayerSheetRoot = "Assets/Art/Sequence/player";
        private const string EnemySheetRoot = "Assets/Art/Sequence/Enemy";
        private const string ClipRoot = "Assets/Animations/Animations";

        /// <summary>这两个动作循环，其余（sprint / atk / dead）不循环</summary>
        private static readonly HashSet<string> LoopActions = new HashSet<string> { "idle", "walk" };

        /// <summary>
        /// 形态别名：图集名里的形态 → 实际要产出的 clip 形态。
        /// coffee 那批素材实际是冰美式，产物统一成 enemy_icecafe_*，和 enemy_IceCafe 控制器对齐。
        /// </summary>
        private static readonly Dictionary<string, string> FormAliases = new Dictionary<string, string>
        {
            { "coffee", "icecafe" },
        };

        [MenuItem("Tools/聚光灯/烘焙主角序列帧动画")]
        public static void BakePlayer()
        {
            Bake("player", PlayerSheetRoot);
        }

        [MenuItem("Tools/聚光灯/烘焙敌人序列帧动画")]
        public static void BakeEnemy()
        {
            Bake("enemy", EnemySheetRoot);
        }

        private static void Bake(string owner, string sheetRoot)
        {
            // 先把现有 clip 按「文件名」建索引，这样不用假设它们放在哪个子目录
            Dictionary<string, string> clipIndex = IndexExistingClips();

            var sheets = AssetDatabase.FindAssets("t:Texture2D", new[] { sheetRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => NormalizeName(System.IO.Path.GetFileNameWithoutExtension(path)).StartsWith(owner + "_"))
                .OrderBy(path => path)
                .ToList();

            int baked = 0;
            int frames = 0;
            var skipped = new List<string>();

            foreach (string sheetPath in sheets)
            {
                // 美术命名可能是 Enemy-coffee-idle / enemy-sanquan-Death，
                // 统一成 enemy_coffee_idle / enemy_sanquan_dead 再配对
                string clipName = NormalizeName(System.IO.Path.GetFileNameWithoutExtension(sheetPath));

                if (!TrySplit(clipName, owner, out string form, out string action))
                {
                    skipped.Add($"{sheetPath}（文件名不符合 {owner}_<form>_<action>）");
                    continue;
                }

                // 动作别名归一（boxing/attack → atk，death → dead），保证和已有敌人 clip 同名
                action = CanonicalAction(action);

                // 形态别名（coffee → icecafe），图集不改名也能产出正确命名的 clip
                if (FormAliases.TryGetValue(form, out string formAlias))
                    form = formAlias;

                clipName = $"{owner}_{form}_{action}";

                List<Sprite> sprites = LoadSprites(sheetPath);
                if (sprites.Count == 0)
                {
                    skipped.Add($"{sheetPath}（没有切好的 Sprite，检查 Texture Type / Sprite Mode / 切片）");
                    continue;
                }

                string clipPath = clipIndex.TryGetValue(clipName, out string existingPath)
                    ? existingPath
                    : $"{ClipRoot}/{owner}/{form}/{clipName}.anim";

                AnimationClip clip = LoadOrCreateClip(clipPath);
                ApplySprites(clip, sprites, LoopActions.Contains(action));

                EditorUtility.SetDirty(clip);
                baked++;
                frames += sprites.Count;
                Debug.Log($"[烘焙-{owner}] {clip.name} <- {sprites.Count} 帧  ({sheetPath})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (string reason in skipped)
                Debug.LogWarning($"[烘焙-{owner}] 跳过 {reason}");

            Debug.Log($"[烘焙-{owner}] 完成：{baked} 个 Animation，共 {frames} 帧" +
                      (skipped.Count > 0 ? $"，{skipped.Count} 项被跳过" : string.Empty));
        }

        /// <summary>统一命名：小写，连字符与空格换成下划线（兼容美术的 Enemy-coffee-idle 写法）</summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        /// <summary>动作别名归一：保证和已有敌人 clip（idle/walk/atk/sprint/dead）同名</summary>
        private static string CanonicalAction(string action)
        {
            switch (action)
            {
                case "boxing":
                case "attack":
                    return "atk";
                case "death":
                    return "dead";
                default:
                    return action;
            }
        }

        /// <summary>把 player_cola_idle 拆成 form=cola、action=idle</summary>
        private static bool TrySplit(string clipName, string owner, out string form, out string action)
        {
            form = null;
            action = null;

            string prefix = owner + "_";
            if (!clipName.StartsWith(prefix)) return false;

            string rest = clipName.Substring(prefix.Length);
            int cut = rest.LastIndexOf('_');
            if (cut <= 0 || cut >= rest.Length - 1) return false;

            form = rest.Substring(0, cut);
            action = rest.Substring(cut + 1);
            return true;
        }

        private static Dictionary<string, string> IndexExistingClips()
        {
            var index = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!index.ContainsKey(name)) index[name] = path;
            }
            return index;
        }

        /// <summary>图集是 Multiple 切片，所有 Sprite 都是同一路径下的子资产</summary>
        private static List<Sprite> LoadSprites(string sheetPath)
        {
            var list = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToList();

            // 按切片名尾部数字排序：player_cola_idle_0 / player__0 / cola_idle_0 都适用
            list.Sort((a, b) => SliceIndex(a.name).CompareTo(SliceIndex(b.name)));
            return list;
        }

        private static int SliceIndex(string spriteName)
        {
            int end = spriteName.Length - 1;
            while (end >= 0 && !char.IsDigit(spriteName[end])) end--;
            if (end < 0) return int.MaxValue;

            int start = end;
            while (start >= 0 && char.IsDigit(spriteName[start])) start--;

            return int.TryParse(spriteName.Substring(start + 1, end - start), out int index)
                ? index
                : int.MaxValue;
        }

        private static AnimationClip LoadOrCreateClip(string clipPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) return existing;

            string dir = System.IO.Path.GetDirectoryName(clipPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string parent = System.IO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, folder);
            }

            var clip = new AnimationClip
            {
                frameRate = FrameRate,
                name = System.IO.Path.GetFileNameWithoutExtension(clipPath),
            };
            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[烘焙] 新建 {clipPath}");
            return clip;
        }

        private static void ApplySprites(AnimationClip clip, List<Sprite> sprites, bool loop)
        {
            // 主角和敌人的 SpriteRenderer 都与 Animator 同在一个物体上，所以 path 是空串
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = sprites[i],
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            clip.frameRate = FrameRate;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.startTime = 0f;
            settings.stopTime = sprites.Count / FrameRate;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
    }
}
