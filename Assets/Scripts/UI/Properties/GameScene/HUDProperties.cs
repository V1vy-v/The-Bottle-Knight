using System;
using Framework;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// HUD 的界面数据，打开界面时传入。
    /// HUD 是运行时 AddComponent 挂出来的组件，预制体上只有 UIView，
    /// 所以需要美术调的参数都放在这里，才能在预制体上编辑。
    /// </summary>
    [Serializable]
    public class HUDProperties : PanelProperties
    {
        [Header("道具栏")]
        [Tooltip("道具图标在 Content 格子里的尺寸")]
        public Vector2 itemIconSize = new Vector2(80f, 80f);

        [Header("小地图")]
        [Tooltip("尺寸 3，顺序 = MapRoot 子物体（冰箱 / 厨房 / 超市）。\n" +
                 "每项填【该场景出生点】在这张图上的归一化位置：左下角 (0,0)，右上角 (1,1)。\n" +
                 "只填这一个值就够了，其余偏移由代码按主角出生位置自动推")]
        public Vector2[] mapSpawnUv =
        {
            new Vector2(0.03f, 0.52f),   // 冰箱：出生点 (-276, 20) 靠近房间左端
            new Vector2(0.5f, 0.5f),     // 厨房
            new Vector2(0.5f, 0.5f),     // 超市
        };

        [Tooltip("尺寸 3，顺序同上。每张图【整张】覆盖多少世界单位宽度。\n" +
                 "代码用它算缩放：px/世界单位 = 地图矩形宽度 ÷ 这个值。\n" +
                 "冰箱：出生点 -276 到厨房门 -156，约 125")]
        public float[] mapCoveredWorldWidth =
        {
            125f,       // 冰箱
            290f,       // 厨房
            300f,       // 超市
        };

        [Tooltip("尺寸 3，顺序同上。每张图【整张】覆盖多少世界单位高度。\n" +
                 "填了就用它单独算 Y 的缩放（贴图长宽比和世界区域不一致时，标记不会上下漂）；\n" +
                 "填 0 = 不单独算，退回用宽度的缩放（要求贴图比例恰好等于世界比例）。")]
        public float[] mapCoveredWorldHeight =
        {
            0f,
            0f,
            0f,
        };

        [Header("小地图 · 标记外观")]
        [Tooltip("主角标记（固定在遮罩中心）。留空用纯白方块")]
        public Sprite playerMarkSprite;
        [Tooltip("敌人标记。留空用纯红方块")]
        public Sprite enemyDotSprite;
        [Tooltip("可交互点（对话 / 商店）的感叹号。留空用纯黄方块")]
        public Sprite interactSprite;

        public Vector2 playerMarkSize = new Vector2(16f, 16f);
        public Vector2 enemyDotSize = new Vector2(14f, 14f);
        public Vector2 interactMarkSize = new Vector2(22f, 22f);

        [Header("小地图 · 调试")]
        [Tooltip("勾上后每秒在 Console 打印主角世界坐标与它在这张图上的归一化位置")]
        public bool debugMapPosition;

        // 供预制体上的 UIView 序列化使用
        public HUDProperties() { }

        public HUDProperties(PanelPriority priority) : base(priority) { }
    }
}
