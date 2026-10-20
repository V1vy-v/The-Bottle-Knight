using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// 音效配置管理工具。
    /// <para>
    /// 左半边读 <c>Assets/Resources/AudioConfig.asset</c> 的所有条目；
    /// 右半边扫 <c>Assets/Scripts</c> 里所有 <c>PlaySfx / PlayBGM / PlayLoop / PlayAmbience</c> 调用，
    /// 把「哪个 key 在哪些代码位置被触发」列出来，并对没被调用的 / 调用了但没配置的做标记。
    /// </para>
    /// 菜单：Tools/聚光灯/音效配置管理
    /// </summary>
    public class AudioConfigWindow : EditorWindow
    {
        private class Row
        {
            public string key;
            public AudioClip clip;
            public bool loop;
            public float volume;
            public readonly List<string> sites = new List<string>();
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<string> _missingInConfig = new List<string>();
        private Vector2 _scroll;
        private string _filter = string.Empty;
        private bool _onlyUnused;

        [MenuItem("Tools/聚光灯/音效配置管理")]
        public static void Open()
        {
            var w = GetWindow<AudioConfigWindow>("音效配置");
            w.minSize = new Vector2(760f, 420f);
            w.Reload();
            w.Show();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            _rows.Clear();
            _missingInConfig.Clear();

            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>("Assets/Resources/AudioConfig.asset");
            var byKey = new Dictionary<string, Row>();

            if (config != null)
            {
                foreach (AudioEntry e in config.Entries)
                {
                    if (string.IsNullOrEmpty(e.key)) continue;
                    var row = new Row { key = e.key, clip = e.clip, loop = e.loop, volume = e.volume };
                    _rows.Add(row);
                    byKey[row.key] = row;
                }
            }

            // 扫代码里的调用点
            var usedKeys = new HashSet<string>();
            string[] files = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories);
            var callPattern = new System.Text.RegularExpressions.Regex(
                "\\.(?:PlaySfx|PlayBGM|PlayLoop|PlayAmbience|Play)\\(\\s*\"([^\"]+)\"");

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = callPattern.Match(lines[i]);
                    if (!m.Success) continue;

                    string key = m.Groups[1].Value;
                    usedKeys.Add(key);

                    if (byKey.TryGetValue(key, out Row row))
                        row.sites.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }

            // 代码里用到但配置里没有的（含拼在前缀上的动态 key，跳过以 _ 结尾的）
            foreach (string key in usedKeys)
            {
                if (key.EndsWith("_")) continue;          // "SFX_Player_Melee_" 这种是拼字符串的
                if (!byKey.ContainsKey(key)) _missingInConfig.Add(key);
            }

            _rows.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            _missingInConfig.Sort();
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) Reload();
                GUILayout.Space(8);
                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(220));
                _onlyUnused = GUILayout.Toggle(_onlyUnused, "只看没被调用的", EditorStyles.toolbarButton, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"共 {_rows.Count} 条", EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int shown = 0;
            foreach (Row row in _rows)
            {
                bool unused = row.sites.Count == 0;
                if (_onlyUnused && !unused) continue;
                if (!string.IsNullOrEmpty(_filter) &&
                    row.key.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                shown++;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(row.key, EditorStyles.boldLabel, GUILayout.Width(260));
                        EditorGUILayout.ObjectField(row.clip, typeof(AudioClip), false, GUILayout.Width(200));
                        GUILayout.Label(row.loop ? "循环" : "一次性", GUILayout.Width(50));
                        GUILayout.Label($"音量 {row.volume:F2}", GUILayout.Width(70));
                        GUILayout.FlexibleSpace();
                        if (unused)
                            GUILayout.Label("未被调用", EditorStyles.miniLabel, GUILayout.Width(70));
                    }

                    if (unused)
                    {
                        EditorGUILayout.LabelField("触发时机", "（代码里没有引用，可能是给动态 key 用的）",
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("触发时机", string.Join("、", row.sites.ToArray()),
                            EditorStyles.miniLabel);
                    }
                }
            }

            if (_missingInConfig.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("代码里用到但 AudioConfig 里没有配置（会报警告）：", EditorStyles.boldLabel);
                foreach (string k in _missingInConfig)
                    EditorGUILayout.LabelField("  " + k, EditorStyles.miniLabel);
            }

            GUILayout.Space(6);
            GUILayout.Label($"显示 {shown} 条 / 共 {_rows.Count} 条", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }
}
