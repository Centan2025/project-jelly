using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class LevelLoader : MonoBehaviour
    {
        public Transform levelRoot;
        public JellyMovementConfig movementConfig;
        public JellyMassConfig massConfig;
        public Material jellyMaterial;
        public Material trackMaterial;
        public Material gateMaterial;
        public Material goalMaterial;
        public Material pickupMaterial;
        public Material drainMaterial;

        private readonly Dictionary<string, ObstacleStateController> _obstaclesById = new Dictionary<string, ObstacleStateController>();

        public void ClearCurrentLevel()
        {
            _obstaclesById.Clear();
            if (levelRoot == null) return;
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(levelRoot.GetChild(i).gameObject);
            }
        }

        public JellyController BuildLevelRuntime(JellyLevelDefinition def, LevelManager manager, CheckpointManager checkpointManager)
        {
            if (levelRoot == null)
            {
                Transform existingRoot = transform.Find("LevelRuntimeRoot");
                if (existingRoot == null)
                {
                    var foundGo = GameObject.Find("LevelRuntimeRoot");
                    if (foundGo != null) existingRoot = foundGo.transform;
                }
                if (existingRoot != null)
                {
                    levelRoot = existingRoot;
                }
                else
                {
                    var rootGo = new GameObject("LevelRuntimeRoot");
                    levelRoot = rootGo.transform;
                }
            }
            ClearCurrentLevel();

            bool hasCustomTrack = false;
            for (int i = 0; i < def.objects.Count; i++)
            {
                if (def.objects[i].type == LevelObjectType.Platform && def.objects[i].id.StartsWith("Track"))
                    hasCustomTrack = true;
            }

            if (!hasCustomTrack)
            {
                float dist = Mathf.Max(28f, def.goalPosition.z + 6f);
                CreateBox("MainTrackFloor", new Vector3(0f, -0.5f, dist * 0.5f - 3f), new Vector3(7f, 1f, dist), new Color(0.92f, 0.94f, 0.98f), trackMaterial);
                CreateBox("LeftRail", new Vector3(-3.65f, 0.35f, dist * 0.5f - 3f), new Vector3(0.3f, 0.7f, dist), new Color(0.82f, 0.86f, 0.92f), trackMaterial);
                CreateBox("RightRail", new Vector3(3.65f, 0.35f, dist * 0.5f - 3f), new Vector3(0.3f, 0.7f, dist), new Color(0.82f, 0.86f, 0.92f), trackMaterial);
            }

            for (int i = 0; i < def.gates.Count; i++)
            {
                var g = def.gates[i];
                var gateGo = CreateBox($"Gate_{g.gateId}", g.position, new Vector3(6.8f, 2.2f, 0.5f), new Color(0.95f, 0.58f, 0.45f), gateMaterial);
                var stateCtrl = gateGo.AddComponent<ObstacleStateController>();
                stateCtrl.visualDoor = gateGo.transform;
                _obstaclesById[g.gateId] = stateCtrl;

                switch (g.gateType)
                {
                    case LevelObjectType.CircularGate:
                        var cg = gateGo.AddComponent<CircularGate>();
                        cg.apertureRadius = g.apertureOrHalfWidth;
                        break;
                    case LevelObjectType.NarrowSlotGate:
                        var ng = gateGo.AddComponent<NarrowSlotGate>();
                        ng.slotHalfWidth = g.apertureOrHalfWidth;
                        ng.slotHalfHeight = 0.6f;
                        break;
                    case LevelObjectType.MassThresholdGate:
                        var mg = gateGo.AddComponent<MassThresholdGate>();
                        mg.minAllowedMass = g.minMass;
                        mg.maxAllowedMass = g.maxMass;
                        mg.apertureRadius = g.apertureOrHalfWidth;
                        break;
                    case LevelObjectType.SplitGate:
                        var sg = gateGo.AddComponent<SplitGate>();
                        sg.maxFragmentMass = g.maxMass > 0f ? g.maxMass : 60f;
                        sg.channelHalfWidth = g.apertureOrHalfWidth;
                        sg.requireSplitState = g.requireSplit;
                        break;
                    case LevelObjectType.TimedGate:
                        var tg = gateGo.AddComponent<TimedGate>();
                        tg.openDuration = Mathf.Max(0.5f, g.minMass);
                        tg.closedDuration = Mathf.Max(0.5f, g.maxMass);
                        break;
                }
            }

            for (int i = 0; i < def.objects.Count; i++)
            {
                SpawnPlacement(def.objects[i]);
            }

            for (int i = 0; i < def.checkpoints.Count; i++)
            {
                var cp = def.checkpoints[i];
                var cpGo = new GameObject($"Checkpoint_{cp.checkpointId}");
                cpGo.transform.SetParent(levelRoot, false);
                cpGo.transform.position = cp.position;
                var sc = cpGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = Mathf.Max(0.8f, cp.radius);
                var trig = cpGo.AddComponent<CheckpointTrigger>();
                trig.checkpointId = cp.checkpointId;
                trig.manager = checkpointManager;
            }

            var goalGo = CreateBox("GoalZone", def.goalPosition, def.goalScale, new Color(0.45f, 0.92f, 0.65f), goalMaterial);
            goalGo.GetComponent<Collider>().isTrigger = true;
            var goalZone = goalGo.AddComponent<LevelGoalZone>();
            goalZone.levelManager = manager;

            JellyController player = SpawnPlayer(def.startPosition, Quaternion.Euler(def.startEulerRotation), def.startingMass);
            return player;
        }

        private void SpawnPlacement(LevelObjectPlacement p)
        {
            Vector3 scale = p.scale.sqrMagnitude > 0.001f ? p.scale : Vector3.one;
            Quaternion rot = Quaternion.Euler(p.eulerRotation);

            switch (p.type)
            {
                case LevelObjectType.Platform:
                case LevelObjectType.Ramp:
                case LevelObjectType.Wall:
                    var plat = CreateBox(p.id, p.position, scale, new Color(0.86f, 0.90f, 0.96f), trackMaterial);
                    plat.transform.rotation = rot;
                    break;

                case LevelObjectType.MassPickup:
                    var pick = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    pick.name = $"Pickup_{p.id}";
                    pick.transform.SetParent(levelRoot, false);
                    pick.transform.SetPositionAndRotation(p.position, rot);
                    pick.transform.localScale = Vector3.one * 0.65f;
                    pick.GetComponent<Collider>().isTrigger = true;
                    ApplyMaterialOrColor(pick, pickupMaterial, new Color(0.35f, 0.95f, 0.55f));
                    var mp = pick.AddComponent<JellyMassPickup>();
                    mp.massGain = p.paramA > 0f ? p.paramA : 20f;
                    break;

                case LevelObjectType.MassDrain:
                    var drain = CreateBox($"Drain_{p.id}", p.position, scale, new Color(0.95f, 0.35f, 0.42f), drainMaterial);
                    drain.GetComponent<Collider>().isTrigger = true;
                    var md = drain.AddComponent<JellyMassDrain>();
                    md.massLoss = p.paramA > 0f ? p.paramA : 20f;
                    md.continuousDrain = p.paramB > 0.5f;
                    break;

                case LevelObjectType.PressurePlate:
                    var plate = CreateBox($"PressurePlate_{p.id}", p.position, new Vector3(2.2f, 0.35f, 2.2f), new Color(0.98f, 0.82f, 0.32f), goalMaterial);
                    plate.GetComponent<Collider>().isTrigger = true;
                    var wp = plate.AddComponent<WeightedPressurePlate>();
                    wp.requiredMass = p.paramA > 0f ? p.paramA : 110f;
                    if (!string.IsNullOrEmpty(p.linkedTargetId) && _obstaclesById.TryGetValue(p.linkedTargetId, out var targetObs))
                        wp.targetObstacle = targetObs;
                    break;

                case LevelObjectType.MomentumBarrier:
                    var bar = CreateBox($"MomentumBarrier_{p.id}", p.position, scale, new Color(0.95f, 0.48f, 0.25f), gateMaterial);
                    var obs = bar.AddComponent<ObstacleStateController>();
                    obs.visualDoor = bar.transform;
                    var mb = bar.AddComponent<MomentumBreakableBarrier>();
                    mb.requiredMomentum = p.paramA > 0f ? p.paramA : 750f;
                    mb.stateController = obs;
                    break;

                case LevelObjectType.PushableBlock:
                    var blk = CreateBox($"PushBlock_{p.id}", p.position, scale, new Color(0.65f, 0.75f, 0.95f), trackMaterial);
                    blk.AddComponent<Rigidbody>();
                    var pb = blk.AddComponent<PushableBlock>();
                    pb.minimumPlayerMassToPush = p.paramA > 0f ? p.paramA : 90f;
                    break;

                case LevelObjectType.MassSensitiveBridge:
                    var brg = CreateBox($"Bridge_{p.id}", p.position, scale, new Color(0.92f, 0.78f, 0.62f), trackMaterial);
                    var msb = brg.AddComponent<MassSensitiveBridge>();
                    msb.maxSafeMass = p.paramA > 0f ? p.paramA : 120f;
                    break;

                case LevelObjectType.MovingPlatform:
                    var mov = CreateBox($"MovingPlatform_{p.id}", p.position, scale, new Color(0.55f, 0.85f, 0.95f), trackMaterial);
                    var mpComp = mov.AddComponent<MovingPlatform>();
                    mpComp.pointA = p.position;
                    mpComp.pointB = p.position + new Vector3(p.paramA != 0f ? p.paramA : 3f, 0f, 0f);
                    mpComp.speed = p.paramB > 0f ? p.paramB : 1.5f;
                    break;

                case LevelObjectType.RotatingObstacle:
                    var rotGo = CreateBox($"RotatingObstacle_{p.id}", p.position, scale, new Color(0.95f, 0.45f, 0.55f), gateMaterial);
                    var ro = rotGo.AddComponent<RotatingObstacle>();
                    ro.angularVelocityEuler = new Vector3(0f, p.paramA != 0f ? p.paramA : 90f, 0f);
                    break;

                case LevelObjectType.SpringLauncher:
                    var spr = CreateBox($"SpringLauncher_{p.id}", p.position, scale, new Color(0.4f, 0.95f, 0.85f), goalMaterial);
                    spr.GetComponent<Collider>().isTrigger = true;
                    var sl = spr.AddComponent<SpringLauncher>();
                    sl.launchVelocity = new Vector3(0f, p.paramA > 0f ? p.paramA : 6.5f, p.paramB > 0f ? p.paramB : 11f);
                    break;

                case LevelObjectType.FrictionZone:
                    var fz = CreateBox($"FrictionZone_{p.id}", p.position, scale, new Color(0.70f, 0.92f, 0.99f), trackMaterial);
                    var sfz = fz.AddComponent<SurfaceFrictionZone>();
                    sfz.frictionMultiplier = p.paramA > 0f ? p.paramA : 0.25f;
                    break;
            }
        }

        private GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Color color, Material sharedMat = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(levelRoot, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            ApplyMaterialOrColor(go, sharedMat, color);
            return go;
        }

        public static void ApplyMaterialOrColor(GameObject go, Material sharedMat, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (sharedMat != null)
            {
                r.sharedMaterial = sharedMat;
                return;
            }
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh != null && Application.isPlaying)
            {
                var mat = new Material(sh) { color = c };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                r.material = mat;
            }
        }

        public JellyController SpawnPlayer(Vector3 pos, Quaternion rot, float startingMass)
        {
            GameObject playerGo = new GameObject("JellyPlayer");
            playerGo.transform.SetParent(levelRoot, false);
            playerGo.transform.SetPositionAndRotation(pos, rot);

            playerGo.AddComponent<Rigidbody>();
            var sc = playerGo.AddComponent<SphereCollider>();
            sc.radius = 0.5f;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
            DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(playerGo.transform, false);
            ApplyMaterialOrColor(vis, jellyMaterial, new Color(0.32f, 0.78f, 0.98f, 0.88f));

            playerGo.AddComponent<JellyPhysicsBody>();
            playerGo.AddComponent<JellyGroundDetector>();
            var input = playerGo.AddComponent<JellyInputAdapter>();
            var motor = playerGo.AddComponent<JellyMotor>();
            playerGo.AddComponent<JellyMorphController>();
            var deform = playerGo.AddComponent<JellyDeformationController>();
            deform.visualRoot = vis.transform;
            var massCtrl = playerGo.AddComponent<JellyMassController>();
            if (massConfig != null) massCtrl.config = massConfig;
            var controller = playerGo.AddComponent<JellyController>();
            if (movementConfig != null)
            {
                controller.movementConfig = movementConfig;
                input.config = movementConfig;
                motor.config = movementConfig;
            }
            playerGo.AddComponent<JellyFragment>();
            playerGo.AddComponent<JellyFragmentRegistry>();
            playerGo.AddComponent<JellyFragmentPool>();
            playerGo.AddComponent<JellyFragmentInputRouter>();
            var split = playerGo.AddComponent<JellySplitController>();
            playerGo.AddComponent<JellyMergeController>();

            controller.ResetState(pos, rot, startingMass);
            split.EnsureSetup();
            return controller;
        }
    }
}
