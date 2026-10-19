using Framework;
using GamePlay.Configs;
using GamePlay.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using YooAsset;

namespace GamePlay.Enemy
{
    /// <summary>
    /// 临时测试：走实例门面生成一个敌人，验证「生成 → 击败 → 计数 → 吸取 → 回收」整条链路。
    /// 跑通后删掉。
    /// </summary>
    public class TestEnemySpawner : MonoBehaviour
    {
        [SerializeField] private string _enemyAddress = "Prefab_Enemy_Guojiao";
        [SerializeField] private float _spawnOffsetX = 1.5f;
        [SerializeField] private LiquidConfig _debugLiquid;

        private GameObject _enemy;
        private bool _registered;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) Spawn();
            if (kb.digit2Key.wasPressedThisFrame) Kill();
            if (kb.digit3Key.wasPressedThisFrame) Release();

            if (kb.digit4Key.wasPressedThisFrame && _debugLiquid != null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null && pc.PlayerLiquids.ForceGetLiquid(_debugLiquid, LiquidTankType.Current))
                    Debug.Log($"[测试] 主槽已塞入 {_debugLiquid.Name}");
            }
        }

        private void Spawn()
        {
            if (_enemy) { Debug.Log("[测试] 场上已有一个，先按 3 回收"); return; }

            // 和 GameplayBattleState.SpawnEnemy 用同一个 API，
            // 这样实例归门面管理，Release 才不会报 "not created by the instance facade"
            _enemy = Global.InstantiateUnpooled(_enemyAddress, new InstantiateOptions(true));
            if (!_enemy)
            {
                Debug.LogError($"[测试] 生成失败，检查地址：{_enemyAddress}");
                return;
            }

            // InstantiateOptions(true) 不改位置，落在预制体自己的坐标，挪到主角旁边
            var player = FindObjectOfType<PlayerController>();
            if (player)
            {
                _enemy.transform.position =
                    player.transform.position + new Vector3(_spawnOffsetX, 0f, 0f);
            }

            var enemy = _enemy.GetComponent<EnemyController>();
            if (enemy != null) enemy.MarkSpawnedByFacade();
            if (enemy == null)
            {
                Debug.LogError("[测试] 预制体上没有 EnemyController");
                return;
            }

            // 接上战斗计数：这样击败它能触发 IsVictory，走完整条「战斗结束 → 切模式」
            var root = Global.Get<GameplayRoot>();
            _registered = root != null && root.Battle != null;
            if (_registered)
            {
                root.Battle.StartBattle(1);
                enemy.OnDefeated += HandleDefeated;
            }

            Debug.Log($"[测试] 已生成 {_enemy.name}  位置 {_enemy.transform.position}");
        }

        private void HandleDefeated(EnemyController e)
        {
            Debug.Log("[测试] 敌人被击败 → 通知 BattleContext");
            Global.Get<GameplayRoot>()?.Battle?.OnEnemyDead();
        }

        private void Kill()
        {
            if (!_enemy) { Debug.Log("[测试] 场上没有敌人"); return; }

            var enemy = _enemy.GetComponent<EnemyController>();
            if (enemy == null) return;

            // 和战死同一条通路：进 Dead 状态 → NotifyDefeated → IsDefeated = true
            // 注意它内部有 if (_fsm == null) return; —— 生成后要等一帧 Start 跑完才有效
            enemy.ForceDefeated();
            Debug.Log("[测试] 已强制击倒");
        }

        private void Release()
        {
            if (!_enemy) { Debug.Log("[测试] 场上没有敌人"); return; }

            var enemy = _enemy.GetComponent<EnemyController>();
            if (enemy != null && _registered) enemy.OnDefeated -= HandleDefeated;

            Global.Release(_enemy);   // 门面生成的实例，这里 Release 是合法的
            _enemy = null;
            _registered = false;
            Debug.Log("[测试] 已回收");
        }

        private void OnDisable()
        {
            var enemy = _enemy ? _enemy.GetComponent<EnemyController>() : null;
            if (enemy != null && _registered) enemy.OnDefeated -= HandleDefeated;
        }


    }
}