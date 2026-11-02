using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jelly.Editor
{
    [InitializeOnLoad]
    public static class JellyProjectBuilder
    {
        private const string ConfigDir = "Assets/Jelly/Configs";
        private const string LevelsDir = "Assets/Jelly/Levels";
        private const string PrefabsDir = "Assets/Jelly/Prefabs";
        private const string MaterialsDir = "Assets/Jelly/Materials";
        private const string ScenesDir = "Assets/Jelly/Scenes";

        static JellyProjectBuilder()
        {
            EditorApplication.delayCall += EnsureScenePopulatedInEditor;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += EnsureScenePopulatedInEditor;
            }
        }

        private static void EnsureScenePopulatedInEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying || BuildPipeline.isBuildingPlayer) return;
            var root = GameObject.Find("LevelRuntimeRoot");
            var player = GameObject.Find("JellyPlayer");
            var curvedSlice = GameObject.Find("SmoothRoadChunk_0");
            var parallaxLayer = GameObject.Find("ParallaxFarLayer");

            var endless = UnityEngine.Object.FindAnyObjectByType<EndlessTrackManager>();
            if (endless != null && (curvedSlice == null || parallaxLayer == null))
            {
                endless.BuildInitialPreviewInEditor(endless.primaryPlayer);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                FocusSceneViewOnPlayer();
            }

            string snapDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Snapshots"));
            if (endless != null && !File.Exists(Path.Combine(snapDir, "unity_piston_gate.png")))
            {
                endless.RenderComparisonSnapshotsToDisk(snapDir);
            }

            if (root == null || player == null || curvedSlice == null)
            {
                var sampleAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity");
                var lvl01 = AssetDatabase.LoadAssetAtPath<JellyLevelDefinition>($"{LevelsDir}/Level_01.asset");
                if (lvl01 == null || sampleAsset == null)
                {
                    GenerateAll();
                }
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                    if (GameObject.Find("SmoothRoadChunk_0") == null)
                    {
                        GenerateAll();
                        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                    }
                    FocusSceneViewOnPlayer();
                }
            }
        }

        private static void FocusSceneViewOnPlayer()
        {
            var player = GameObject.Find("JellyPlayer");
            if (player == null) return;
            foreach (SceneView sv in SceneView.sceneViews)
            {
                if (sv != null)
                {
                    sv.LookAt(player.transform.position + new Vector3(0f, 1f, 6f), Quaternion.Euler(26f, 0f, 0f), 14f);
                    sv.Repaint();
                }
            }
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem("Jelly/Generate All Project Assets & Scenes")]
        public static void GenerateAll()
        {
            EnsureFolders();

            var moveCfg = CreateOrUpdateAsset<JellyMovementConfig>($"{ConfigDir}/DefaultMovementConfig.asset");
            var massCfg = CreateOrUpdateAsset<JellyMassConfig>($"{ConfigDir}/DefaultMassConfig.asset");

            var jellyMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_JellyStylized.mat", new Color(0.28f, 0.80f, 0.98f, 0.88f), 0.85f);
            var trackMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_TrackPastel.mat", new Color(0.92f, 0.94f, 0.98f, 1f), 0.25f);
            var gateMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_GateAccent.mat", new Color(0.96f, 0.56f, 0.44f, 1f), 0.45f);
            var goalMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_GoalEmerald.mat", new Color(0.45f, 0.92f, 0.65f, 1f), 0.5f);
            var pickupMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_PickupLime.mat", new Color(0.35f, 0.95f, 0.55f, 1f), 0.7f);
            var drainMat = CreateOrUpdateMaterial($"{MaterialsDir}/M_DrainCrimson.mat", new Color(0.95f, 0.35f, 0.42f, 1f), 0.4f);

            var generatedLevels = JellyLevelCatalogFactory.CreateFullCatalog50();
            var savedLevelAssets = new List<JellyLevelDefinition>(generatedLevels.Count);
            for (int i = 0; i < generatedLevels.Count; i++)
            {
                var runtimeDef = generatedLevels[i];
                string assetPath = $"{LevelsDir}/{runtimeDef.levelId}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<JellyLevelDefinition>(assetPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(runtimeDef, existing);
                    EditorUtility.SetDirty(existing);
                    savedLevelAssets.Add(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(runtimeDef, assetPath);
                    savedLevelAssets.Add(runtimeDef);
                }
            }

            for (int i = 0; i < savedLevelAssets.Count - 1; i++)
            {
                savedLevelAssets[i].nextLevelReference = savedLevelAssets[i + 1];
                EditorUtility.SetDirty(savedLevelAssets[i]);
            }

            AssetDatabase.SaveAssets();

            var validation = LevelValidator.ValidateCatalog(savedLevelAssets);
            if (!validation.IsValid)
            {
                throw new Exception("Level catalog validation failed: " + string.Join("; ", validation.Errors));
            }

            CreatePlayerPrefab(moveCfg, massCfg, jellyMat);
            CreatePickupPrefab(pickupMat);
            CreateGatePrefab(gateMat);

            var buildScenes = new List<EditorBuildSettingsScene>();

            // Populate SampleScene.unity with Level 01 so opening Unity shows Level 01 immediately!
            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            CreateGameplayScene(sampleScenePath, savedLevelAssets, 0, startInMainMenu: false, jellyMat, trackMat, gateMat, goalMat, pickupMat, drainMat);

            string mainMenuPath = $"{ScenesDir}/MainMenu.unity";
            CreateGameplayScene(mainMenuPath, savedLevelAssets, 0, startInMainMenu: false, jellyMat, trackMat, gateMat, goalMat, pickupMat, drainMat);
            buildScenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));

            for (int i = 0; i < 10; i++)
            {
                string levelScenePath = $"{ScenesDir}/{savedLevelAssets[i].levelId}.unity";
                CreateGameplayScene(levelScenePath, savedLevelAssets, i, startInMainMenu: false, jellyMat, trackMat, gateMat, goalMat, pickupMat, drainMat);
                buildScenes.Add(new EditorBuildSettingsScene(levelScenePath, true));
            }

            string sandboxPath = $"{ScenesDir}/MechanicsTestSandbox.unity";
            CreateGameplayScene(sandboxPath, savedLevelAssets, 9, startInMainMenu: false, jellyMat, trackMat, gateMat, goalMat, pickupMat, drainMat);
            buildScenes.Add(new EditorBuildSettingsScene(sandboxPath, true));

            EditorBuildSettings.scenes = buildScenes.ToArray();

            PlayerSettings.companyName = "JellyStudio";
            PlayerSettings.productName = "Project Jelly";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FocusSceneViewOnPlayer();
            Debug.Log($"[JellyProjectBuilder] Successfully generated 50 LevelDefinitions, Prefabs, Materials, SampleScene, and {buildScenes.Count} Scenes.");
        }

        public static void GenerateAndCaptureSnapshots()
        {
            GenerateAll();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var endless = UnityEngine.Object.FindAnyObjectByType<EndlessTrackManager>();
            if (endless != null)
            {
                endless.BuildInitialPreviewInEditor(endless.primaryPlayer);
                string snapDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Snapshots"));
                endless.RenderComparisonSnapshotsToDisk(snapDir);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
        }

        public static void BatchBuildAndroidApk()
        {
            GenerateAll();
            string buildDir = "Builds/Android";
            if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);
            string apkPath = $"{buildDir}/ProjectJelly_V1.apk";

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled) scenes.Add(s.path);
            }

            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[JellyProjectBuilder] Android Build Result: {report.summary.result}, Output: {apkPath}, Errors: {report.summary.totalErrors}");
        }

        private static void EnsureFolders()
        {
            string[] dirs = { ConfigDir, LevelsDir, PrefabsDir, MaterialsDir, ScenesDir, "Assets/Scenes" };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
            AssetDatabase.Refresh();
        }

        private static T CreateOrUpdateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreatePlayerPrefab(JellyMovementConfig moveCfg, JellyMassConfig massCfg, Material mat)
        {
            var root = new GameObject("JellyPlayerPrefab");
            root.AddComponent<Rigidbody>();
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = 0.5f;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
            UnityEngine.Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(root.transform, false);
            var r = vis.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;

            root.AddComponent<JellyPhysicsBody>();
            root.AddComponent<JellyGroundDetector>();
            var input = root.AddComponent<JellyInputAdapter>();
            input.config = moveCfg;
            var motor = root.AddComponent<JellyMotor>();
            motor.config = moveCfg;
            root.AddComponent<JellyMorphController>();
            var def = root.AddComponent<JellyDeformationController>();
            def.visualRoot = vis.transform;
            var mc = root.AddComponent<JellyMassController>();
            mc.config = massCfg;
            var ctrl = root.AddComponent<JellyController>();
            ctrl.movementConfig = moveCfg;
            root.AddComponent<JellyFragment>();
            root.AddComponent<JellyFragmentRegistry>();
            root.AddComponent<JellyFragmentPool>();
            root.AddComponent<JellyFragmentInputRouter>();
            root.AddComponent<JellySplitController>();
            root.AddComponent<JellyMergeController>();

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/JellyPlayer.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreatePickupPrefab(Material pickupMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MassPickupPrefab";
            go.transform.localScale = Vector3.one * 0.65f;
            go.GetComponent<Collider>().isTrigger = true;
            var r = go.GetComponent<Renderer>();
            if (r != null && pickupMat != null) r.sharedMaterial = pickupMat;
            go.AddComponent<JellyMassPickup>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/MassPickup.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateGatePrefab(Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CircularGatePrefab";
            go.transform.localScale = new Vector3(6.8f, 2.2f, 0.5f);
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            go.AddComponent<ObstacleStateController>();
            go.AddComponent<CircularGate>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/CircularGate.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateGameplayScene(
            string path,
            List<JellyLevelDefinition> _,
            int startIndex,
            bool startInMainMenu,
            Material __,
            Material ___,
            Material ____,
            Material _____,
            Material ______,
            Material _______)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Reload persistent assets AFTER NewScene (NewScene unloads unreferenced ScriptableObjects in memory)
            var moveCfg = AssetDatabase.LoadAssetAtPath<JellyMovementConfig>($"{ConfigDir}/DefaultMovementConfig.asset");
            var massCfg = AssetDatabase.LoadAssetAtPath<JellyMassConfig>($"{ConfigDir}/DefaultMassConfig.asset");
            var jellyMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_JellyStylized.mat");
            var trackMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_TrackPastel.mat");
            var gateMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_GateAccent.mat");
            var goalMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_GoalEmerald.mat");
            var pickupMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_PickupLime.mat");
            var drainMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_DrainCrimson.mat");

            var fallbackCatalog = JellyLevelCatalogFactory.CreateFullCatalog50();
            var freshCatalog = new List<JellyLevelDefinition>(50);
            for (int i = 0; i < 50; i++)
            {
                var loaded = AssetDatabase.LoadAssetAtPath<JellyLevelDefinition>($"{LevelsDir}/Level_{i + 1:D2}.asset");
                freshCatalog.Add(loaded != null ? loaded : fallbackCatalog[i]);
            }

            JellyLevelDefinition activeDef = freshCatalog[Mathf.Clamp(startIndex, 0, freshCatalog.Count - 1)];

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.93f, 0.96f, 0.99f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                var camCtrl = cam.gameObject.AddComponent<JellyCameraController>();

                var sysGo = new GameObject("JellyGameSystems");
                var loader = sysGo.AddComponent<LevelLoader>();
                loader.movementConfig = moveCfg;
                loader.massConfig = massCfg;
                loader.jellyMaterial = jellyMat;
                loader.trackMaterial = trackMat;
                loader.gateMaterial = gateMat;
                loader.goalMaterial = goalMat;
                loader.pickupMaterial = pickupMat;
                loader.drainMaterial = drainMat;

                var cpMgr = sysGo.AddComponent<CheckpointManager>();
                var lvlMgr = sysGo.AddComponent<LevelManager>();
                sysGo.AddComponent<JellyFXPool>();

                lvlMgr.levelCatalog = freshCatalog;
                lvlMgr.currentLevelIndex = startIndex;
                lvlMgr.loader = loader;
                lvlMgr.checkpointManager = cpMgr;
                lvlMgr.cameraController = camCtrl;

                try
                {
                    lvlMgr.LoadLevel(activeDef);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JellyProjectBuilder] LoadLevel failed in {path}: {ex}");
                }

                // Enable Endless Winding Track + Cutout Gates + Pickup-driven Split/Grow/Shrink on SampleScene, MainMenu, and Level_01
                if (startIndex == 0)
                {
                    lvlMgr.enabled = false;
                    var endless = sysGo.AddComponent<EndlessTrackManager>();
                    endless.trackRoot = loader.levelRoot;
                    endless.primaryPlayer = lvlMgr.ActivePlayer;
                    endless.mainCamera = cam;
                    endless.jellyMaterial = jellyMat;
                    endless.trackMaterial = trackMat;
                    endless.gateMaterial = gateMat;
                    endless.goalMaterial = goalMat;
                    endless.pickupMaterial = pickupMat;
                    endless.drainMaterial = drainMat;
                    endless.BuildInitialPreviewInEditor(lvlMgr.ActivePlayer);
                }

                BuildSceneUI(lvlMgr, activeDef, startInMainMenu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void BuildSceneUI(LevelManager lvlMgr, JellyLevelDefinition def, bool startInMainMenu)
        {
            var canvasGo = new GameObject("JellyUICanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var safeRoot = new GameObject("SafeAreaRoot");
            safeRoot.transform.SetParent(canvasGo.transform, false);
            var safeRect = safeRoot.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            var fitter = safeRoot.AddComponent<JellySafeAreaFitter>();
            fitter.targetRect = safeRect;

            var hudPanel = CreatePanel(safeRoot.transform, "HUDPanel");
            var menuPanel = CreatePanel(safeRoot.transform, "MainMenuPanel");
            var pausePanel = CreatePanel(safeRoot.transform, "PausePanel");
            var completePanel = CreatePanel(safeRoot.transform, "LevelCompletePanel");

            bool isEndless = lvlMgr != null && lvlMgr.GetComponent<EndlessTrackManager>() != null;
            string title = isEndless
                ? "SONSUZ JÖLE KOŞUSU — Mesafe: 0m | Skor: 0"
                : (def != null ? $"{def.levelId.Replace('_', ' ')} — {def.displayName}" : "Level 01 — First Roll");
            var titleText = CreateLabel(hudPanel.transform, "LevelTitleLabel", title, new Vector2(0.5f, 0.94f));
            string defaultMassText = isEndless
                ? "Kütle: 90 | Durum: Tek Parça\nYeşil (+): Büyü | Kırmızı (-): Küçül | Mor (÷2): Bölün | Altın (><): Birleş"
                : $"Kütle: {(def != null ? def.startingMass : 100f):F0} | Parça: 1  [WASD: Hareket | Space: Bölün | E: Birleş | R: Yeniden | N/P: Bölüm Geç]";
            var massText = CreateLabel(hudPanel.transform, "MassLabel", defaultMassText, new Vector2(0.5f, 0.87f));
            var summaryText = CreateLabel(completePanel.transform, "SummaryLabel", "BÖLÜM TAMAMLANDI! Sonraki bölüme geçiliyor...", new Vector2(0.5f, 0.55f));

            var uiMgr = canvasGo.AddComponent<JellyUIManager>();
            uiMgr.levelManager = lvlMgr;
            uiMgr.hudPanel = hudPanel;
            uiMgr.mainMenuPanel = menuPanel;
            uiMgr.pausePanel = pausePanel;
            uiMgr.levelCompletePanel = completePanel;
            uiMgr.levelTitleLabel = titleText;
            uiMgr.massLabel = massText;
            uiMgr.completionSummaryLabel = summaryText;

            if (startInMainMenu) uiMgr.ShowMainMenu();
            else uiMgr.ShowHUD();
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, string content, Vector2 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(720f, 64f);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.color = new Color(0.15f, 0.20f, 0.28f);
            txt.fontSize = 16;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return txt;
        }
    }
}
