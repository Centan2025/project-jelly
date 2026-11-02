#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jelly.Editor
{
    public static class Phase2CoreObstaclePrefabBuilder
    {
        private const string OutputFolder = "Assets/Jelly/Prefabs/Phase2CoreObstacles";
        private const string FullKitPrefabFolder = "Assets/Jelly/Prefabs/BlenderKitMolds";
        private const string KitFbxPath = "Assets/Jelly/Models/ProjectJelly_Kit.fbx";

        [InitializeOnLoadMethod]
        private static void AutoRefreshSceneAndPrefabsOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                string[] fbxPaths = { KitFbxPath, "Assets/Jelly/Resources/Models/ProjectJelly_Kit.fbx" };
                for (int i = 0; i < fbxPaths.Length; i++)
                {
                    var importer = AssetImporter.GetAtPath(fbxPaths[i]) as ModelImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }

                var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    if (allTransforms[i] != null)
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(allTransforms[i].gameObject);
                }

                var mgr = Object.FindFirstObjectByType<EndlessTrackManager>();
                if (mgr != null)
                {
                    mgr.StartLevel(Mathf.Max(1, mgr.CurrentLevelNumber));
                    EditorUtility.SetDirty(mgr);
                    SceneView.RepaintAll();
                }
            };
        }

        [MenuItem("Jelly/Phase 2/Build 5 Core Obstacle Prefabs")]
        public static void BuildAllCoreObstaclePrefabs()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }
            if (!Directory.Exists(FullKitPrefabFolder))
            {
                Directory.CreateDirectory(FullKitPrefabFolder);
            }

            var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbxPath);

            // 1. Phase 2 Core + Phase 4 & 6 Gameplay Obstacle Prefabs
            BuildObstaclePrefab("Gate_Narrow", EndlessObstacleType.SmallCenterHole, null, fbxRoot);
            BuildObstaclePrefab("Gate_Piston", EndlessObstacleType.VerticalCrusherGate, null, fbxRoot);
            BuildObstaclePrefab("Obstacle_GlassWall", EndlessObstacleType.HeavySmashWall, null, fbxRoot);
            BuildObstaclePrefab("Tunnel_Twin", EndlessObstacleType.TwinSplitTunnels, null, fbxRoot);

            BuildObstaclePrefab("Gate_MultiHole_SizeChoice", EndlessObstacleType.MultiHoleDecisionWall,
                MultiHoleWallConfig.CreateSizeChoicePreset(), fbxRoot);
            BuildObstaclePrefab("Gate_MultiHole_SplitChoice", EndlessObstacleType.MultiHoleDecisionWall,
                MultiHoleWallConfig.CreateSplitChoicePreset(), fbxRoot);
            BuildObstaclePrefab("Gate_MultiHole_RiskReward", EndlessObstacleType.MultiHoleDecisionWall,
                MultiHoleWallConfig.CreateRiskRewardPreset(), fbxRoot);

            BuildObstaclePrefab("Gate_FlexibleSqueeze", EndlessObstacleType.FlexibleSqueezeGate, null, fbxRoot);
            BuildObstaclePrefab("Gate_MassScale", EndlessObstacleType.MassScaleGate, null, fbxRoot);
            BuildObstaclePrefab("Gate_SlidingHoleWall", EndlessObstacleType.SlidingHoleWall, null, fbxRoot);
            BuildObstaclePrefab("Gate_MicroShatter", EndlessObstacleType.MicroShatterGate, null, fbxRoot);

            // 2. Export allGRP_* Molds from ProjectJelly_Kit.fbx as individual Unity Prefabs
            if (fbxRoot != null)
            {
                foreach (Transform child in fbxRoot.transform)
                {
                    if (!child.name.StartsWith("GRP_")) continue;
                    var clone = Object.Instantiate(child.gameObject);
                    clone.name = child.name.Substring(4);
                    clone.transform.localPosition = Vector3.zero;
                    clone.transform.localRotation = Quaternion.identity;
                    string moldPath = $"{FullKitPrefabFolder}/{clone.name}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(clone, moldPath);
                    Object.DestroyImmediate(clone);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase2CoreObstaclePrefabBuilder] Built all Phase 2–6 obstacle & Blender kit mold prefabs.");
        }

        private static void BuildObstaclePrefab(
            string prefabName,
            EndlessObstacleType type,
            MultiHoleWallConfig mhConfig,
            GameObject fbxRoot)
        {
            var root = new GameObject(prefabName);
            var obs = root.AddComponent<EndlessGateObstacle>();
            obs.obstacleType = type;
            obs.multiHoleConfig = mhConfig;

            if (!BlenderKitModelSpawner.PopulateObstacleWithBlenderModel(root.transform, obs))
            {
                if (fbxRoot != null)
                {
                    Transform grp = fbxRoot.transform.Find("GRP_" + prefabName);
                    if (grp != null)
                    {
                        var visualClone = Object.Instantiate(grp.gameObject, root.transform);
                        visualClone.name = "BlenderMesh_" + prefabName;
                        visualClone.transform.localPosition = Vector3.zero;
                        visualClone.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            string path = $"{OutputFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
