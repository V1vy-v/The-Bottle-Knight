using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GamePlay.Enemy;

namespace Framework.Editor
{
    /// <summary>
    /// 一键把当前场景里「本该是静态布景」的 Rigidbody2D 改成 Static。
    /// <para>
    /// 背景：GameScene 里 163 个刚体（Ground / Wall / 超市货架 / 冰柜 …）都是
    /// <c>BodyType = Dynamic</c> + <c>GravityScale = 1</c>，运行时整片场景会往下掉。
    /// Tilemap + CompositeCollider2D 必须挂 Static 刚体，动态刚体只会让它自由落体。
    /// </para>
    /// 保留：带 Player 标签的物体、敌人（EnemyController）。
    /// </summary>
    public static class SceneRigidbodyToStatic
    {
        [MenuItem("Tools/聚光灯/场景刚体批量改静态")]
        public static void FixActiveScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[静态化] 没有打开的场景");
                return;
            }

            Rigidbody2D[] bodies = Object.FindObjectsByType<Rigidbody2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int changed = 0;
            int skipped = 0;

            foreach (Rigidbody2D rb in bodies)
            {
                if (rb == null) continue;

                GameObject go = rb.gameObject;

                // 玩家 / 敌人要保持动态
                if (go.CompareTag("Player") || go.GetComponentInParent<EnemyController>() != null)
                {
                    skipped++;
                    continue;
                }

                if (rb.bodyType == RigidbodyType2D.Static && Mathf.Approximately(rb.gravityScale, 0f))
                    continue;

                Undo.RecordObject(rb, "场景刚体改静态");
                rb.bodyType = RigidbodyType2D.Static;
                rb.gravityScale = 0f;
                EditorUtility.SetDirty(rb);
                changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[静态化] 完成：改动 {changed} 个刚体，跳过 {skipped} 个（玩家/敌人）");
        }
    }
}
