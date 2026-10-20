using System.Collections.Generic;
using System.Linq;
using Framework;
using GamePlay.Configs;
using GamePlay.Enemy;
using GamePlay.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 超市流程的场景补齐工具。手改 .unity 很容易漏层级 / 漏字段，
/// 这里统一用编辑器 API 做，跑一次即可，重复跑是幂等的。
/// <para>菜单：Tools / 超市流程 / 一键检查并补齐场景</para>
/// </summary>
public static class SupermarketSceneSetup
{
    // ---------------- 商品分组 ----------------
    static PropConfig P(string file) =>
        AssetDatabase.LoadAssetAtPath<PropConfig>($"Assets/Configs/Prop/{file}.asset");

    static List<PropConfig> KitchenGoods() => new()
    {
        P("PropConfig 0"),   // 调味罐
        P("PropConfig 1"),   // 吸管
        P("PropConfig 2"),   // 打蛋器
        P("PropConfig 3"),   // 手套
        P("PropConfig 4"),   // 保鲜膜
        P("PropConfig 5"),   // 平底锅
        P("PropConfig 6"),   // 高压锅
        P("PropConfig 7"),   // 篮球
    };

    static List<PropConfig> MarketGoods() => new()
    {
        P("PropConfig 条形码"),
        P("PropConfig 扫把"),
        P("PropConfig 购物车"),
        P("PropConfig 果冻"),
        P("PropConfig 扫码枪"),
        P("PropConfig 自热火锅"),
    };

    [MenuItem("Tools/超市流程/一键检查并补齐场景", false, 10)]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
        {
            Debug.LogError("[超市] 没有打开场景");
            return;
        }

        var report = new List<string>();

        // ---------- 1. 找个位置参考：超市出生点 ----------
        GameObject spawn = FindScene("[SupermarketSpawn]") ?? FindScene(GlobalConstants.PLAYER_SPAWN_NAME);
        Vector2 spawnPos = spawn != null ? (Vector2)spawn.transform.position : new Vector2(307f, -82f);
        report.Add($"超市出生点：{(spawn != null ? spawn.name : "（没找到）")} @ {spawnPos}");

        // ---------- 2. 开场对话触发器 ----------
        EnsureTrigger("meet_leibi", spawnPos + new Vector2(16f, 6f), new Vector2(16f, 36f), "Node_23", false);
        report.Add("meet_leibi：已就位（goal = Node_23）");

        // ---------- 3. 冰柜区入口触发器 ----------
        // 冰柜区是右侧竖长条，触发区给大一点（覆盖整条走廊），免得玩家从下往上走时擦不到
        EnsureTrigger("enter_icecafe", new Vector2(555f, 0f), new Vector2(110f, 150f), "Node_30", false);
        report.Add("enter_icecafe：已就位（goal = Node_30，覆盖冰柜区整条走廊）");

        // ---------- 4. 中三拳尸体 ----------
        GameObject corpse = FindScene("场景-中三拳") ?? FindScene("场景-被击晕的中三拳_0");
        if (corpse != null)
        {
            WireCorpse(corpse);
            report.Add("场景-中三拳：EnemyController / AbsorbSource / 圆形触发器 已就位");
        }
        else report.Add("⚠ 没找到「场景-中三拳」");

        // ---------- 5. 雷碧立绘（说完话退场） ----------
        GameObject leibi = FindScene("对话用-leibi");
        if (leibi != null)
        {
            var hide = leibi.GetComponent<HideOnNodeEnter>() ?? leibi.AddComponent<HideOnNodeEnter>();
            SetField(hide, "_nodeId", "Node_24");
            EditorUtility.SetDirty(hide);

            var sr = leibi.GetComponent<SpriteRenderer>();
            report.Add(sr != null && sr.sprite == null
                ? "⚠ 对话用-leibi：SpriteRenderer 没有图（把雷碧立绘拖进去）"
                : "对话用-leibi：HideOnNodeEnter(Node_24) 已就位");
        }
        else report.Add("⚠ 没找到「对话用-leibi」");

        // ---------- 6. 收银台：领东西，不是商店 ----------
        GameObject cashier = FindScene("收银台交互点");
        if (cashier != null)
        {
            var shop = cashier.GetComponent<ShopInteractable>();
            if (shop != null) Object.DestroyImmediate(shop, true);      // 收银台不卖货

            var gift = cashier.GetComponent<CashierInteractable>() ?? cashier.AddComponent<CashierInteractable>();
            SetField(gift, "_gift", P("PropConfig 8"));                 // 曼妥思
            SetField(gift, "_prompt", "收银台");
            EditorUtility.SetDirty(gift);
            report.Add("收银台交互点：CashierInteractable（送曼妥思）已就位");
        }
        else report.Add("⚠ 没找到「收银台交互点」");

        // ---------- 7. 清掉早先手写 YAML 时多建的重复物件 ----------
        GameObject dup = FindScene("cashier_interact");
        if (dup != null)
        {
            Object.DestroyImmediate(dup);
            report.Add("已删除重复的 cashier_interact");
        }

        // ---------- 8. 三个商店分货 ----------
        WireShop("drawer1", KitchenGoods(), report, "厨房抽屉");
        WireShop("drawer2", MarketGoods(), report, "超市货架·下路");
        WireShop("drawer3", MarketGoods(), report, "超市货架·上路");

        // ---------- 9. 三场 BOSS 的刷怪锚点 ----------
        WireBattleAnchor("Assets/Configs/Battle/Battle_008_LeiBi.asset", "Leibi", report);
        WireBattleAnchor("Assets/Configs/Battle/Battle_009_IceCafe.asset", "IceCafe", report);
        WireBattleAnchor("Assets/Configs/Battle/Battle_010_ThreeFist.asset", "SanQuan", report);

        // ---------- 10. 环境检查 ----------
        int borders = Resources.FindObjectsOfTypeAll<Transform>()
            .Count(t => t.name == "Border (2)" && t.gameObject.scene == scene);
        report.Add($"阻挡墙 Border (2)：{borders} 个");
        report.Add($"场上敌人：{Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length} 只");
        report.Add($"场上商店：{Object.FindObjectsByType<ShopInteractable>(FindObjectsSortMode.None).Length} 个");

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[超市] 场景补齐完成\n  " + string.Join("\n  ", report));
    }

    // ================= 工具 =================

    static void WireCorpse(GameObject go)
    {
        var ec = go.GetComponent<EnemyController>() ?? go.AddComponent<EnemyController>();

        var cfg = AssetDatabase.LoadAssetAtPath<EnemyConfig>("Assets/Configs/Enemy/Enemy_017_农夫二拳.asset");
        if (cfg == null) Debug.LogWarning("[超市] 找不到 Enemy_017_农夫二拳.asset");

        SetField(ec, "config", cfg);
        SetField(ec, "_startDefeated", true);
        SetField(ec, "_passiveAtStart", true);

        if (go.GetComponent<AbsorbSource>() == null) go.AddComponent<AbsorbSource>();

        var col = go.GetComponent<CircleCollider2D>() ?? go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 2.5f;

        go.layer = 11;      // Interactable：PlayerInteractor 只扫这一层
        EditorUtility.SetDirty(go);
    }

    static void WireShop(string name, List<PropConfig> goods, List<string> report, string label)
    {
        GameObject go = FindScene(name);
        if (go == null) { report.Add($"⚠ 没找到商店 {name}"); return; }

        var shop = go.GetComponent<ShopInteractable>();
        if (shop == null) { report.Add($"⚠ {name} 上没有 ShopInteractable"); return; }

        // 没图标的道具不进商店（现在只保留两张图标图集里有的）
        goods.RemoveAll(p => p == null || p.sprite == null);

        var so = new SerializedObject(shop);
        var list = so.FindProperty("_goods");
        list.arraySize = goods.Count;
        for (int i = 0; i < goods.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = goods[i];
        so.ApplyModifiedProperties();

        report.Add($"{label}（{name}）：{goods.Count} 件货");
    }

    /// <summary>把战斗配置的 SpawnAnchorName 指到场景里的空物体，并检查它是否真的存在</summary>
    static void WireBattleAnchor(string assetPath, string anchorName, List<string> report)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<BattleConfig>(assetPath);
        if (cfg == null)
        {
            report.Add($"⚠ 找不到战斗配置 {assetPath}");
            return;
        }

        var so = new SerializedObject(cfg);
        var prop = so.FindProperty("SpawnAnchorName");
        if (prop == null) { report.Add($"⚠ {cfg.name} 上没有 SpawnAnchorName 字段"); return; }
        prop.stringValue = anchorName;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(cfg);

        bool found = FindScene(anchorName) != null;
        report.Add($"BOSS 刷怪点 {anchorName}（{cfg.name}）：{(found ? "场景里有" : "⚠ 场景里没找到，会改成围绕主角散开")}");
    }

    static FlowTrigger EnsureTrigger(string name, Vector2 pos, Vector2 size, string goalId, bool asAnchor)

    {
        GameObject go = FindScene(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }

        // 位置每次都按工具给的值摆正：这两个触发器是工具管的，手调过的位置重跑会覆盖
        go.transform.position = pos;
        go.layer = 10;      // Obstacle：触发器只在这一层和玩家回调

        var col = go.GetComponent<BoxCollider2D>() ?? go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.offset = Vector2.zero;
        col.size = size;

        var ft = go.GetComponent<FlowTrigger>() ?? go.AddComponent<FlowTrigger>();
        SetField(ft, "_goalId", goalId);
        SetField(ft, "_once", false);
        SetField(ft, "_asEncounterAnchor", asAnchor);
        SetField(ft, "_hideAfterFire", false);

        EditorUtility.SetDirty(go);
        return ft;
    }

    static GameObject FindScene(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name != name) continue;
            GameObject go = t.gameObject;
            if (!go.scene.IsValid() || string.IsNullOrEmpty(go.scene.name)) continue;   // 跳过预制体资产
            return go;
        }
        return null;
    }

    static void SetField(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning($"[超市] {target.name} 上找不到字段 {field}（脚本改过名？）");
            return;
        }

        switch (value)
        {
            case bool b: p.boolValue = b; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case string s: p.stringValue = s; break;
            case Object o: p.objectReferenceValue = o; break;
            default:
                Debug.LogWarning($"[超市] {target.name}.{field} 不支持的类型");
                return;
        }

        so.ApplyModifiedProperties();
    }
}
