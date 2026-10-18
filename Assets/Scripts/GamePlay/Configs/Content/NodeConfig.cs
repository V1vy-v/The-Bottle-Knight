using GamePlay.Configs;
using GamePlay.Session;
using UnityEngine;

namespace GamePlay.Story
{
    /// <summary>
    /// 剧情流程中的一个节点。每个节点代表"一步"。
    /// Id 继承自 ConfigBase（原 nodeId 字段已并入）。
    /// </summary>
    [CreateAssetMenu(fileName = "NodeConfig", menuName = "Gameplay/NodeConfig")]
    public sealed class NodeConfig : ConfigBase
    {
        [Header("进入此节点时切换到的模式")]
        public GameplayMode mode;

        [Header("是否需要刷新Context")]
        public bool refreshDialog;
        public bool refreshBattle;

        [Header("目标条件")]
        [Tooltip("本节点等哪个目标；留空接受任何报告")]
        public string goalId;

        [Header("数据配置(不需要就不填)")]
        public TextAsset dialogConfig;
        public BattleConfig battleConfig;

        [Tooltip("【临时】战斗系统完成前：敌人照常生成，进场后短暂延迟立刻判定死亡。做完真战斗后逐个取消勾选，最后删掉本字段。")]
        public bool autoWinBattle;

        [Header("下一个结点")]
        public NodeConfig nextNode;

        [Header("当前流程")]
        public StoryStage currentStage;

        [Header("转场")]
        [Tooltip("进入本节点时播放一次黑屏转场（跨关卡区域用，比如冰箱→厨房）")]
        public bool fadeTransition;

        [Tooltip("黑屏全黑时把主角传送到这个场景物体处（填物体名，如 [KitchenSpawn]）；留空则不传送")]
        public string teleportTargetName;
    }
}
