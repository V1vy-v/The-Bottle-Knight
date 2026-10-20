using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GamePlay.Configs;

namespace Framework.Editor
{
    /// <summary>
    /// 敌人血量调参工具：按 Tier 批量给 MaxHp 乘一个系数。
    /// <para>
    /// 场景：BOSS 血量太高（国窖/快乐水/雷碧 800、冰美式 600、三拳大王 1000），
    /// 手改 5 个 .asset 太慢，这里一次搞定，并且带 Undo。
    /// </para>
    /// 菜单：Tools/聚光灯/敌人血量调参
    /// </summary>
    public class EnemyBalanceWindow : EditorWindow
    {
        private float _bossScale = 0.6f;       // Tier 3 = BOSS
        private float _eliteScale = 0.9f;      // Tier 2 = 精英
        private bool _onlyTier3 = true;

        [MenuItem("Tools/聚光灯/敌人血量调参")]
        public static void Open()
        {
            var w = GetWindow<EnemyBalanceWindow>("敌人血量");
            w.minSize = new Vector2(420f, 260f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "按 Tier 批量调整 EnemyConfig.MaxHp（带 Undo）。\n" +
                "Tier 3 = BOSS（国窖 / 快乐水 / 雷碧 / 冰美式 / 三拳大王）\n" +
                "Tier 2 = 精英（料酒）", MessageType.Info);

            _bossScale = EditorGUILayout.Slider("BOSS 血量系数", _bossScale, 0.1f, 2f);
            _onlyTier3 = EditorGUILayout.Toggle("只改 BOSS（Tier 3）", _onlyTier3);

            if (!_onlyTier3)
                _eliteScale = EditorGUILayout.Slider("精英血量系数", _eliteScale, 0.1f, 2f);

            EditorGUILayout.Space();

            if (GUILayout.Button("应用到所有敌人配置", GUILayout.Height(32)))
                Apply();

            EditorGUILayout.Space();

            if (GUILayout.Button("列出当前血量"))
                ListCurrent();
        }

        private List<EnemyConfig> LoadAll()
        {
            var list = new List<EnemyConfig>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyConfig"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);
                if (cfg != null) list.Add(cfg);
            }
            return list;
        }

        private void Apply()
        {
            foreach (EnemyConfig cfg in LoadAll())
            {
                int tier = (int)cfg.Tier;

                float scale = 1f;
                if (tier == 3) scale = _bossScale;                       // Boss
                else if (tier == 2 && !_onlyTier3) scale = _eliteScale;  // MiniBoss（料酒）
                else continue;

                if (Mathf.Approximately(scale, 1f)) continue;

                Undo.RecordObject(cfg, "调整敌人血量");
                cfg.MaxHp = Mathf.Max(1f, cfg.MaxHp * scale);
                EditorUtility.SetDirty(cfg);
                Debug.Log($"[血量] {cfg.Name}（Tier{tier}）→ {cfg.MaxHp}");
            }

            AssetDatabase.SaveAssets();
        }

        private void ListCurrent()
        {
            foreach (EnemyConfig cfg in LoadAll())
                Debug.Log($"[血量] {cfg.Id}  Tier{(int)cfg.Tier}  HP={cfg.MaxHp}");
        }
    }
}
