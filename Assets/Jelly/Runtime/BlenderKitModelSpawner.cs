using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    /// <summary>
    /// Loads and instantiates the high-detail 3D Blender models from ProjectJelly_Kit.fbx
    /// (Resources/Models/ProjectJelly_Kit) at runtime and in EditMode, upgrading materials
    /// (including procedural diagonal yellow/black hazard textures and glossy URP shaders)
    /// and binding animated obstacle parts to EndlessGateObstacle.
    /// </summary>
    public static class BlenderKitModelSpawner
    {
        private static GameObject _cachedKitRoot;
        private static Texture2D _hazardStripeTexture;
        private static Texture2D _softRadialDiscTexture;
        private static Texture2D _jellyGlossGradientTexture;
        private static Mesh _uprightJellyDomeBaseMesh;
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public static GameObject GetKitRoot()
        {
            if (_cachedKitRoot != null)
                return _cachedKitRoot;

            _cachedKitRoot = Resources.Load<GameObject>("Models/ProjectJelly_Kit");

#if UNITY_EDITOR
            if (_cachedKitRoot == null)
            {
                _cachedKitRoot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jelly/Models/ProjectJelly_Kit.fbx");
            }
            if (_cachedKitRoot == null)
            {
                _cachedKitRoot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jelly/Resources/Models/ProjectJelly_Kit.fbx");
            }
#endif
            return _cachedKitRoot;
        }

        public static Transform FindGroupTemplate(string groupName)
        {
            GameObject kit = GetKitRoot();
            if (kit == null)
                return null;

            string fullName = groupName.StartsWith("GRP_") ? groupName : "GRP_" + groupName;
            Transform direct = kit.transform.Find(fullName);
            if (direct != null)
                return direct;

            Transform[] all = kit.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == fullName)
                    return all[i];
            }
            return null;
        }

        public static Mesh FindMeshInGroup(string groupName, string meshNamePrefix)
        {
            Transform grp = FindGroupTemplate(groupName);
            if (grp == null)
                return null;

            MeshFilter[] filters = grp.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null && filters[i].gameObject.name.StartsWith(meshNamePrefix))
                    return filters[i].sharedMesh;
            }
            return null;
        }

        /// <summary>
        /// Upgrades a player Jelly GameObject to use the upright Y-up organic plump flat-bottomed dome mesh
        /// from ProjectJelly_Kit AND attaches JellyRealtimeDeformer for continuous 3D vertex wave/rolling/jiggle physics.
        /// Also strips legacy primitive eyeball decorations (GlossHighlight / JellyInnerCore).
        /// </summary>
        public static void ApplyBlenderJellyMesh(GameObject jellyGo)
        {
            if (jellyGo == null)
                return;

            StripLegacyJellyPrimitives(jellyGo.transform);
            if (jellyGo.transform.parent != null)
            {
                StripLegacyJellyPrimitives(jellyGo.transform.parent);
            }

            MeshFilter mf = jellyGo.GetComponent<MeshFilter>();
            if (mf == null)
            {
                Transform vis = jellyGo.transform.Find("Visual");
                if (vis != null)
                {
                    mf = vis.GetComponent<MeshFilter>();
                }
            }
            if (mf == null)
                return;

            Mesh baseDome = GetOrCreateUprightJellyDomeBaseMesh();
            if (baseDome == null)
                return;

            var deformer = mf.gameObject.GetComponent<JellyRealtimeDeformer>();
            if (deformer == null)
            {
                deformer = mf.gameObject.AddComponent<JellyRealtimeDeformer>();
            }
            deformer.InitializeWithBaseMesh(baseDome);
        }

        public static void ApplyJellySkinMaterial(GameObject jellyGo, Color tintColor)
        {
            if (jellyGo == null)
                return;

            var deformer = jellyGo.GetComponentInChildren<JellyRealtimeDeformer>();
            if (deformer != null)
            {
                deformer.SetJellyTint(tintColor);
            }
        }

        public static void ApplySoftShadowDiscMaterial(GameObject discGo, Color glowColor)
        {
            if (discGo == null)
                return;

            var r = discGo.GetComponent<Renderer>();
            if (r == null)
                return;

            string key = "SoftShadowDisc_" + ColorUtility.ToHtmlStringRGBA(glowColor);
            if (!MaterialCache.TryGetValue(key, out Material mat) || mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
                mat = new Material(sh) { name = key };
                Texture2D radialTex = GetOrCreateSoftRadialDiscTexture();
                ConfigureTransparentURPMaterial(mat, glowColor, radialTex);
                MaterialCache[key] = mat;
            }
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void StripLegacyJellyPrimitives(Transform root)
        {
            if (root == null) return;
#if UNITY_EDITOR
            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject);
#endif
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform ch = root.GetChild(i);
#if UNITY_EDITOR
                UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(ch.gameObject);
#endif
                if (ch.name == "GlossHighlight" || ch.name == "JellyInnerCore")
                {
                    if (Application.isPlaying) Object.Destroy(ch.gameObject);
                    else Object.DestroyImmediate(ch.gameObject);
                }
            }
        }

        public static Mesh GetOrCreateUprightJellyDomeBaseMesh()
        {
            if (_uprightJellyDomeBaseMesh != null)
                return _uprightJellyDomeBaseMesh;

            // Build a wide, plump, low-profile rounded Jelly cushion/dome (matching Reference Section 1:
            // wider than tall, broad rounded top, flat puddle-like base — NEVER a vertical pointed egg!)
            const int rings = 28;
            const int segs = 44;
            int vertCount = (rings + 1) * (segs + 1);
            var rawV = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[rings * segs * 6];

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            int vIdx = 0;
            for (int r = 0; r <= rings; r++)
            {
                float v01 = r / (float)rings;
                float phi = (v01 - 0.5f) * Mathf.PI; // -pi/2 (bottom) .. +pi/2 (top)
                float sy = Mathf.Sin(phi);
                float cosPhi = Mathf.Max(0f, Mathf.Cos(phi));

                // Superellipse crown (pow 0.74) keeps the upper dome broad and round instead of pinching into an egg tip!
                float broadCrown = sy > 0f ? Mathf.Pow(cosPhi, 0.74f) : Mathf.Pow(cosPhi, 0.85f);
                // Softly flattened bottom cushion (sy < 0 -> compressed to 0.38)
                float yShaped = sy < 0f ? sy * 0.38f : Mathf.Pow(sy, 0.86f) * 0.82f;
                // Gentle chubby cheek fullness around equator
                float cheekBulge = 1.0f + 0.08f * Mathf.Cos(phi);

                for (int s = 0; s <= segs; s++)
                {
                    float u01 = s / (float)segs;
                    float theta = u01 * Mathf.PI * 2f;
                    float x = broadCrown * Mathf.Cos(theta) * cheekBulge;
                    float z = broadCrown * Mathf.Sin(theta) * cheekBulge;
                    float y = yShaped;

                    Vector3 p = new Vector3(x, y, z);
                    rawV[vIdx] = p;
                    if (p.x < min.x) min.x = p.x;
                    if (p.y < min.y) min.y = p.y;
                    if (p.z < min.z) min.z = p.z;
                    if (p.x > max.x) max.x = p.x;
                    if (p.y > max.y) max.y = p.y;
                    if (p.z > max.z) max.z = p.z;
                    vIdx++;
                }
            }

            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;
            float invX = size.x > 0.001f ? 1f / size.x : 1f;
            float invY = size.y > 0.001f ? 1f / size.y : 1f;
            float invZ = size.z > 0.001f ? 1f / size.z : 1f;

            // Proportion as a wide, chubby horizontal jelly cushion:
            // Width X = 1.14 (-0.57..+0.57), Height Y = 0.66 (-0.33..+0.33), Depth Z = 0.96 (-0.48..+0.48)
            const float targetWidthX = 1.14f;
            const float targetHeightY = 0.66f;
            const float targetDepthZ = 0.96f;

            for (int i = 0; i < vertCount; i++)
            {
                float nx = (rawV[i].x - center.x) * invX; // -0.5 .. +0.5
                float ny = (rawV[i].y - center.y) * invY; // -0.5 .. +0.5
                float nz = (rawV[i].z - center.z) * invZ; // -0.5 .. +0.5

                rawV[i] = new Vector3(nx * targetWidthX, ny * targetHeightY, nz * targetDepthZ);
                uvs[i] = new Vector2(Mathf.Clamp01(nx + 0.5f), Mathf.Clamp01(ny + 0.5f));
            }

            int tIdx = 0;
            int stride = segs + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int i0 = r * stride + s;
                    int i1 = i0 + 1;
                    int i2 = (r + 1) * stride + s;
                    int i3 = i2 + 1;

                    tris[tIdx++] = i0;
                    tris[tIdx++] = i2;
                    tris[tIdx++] = i1;

                    tris[tIdx++] = i1;
                    tris[tIdx++] = i2;
                    tris[tIdx++] = i3;
                }
            }

            _uprightJellyDomeBaseMesh = new Mesh
            {
                name = "UprightJellyDomeBaseMesh",
                vertices = rawV,
                uv = uvs,
                triangles = tris
            };
            _uprightJellyDomeBaseMesh.RecalculateNormals();
            _uprightJellyDomeBaseMesh.RecalculateBounds();
            return _uprightJellyDomeBaseMesh;
        }

        /// <summary>
        /// Instantiates a 3D model group from ProjectJelly_Kit centered inside a new holder GameObject.
        /// Strips 3D floor label meshes (LBL_*) and upgrades materials for URP/Standard rendering.
        /// </summary>
        public static GameObject InstantiateModelGroup(string groupName, Transform parent, bool alignBottomToZero, float uniformScale = 1.0f)
        {
            Transform template = FindGroupTemplate(groupName);
            if (template == null)
                return null;

            var holder = new GameObject(groupName);
            holder.transform.position = Vector3.zero;
            holder.transform.rotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            GameObject instance = Object.Instantiate(template.gameObject, holder.transform, false);
            instance.name = template.name;
            instance.transform.localPosition = Vector3.zero;
            // In Blender, models face -Y (towards the Blender camera), which FBX (-90 deg X) maps to +Z (away from Unity camera).
            // Rotating 180 deg around World Y makes the front face (-Y in Blender) face -Z (directly towards the Unity camera)!
            if (!groupName.Contains("MultiHole"))
            {
                instance.transform.Rotate(0f, 180f, 0f, Space.World);
            }

            StripFloorLabelsAndSetupRenderers(instance);

            // Compute world bounds while holder is at origin & identity rotation
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }

                float targetY = alignBottomToZero ? combined.min.y : combined.center.y;
                instance.transform.position -= new Vector3(combined.center.x, targetY, combined.center.z);
            }

            if (Mathf.Abs(uniformScale - 1.0f) > 0.001f)
            {
                holder.transform.localScale = Vector3.one * uniformScale;
            }

            if (parent != null)
            {
                holder.transform.SetParent(parent, false);
            }

            return holder;
        }

        public static string GetPickupGroupName(EndlessPickupType type)
        {
            switch (type)
            {
                case EndlessPickupType.Grow: return "Pickup_Grow";
                case EndlessPickupType.Shrink: return "Pickup_Shrink";
                case EndlessPickupType.Split: return "Pickup_Split";
                case EndlessPickupType.Merge: return "Pickup_Merge";
                case EndlessPickupType.SpeedBoost: return "Pickup_Speed";
                case EndlessPickupType.Shield: return "Pickup_Shield";
                case EndlessPickupType.Magnet: return "Pickup_Magnet";
                case EndlessPickupType.StarScore: return "Pickup_Star";
                case EndlessPickupType.SlowTime: return "Pickup_Slow";
                default: return "Pickup_Grow";
            }
        }

        public static string GetObstacleGroupName(EndlessObstacleType type, float trackZ)
        {
            switch (type)
            {
                case EndlessObstacleType.SmallCenterHole:
                case EndlessObstacleType.LeftOpeningWall:
                case EndlessObstacleType.RightOpeningWall:
                case EndlessObstacleType.SlidingDoorWall:
                case EndlessObstacleType.PincerShutters:
                    return "Gate_Narrow";
                case EndlessObstacleType.VerticalCrusherGate:
                    return "Gate_Piston";
                case EndlessObstacleType.HeavySmashWall:
                    return "Obstacle_GlassWall";
                case EndlessObstacleType.TwinSplitTunnels:
                    return "Tunnel_Twin";
                case EndlessObstacleType.MovingTwinTunnels:
                    return "Tunnel_Moving";
                case EndlessObstacleType.SwingingPendulum:
                    return "Obstacle_Pendulum";
                case EndlessObstacleType.SpinnerBar:
                    return "Obstacle_Spinner";
                case EndlessObstacleType.MultiHoleDecisionWall:
                {
                    int vIdx = Mathf.Abs(Mathf.RoundToInt(trackZ / 12f)) % 3;
                    return vIdx == 0
                        ? "Gate_MultiHole_SizeChoice"
                        : (vIdx == 1 ? "Gate_MultiHole_SplitChoice" : "Gate_MultiHole_RiskReward");
                }
                case EndlessObstacleType.FlexibleSqueezeGate:
                    return "Gate_FlexibleSqueeze";
                case EndlessObstacleType.MassScaleGate:
                    return "Gate_MassScale";
                case EndlessObstacleType.SlidingHoleWall:
                    return "Gate_SlidingHoleWall";
                case EndlessObstacleType.MicroShatterGate:
                    return "Gate_MicroShatter";
                default:
                    return "Gate_Narrow";
            }
        }

        /// <summary>
        /// Populates an obstacle root with the high-detail 3D Blender model and binds moving/breakable parts.
        /// Returns true if the 3D Blender model was found and instantiated.
        /// </summary>
        public static bool PopulateObstacleWithBlenderModel(Transform gateRoot, EndlessGateObstacle obs)
        {
            if (gateRoot == null || obs == null)
                return false;

            string grpName;
            if (obs.obstacleType == EndlessObstacleType.MultiHoleDecisionWall && obs.multiHoleConfig != null)
            {
                switch (obs.multiHoleConfig.VariantType)
                {
                    case MultiHoleVariantType.SizeChoice:
                        grpName = "Gate_MultiHole_SizeChoice";
                        break;
                    case MultiHoleVariantType.SplitChoice:
                        grpName = "Gate_MultiHole_SplitChoice";
                        break;
                    default:
                        grpName = "Gate_MultiHole_RiskReward";
                        break;
                }
            }
            else
            {
                grpName = GetObstacleGroupName(obs.obstacleType, obs.trackZ);
            }

            GameObject modelHolder = InstantiateModelGroup(grpName, gateRoot, alignBottomToZero: true);
            if (modelHolder == null)
                return false;

            modelHolder.transform.localPosition = Vector3.zero;
            modelHolder.transform.localRotation = Quaternion.identity;

            if (obs.obstacleType == EndlessObstacleType.MultiHoleDecisionWall && obs.multiHoleConfig == null)
            {
                int vIdx = Mathf.Abs(Mathf.RoundToInt(obs.trackZ / 12f)) % 3;
                MultiHoleVariantType vType = vIdx == 0
                    ? MultiHoleVariantType.SizeChoice
                    : (vIdx == 1 ? MultiHoleVariantType.SplitChoice : MultiHoleVariantType.RiskRewardChoice);
                obs.multiHoleConfig = MultiHoleWallConfig.CreatePreset(vType);
            }

            BindObstacleSubParts(modelHolder.transform, obs);
            return true;
        }

        private static void BindObstacleSubParts(Transform modelRoot, EndlessGateObstacle obs)
        {
            Transform[] children = modelRoot.GetComponentsInChildren<Transform>(true);

            switch (obs.obstacleType)
            {
                case EndlessObstacleType.VerticalCrusherGate:
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (children[i].name.StartsWith("Piston_CrusherBar"))
                        {
                            obs.movingPartA = children[i];
                            break;
                        }
                    }
                    break;
                }

                case EndlessObstacleType.HeavySmashWall:
                {
                    var smashGroup = new GameObject("BreakableVoronoiCluster").transform;
                    smashGroup.SetParent(modelRoot, false);
                    smashGroup.localPosition = Vector3.zero;
                    smashGroup.localRotation = Quaternion.identity;
                    smashGroup.localScale = Vector3.one;

                    for (int i = 0; i < children.Length; i++)
                    {
                        Transform ch = children[i];
                        if (ch != null && (ch.name.StartsWith("Glass_VoronoiBlock") || ch.name.StartsWith("Glass_CrackCore")))
                        {
                            ch.SetParent(smashGroup, true);
                        }
                    }
                    obs.breakableCenterPiece = smashGroup;
                    break;
                }

                case EndlessObstacleType.MovingTwinTunnels:
                {
                    obs.movingPartA = modelRoot;
                    break;
                }

                case EndlessObstacleType.SpinnerBar:
                {
                    var rotorGroup = new GameObject("SpinnerRotorGroup").transform;
                    rotorGroup.SetParent(modelRoot, true);
                    rotorGroup.localPosition = new Vector3(0f, 1.50f, 0f);
                    rotorGroup.localRotation = Quaternion.identity;

                    for (int i = 0; i < children.Length; i++)
                    {
                        Transform ch = children[i];
                        if (ch != null && (ch.name.StartsWith("Spin_HorizArms") || ch.name.StartsWith("Spin_EndCap") || ch.name.StartsWith("Spin_CenterHub")))
                        {
                            ch.SetParent(rotorGroup, true);
                        }
                    }
                    obs.movingPartA = rotorGroup;
                    break;
                }

                case EndlessObstacleType.SlidingHoleWall:
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (children[i].name.StartsWith("SlideHole_Cut_Collar"))
                        {
                            obs.movingPartA = children[i];
                            break;
                        }
                    }
                    break;
                }
            }
        }

        private static void StripFloorLabelsAndSetupRenderers(GameObject instance)
        {
            Transform[] all = instance.GetComponentsInChildren<Transform>(true);
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string tn = t.name;
                // Strip 3D floor text labels AND Blender studio dark-interior plug cylinders so holes/tunnels are 100% open in Unity!
                if (tn.StartsWith("LBL_") || tn.StartsWith("Narrow_HoleDark") || tn.Contains("_DarkBack_"))
                {
                    toDestroy.Add(t.gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (Application.isPlaying)
                    Object.Destroy(toDestroy[i]);
                else
                    Object.DestroyImmediate(toDestroy[i]);
            }

            // Place 3D "+" and "-" emblems INSIDE the center of the translucent Pickup_Grow and Pickup_Shrink spheres!
            Transform[] remaining = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < remaining.Length; i++)
            {
                Transform t = remaining[i];
                if (t == null) continue;
                string n = t.name;
                if (n.StartsWith("Grow_BarH") || n.StartsWith("Grow_BarV") || n.StartsWith("Shrink_BarH") ||
                    n.StartsWith("Grow_Plus") || n.StartsWith("Shrink_Minus"))
                {
                    Vector3 lp = t.localPosition;
                    t.localPosition = new Vector3(0f, 0f, lp.z);
                    t.localScale = new Vector3(1.12f, 1.85f, 1.12f);
                }
                else if (n.StartsWith("Grow_Body") || n.StartsWith("Shrink_Body"))
                {
                    var bodyRenderer = t.GetComponent<Renderer>();
                    if (bodyRenderer != null)
                    {
                        bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        bodyRenderer.receiveShadows = false;
                    }
                }
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material upgraded = UpgradeBlenderMaterial(mats[m], r.gameObject.name);
                    if (upgraded != null && upgraded != mats[m])
                    {
                        mats[m] = upgraded;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }

        private static Material UpgradeBlenderMaterial(Material srcMat, string objectName)
        {
            string matName = srcMat != null ? srcMat.name : objectName;
            string cacheKey = matName + "_" + objectName;
            if (MaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
                return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return srcMat;

            var m = new Material(shader) { name = matName + "_URP" };

            // Check if this is a yellow/black diagonal hazard striped part
            if (matName.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("CrusherBar", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Texture2D hzTex = GetOrCreateHazardTexture();
                m.color = Color.white;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", hzTex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", hzTex);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.72f);
                MaterialCache[cacheKey] = m;
                return m;
            }

            // Resolve color: FBX exporter writes grey/white (0.8, 0.8, 0.8) for Blender procedural node materials (mat_jelly),
            // so always prioritize explicit semantic color mapping or saturated imported colors!
            Color baseCol = ResolveBlenderMaterialColor(srcMat, matName, objectName);

            m.color = baseCol;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.86f);
            if (m.HasProperty("_Metallic"))
            {
                bool isMetal = matName.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               matName.IndexOf("Silver", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               matName.IndexOf("Metal", System.StringComparison.OrdinalIgnoreCase) >= 0;
                m.SetFloat("_Metallic", isMetal ? 0.45f : 0.08f);
            }

            string combinedLower = (matName + " " + objectName).ToLowerInvariant();
            bool isTranslucentPickupSphere = combinedLower.Contains("grow_body") ||
                                             combinedLower.Contains("grow_glass") ||
                                             combinedLower.Contains("shrink_body") ||
                                             combinedLower.Contains("shrink_glass") ||
                                             combinedLower.Contains("slow_glass");

            if (isTranslucentPickupSphere)
            {
                baseCol.a = 0.42f;
                ConfigureTransparentURPMaterial(m, baseCol, null);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.96f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.96f);
            }

            bool isPureWhiteEmblem = combinedLower.Contains("barh") ||
                                     combinedLower.Contains("barv") ||
                                     combinedLower.Contains("whiteicon") ||
                                     combinedLower.Contains("highlight") ||
                                     combinedLower.Contains("splittext") ||
                                     combinedLower.Contains("split_text") ||
                                     combinedLower.Contains("plus") ||
                                     combinedLower.Contains("minus") ||
                                     combinedLower.Contains("divide") ||
                                     combinedLower.Contains("heart") ||
                                     combinedLower.Contains("glint");

            bool isArrowEmblem = combinedLower.Contains("downarrow") ||
                                 combinedLower.Contains("arrowl") ||
                                 combinedLower.Contains("arrowr") ||
                                 combinedLower.Contains("pistonarrow") ||
                                 combinedLower.Contains("tunnelarrow");

            bool isEmissive = isPureWhiteEmblem ||
                              isArrowEmblem ||
                              combinedLower.Contains("neon") ||
                              combinedLower.Contains("glow") ||
                              combinedLower.Contains("rim") ||
                              combinedLower.Contains("white") ||
                              combinedLower.Contains("crack") ||
                              combinedLower.Contains("chevron") ||
                              combinedLower.Contains("grow") ||
                              combinedLower.Contains("shrink") ||
                              combinedLower.Contains("star");

            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                float emMult = isPureWhiteEmblem ? 1.85f : (isTranslucentPickupSphere ? 0.32f : (isEmissive ? 0.55f : 0.20f));
                m.SetColor("_EmissionColor", new Color(baseCol.r, baseCol.g, baseCol.b, 1f) * emMult);
            }

            MaterialCache[cacheKey] = m;
            return m;
        }

        private static Color ResolveBlenderMaterialColor(Material srcMat, string matName, string objName)
        {
            string s = (matName + " " + objName).ToLowerInvariant();

            // 1. Explicit high-contrast pure white emblems (+, -, ÷2, ♥, highlights, silver magnet tips)
            if (s.Contains("barh") || s.Contains("barv") || s.Contains("whiteicon") ||
                s.Contains("highlight") || s.Contains("splittext") || s.Contains("split_text") ||
                s.Contains("plus") || s.Contains("minus") || s.Contains("divide") ||
                s.Contains("heart") || s.Contains("glint") ||
                s.Contains("silver") || s.Contains("magnet_tip"))
            {
                return new Color(0.98f, 0.99f, 1.00f);
            }

            // 2. Aura particles around Grow (+) and Shrink (-) orbs match their orb color (not white!)
            if (s.Contains("grow_spark") || s.Contains("grow_particle"))
                return new Color(0.38f, 1.00f, 0.58f);
            if (s.Contains("shrink_spark") || s.Contains("shrink_particle"))
                return new Color(1.00f, 0.42f, 0.58f);

            // 3. Explicit Gate_Narrow & Gate_MultiHole palette (prevents "narrow" matching "arrow"!)
            if (s.Contains("rivet"))
                return new Color(1.00f, 0.78f, 0.12f); // Gold rivets
            if (s.Contains("pedestalrim"))
                return new Color(0.26f, 0.68f, 1.00f); // Cyan-Blue glowing pedestal rim
            if (s.Contains("pedestal"))
                return new Color(0.28f, 0.20f, 0.58f); // Deep Purple-Blue pedestal base
            if (s.Contains("holecollar") || s.Contains("narrowcollar") || s.Contains("panelseam") ||
                s.Contains("darktrim") || s.Contains("buttressfoot"))
                return new Color(0.24f, 0.16f, 0.56f); // Deep Violet-Indigo collar & trim
            if (s.Contains("buttressrib") || s.Contains("buttressneon") || s.Contains("trimmag"))
                return new Color(0.92f, 0.26f, 0.98f); // Glowing Neon Magenta buttress ribs & arch trim
            if (s.Contains("narrow_wall") || s.Contains("narrowwallindigo") || s.Contains("wallindigo") ||
                s.Contains("narrow_buttress") || s.Contains("buttressindigo"))
                return new Color(0.36f, 0.26f, 0.76f); // Rich Indigo-Purple main gate wall

            // 4. Explicit Pickup & Obstacle colors (prevents Blender procedural node materials from importing as white/grey!)
            if (s.Contains("grow") || s.Contains("green") || s.Contains("jump"))
                return new Color(0.10f, 0.92f, 0.38f); // Vibrant Emerald Green (+)
            if (s.Contains("shrink") || s.Contains("red") || s.Contains("magnet"))
                return new Color(1.00f, 0.16f, 0.26f); // Vibrant Ruby Red (-) / Magnet
            if (s.Contains("merge") || s.Contains("chevron") || s.Contains("star") ||
                s.Contains("gold") || s.Contains("yellow") || s.Contains("sand") ||
                s.Contains("sticky") || s.Contains("honey") ||
                s.Contains("downarrow") || s.Contains("arrowl") || s.Contains("arrowr") ||
                s.Contains("pistonarrow") || s.Contains("tunnelarrow") ||
                s.Contains("keystone") || s.Contains("spike"))
                return new Color(1.00f, 0.78f, 0.12f); // Vibrant Golden Yellow
            if (s.Contains("speed") || s.Contains("cyan") || s.Contains("ice") ||
                s.Contains("glass") || s.Contains("voronoi") || s.Contains("crack") ||
                s.Contains("island") || s.Contains("barrier"))
                return new Color(0.14f, 0.84f, 1.00f); // Electric Ice Cyan
            if (s.Contains("split") || s.Contains("diamond") || s.Contains("crystal") ||
                s.Contains("shield"))
                return new Color(0.76f, 0.24f, 0.98f); // Vibrant Amethyst / Shield Violet
            if (s.Contains("pink") || s.Contains("leaf") || s.Contains("canopy"))
                return new Color(1.00f, 0.42f, 0.74f); // Pink Tree Canopy
            if (s.Contains("magenta") || s.Contains("rib") || s.Contains("swirl") || s.Contains("neon"))
                return new Color(0.92f, 0.26f, 0.98f); // Neon Magenta
            if (s.Contains("dark") || s.Contains("barrel") || s.Contains("recess") || s.Contains("disc"))
                return new Color(0.10f, 0.11f, 0.22f); // Deep Dark Interior

            // 3. Check if FBX imported material has a saturated non-grey color
            if (srcMat != null)
            {
                Color c = srcMat.HasProperty("_BaseColor")
                    ? srcMat.GetColor("_BaseColor")
                    : (srcMat.HasProperty("_Color") ? srcMat.color : Color.gray);
                float maxC = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float minC = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (maxC > 0.12f && (maxC - minC) > 0.08f)
                {
                    return c;
                }
            }

            return new Color(0.48f, 0.28f, 0.88f); // Rich hyper-casual purple default
        }

        private static Texture2D GetOrCreateHazardTexture()
        {
            if (_hazardStripeTexture != null)
                return _hazardStripeTexture;

            const int size = 128;
            _hazardStripeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralHazardStripes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32 yellow = new Color32(255, 204, 24, 255);
            Color32 dark = new Color32(24, 22, 34, 255);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int stripe = ((x + y) / 16) % 2;
                    pixels[y * size + x] = stripe == 0 ? yellow : dark;
                }
            }

            _hazardStripeTexture.SetPixels32(pixels);
            _hazardStripeTexture.Apply();
            return _hazardStripeTexture;
        }

        private static void ConfigureTransparentURPMaterial(Material mat, Color color, Texture2D mainTex)
        {
            if (mat == null) return;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mainTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
            }
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static Texture2D GetOrCreateSoftRadialDiscTexture()
        {
            if (_softRadialDiscTexture != null)
                return _softRadialDiscTexture;

            const int size = 64;
            _softRadialDiscTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SoftRadialDiscTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.68f) / 0.16f, 2f)) * 0.65f;
                    float core = Mathf.Clamp01(1f - r * 1.15f) * 0.35f;
                    float a = Mathf.Clamp01(ring + core) * Mathf.Clamp01((1f - r) * 4f);
                    byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            _softRadialDiscTexture.SetPixels32(pixels);
            _softRadialDiscTexture.Apply();
            return _softRadialDiscTexture;
        }

        internal static Texture2D GetOrCreateJellyGlossGradientTexture()
        {
            if (_jellyGlossGradientTexture != null)
                return _jellyGlossGradientTexture;

            const int size = 128;
            _jellyGlossGradientTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralJellyGlossGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1); // 0 = bottom skirt, 1 = dome crown
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    // Luminous inner core in middle-lower dome + bright rim at bottom + top-left wet studio gloss crescent
                    float dxSpec = (u - 0.34f) * 1.35f;
                    float dySpec = (v - 0.74f) * 1.85f;
                    float specCrescent = Mathf.Exp(-(dxSpec * dxSpec + dySpec * dySpec) * 14f) * 0.55f;

                    float dxDot = (u - 0.64f) * 1.8f;
                    float dyDot = (v - 0.58f) * 1.8f;
                    float subHighlight = Mathf.Exp(-(dxDot * dxDot + dyDot * dyDot) * 22f) * 0.28f;

                    float verticalGrad = Mathf.Lerp(0.88f, 1.08f, Mathf.SmoothStep(0f, 1f, v));
                    float bottomNeonRim = Mathf.Exp(-v * 9.5f) * 0.35f;

                    float brightness = Mathf.Clamp(verticalGrad + specCrescent + subHighlight + bottomNeonRim, 0.75f, 1.45f);
                    byte cByte = (byte)Mathf.Clamp(Mathf.RoundToInt(brightness * 230f), 0, 255);
                    pixels[y * size + x] = new Color32(cByte, cByte, 255, 242);
                }
            }

            _jellyGlossGradientTexture.SetPixels32(pixels);
            _jellyGlossGradientTexture.Apply();
            return _jellyGlossGradientTexture;
        }

        internal static Material GetOrCreateGlossyJellyMaterial(Color tint)
        {
            Color32 c32 = tint;
            string key = $"GlossyJellyMat_{c32.r}_{c32.g}_{c32.b}";
            if (MaterialCache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = key };
            Texture2D glossTex = GetOrCreateJellyGlossGradientTexture();

            Color brightTint = new Color(
                Mathf.Clamp01(tint.r * 1.05f + 0.05f),
                Mathf.Clamp01(tint.g * 1.05f + 0.08f),
                Mathf.Clamp01(tint.b * 1.05f + 0.08f),
                0.96f);

            mat.color = brightTint;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", brightTint);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", glossTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", glossTex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.96f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(tint.r * 0.38f, tint.g * 0.46f, tint.b * 0.52f, 1f));
            }

            MaterialCache[key] = mat;
            return mat;
        }
    }
}
