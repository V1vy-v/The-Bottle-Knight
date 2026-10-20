using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using NavMeshPlus.Components;

namespace Framework.Editor
{
    /// <summary>
    /// 修复 NavMeshSurface 的 Volume 采集盒子。
    /// <para>
    /// 背景：三个 Surface 都用 <c>CollectObjects.Volume</c>，但盒子尺寸是
    /// <c>Size (x, 10, z)</c>、<c>Center (x, 2, z)</c> —— 高度只覆盖 y ∈ [-3, 7]。
    /// 2D 场景里 y 是「上下」，绝大多数几何都不在这条窄带里，
    /// 结果就是「点 Bake 只有横跨 y≈0 的厨房被烘进去」。
    /// </para>
    /// <para>
    /// 做法：把每个 Volume 盒子的 y 撑到覆盖当前场景全部渲染体的高度，
    /// x / z 保持不变（三个区域靠 x 分段，不能动）。
    /// </para>
    /// 菜单：Tools/聚光灯/修复 NavMesh 采集范围（并重烘）
    /// </summary>
    public static class NavMeshVolumeFixer
    {
        [MenuItem("Tools/聚光灯/修复 NavMesh 采集范围（并重烘）")]
        public static void FixAndRebake()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[NavMesh] 没有打开的场景");
                return;
            }

            // 1) 统计整个场景的竖直范围（2D 的 y）
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
                minZ = Mathf.Min(minZ, b.min.z);
                maxZ = Mathf.Max(maxZ, b.max.z);
            }

            if (minY > maxY)
            {
                Debug.LogError("[NavMesh] 场景里没有渲染体，放弃");
                return;
            }

            float centerY = (minY + maxY) * 0.5f;
            float heightY = Mathf.Max(20f, (maxY - minY) * 1.2f);
            float centerZ = (minZ + maxZ) * 0.5f;
            float depthZ = Mathf.Max(20f, (maxZ - minZ) * 2f);

            int fixedCount = 0;
            int baked = 0;

            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface == null) continue;

                if (surface.collectObjects == CollectObjects.Volume)
                {
                    Undo.RecordObject(surface, "修复 NavMesh 采集范围");

                    Vector3 size = surface.size;
                    Vector3 center = surface.center;

                    surface.size = new Vector3(size.x, heightY, depthZ);
                    surface.center = new Vector3(center.x, centerY, centerZ);

                    EditorUtility.SetDirty(surface);
                    fixedCount++;

                    Debug.Log($"[NavMesh] {surface.name}：Size.y {size.y:F1} → {heightY:F1}，" +
                              $"Center.y {center.y:F1} → {centerY:F1}，Size.z → {depthZ:F1}，Center.z → {centerZ:F1}");
                }

                // 2) 统一用碰撞体几何（和玩家碰撞一致）
                if (surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders)
                {
                    Undo.RecordObject(surface, "NavMesh 改用碰撞体几何");
                    surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    EditorUtility.SetDirty(surface);
                    Debug.Log($"[NavMesh] {surface.name}：Use Geometry → Physics Colliders");
                }

                // 3) 直接重烘
                surface.BuildNavMesh();
                baked++;
                Debug.Log($"[NavMesh] {surface.name}：已重新烘焙");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[NavMesh] 完成：调整 {fixedCount} 个采集盒子，重烘 {baked} 个 Surface");
        }
    }
}
