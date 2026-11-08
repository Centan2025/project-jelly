# Modül Haritası (L2 Cache — Hedefli Kod Arama Rehberi)

> **Kural:** Kaynak dosyaları `Read` ile baştan sona okumadan önce aradığınız özelliğin hangi dosya ve sınıfta olduğunu buradan tespit edin, ardından sembol/grep aramasıyla yalnızca ilgili satırları okuyun `[VERIFIED]`.

## 1. Assembly Yapısı (`Assets/Jelly/`)
| Assembly | Tanım Dosyası | Sorumluluk |
|---|---|---|
| `Jelly.Runtime` | `Assets/Jelly/Runtime/Jelly.Runtime.asmdef` | Tüm oyun içi mekanikler, fizik, seviye, UI ve prosedürel pist üretimi (`Unity.InputSystem` bağımlı) `[VERIFIED]` |
| `Jelly.Editor` | `Assets/Jelly/Editor/Jelly.Editor.asmdef` | Sahne, prefab, materyal ve `ScriptableObject` kataloglarını üreten Editor araçları `[VERIFIED]` |
| `Jelly.Tests.EditMode` | `Assets/Jelly/Tests/EditMode/Jelly.Tests.EditMode.asmdef` | Matematik, kütle korunumu, geçit ve seviye doğrulama birim testleri `[VERIFIED]` |
| `Jelly.Tests.PlayMode` | `Assets/Jelly/Tests/PlayMode/Jelly.Tests.PlayMode.asmdef` | Fizik, hareket, bölünme/birleşme ve seviye geçiş entegrasyon testleri `[VERIFIED]` |

---

## 2. Çekirdek Modüller ve Göreli Dosya Yolları (`Assets/Jelly/Runtime/`)

### A. Sonsuz Koşu, Vitrin & Prosedürel Pist (`Endless` & `Progressive Map`)
- `Assets/Jelly/Runtime/EndlessTrackManager.cs` (~210 KB — **DİKKAT: Asla tam okuma, `grep` ile satır bul!**)
  - Giriş Noktası: `EndlessTrackManager : MonoBehaviour`, `JellyScreenMode`, `BiomePalette`
  - Sorumluluk: `Assets/Scenes/SampleScene.unity` içindeki ana oyun döngüsü, 5 biyom paleti, 6 tam ekran UI (Ana Menü, HUD, Görevler, Mağaza, Ayarlar, Seviye Sonu), spline yol ve kamera/oyuncu yönetimi `[VERIFIED]`.
- `Assets/Jelly/Runtime/EndlessGateObstacle.cs`
  - Semboller: `EndlessObstacleType`, `EndlessGateObstacle`
  - Sorumluluk: Piston kapı, kırılabilir cam duvar, döner engel, lazer dilimleyici ve renk bariyeri davranışları `[VERIFIED]`.
- `Assets/Jelly/Runtime/EndlessPickupItem.cs`
  - Semboller: `EndlessPickupType`, `EndlessPickupItem`
  - Sorumluluk: 9 toplanabilir nesne (`+`, `-`, `÷2`, `><`, hız, kalkan, mıknatıs, yıldız, yavaş zaman) `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyProgressiveMapGenerator.cs` & `Assets/Jelly/Runtime/JellyTrackSegmentNode.cs`
  - Semboller: `JellyProgressiveMapGenerator`, `JellyLevelBlueprint`, `TrackChunkSpec`, `TrackSegmentModuleType`, `JellyTrackSegmentNode`
  - Sorumluluk: 8 modüler pist segmentinin (`StandardRoad`, `SplitTwinBridge`, `JumpRampOverAbyss`, `DoubleDeckerOverpass`, `LaserSlicerGate`, `SurfaceHazardZone`, `ColorMatchSection`, `LevelFinaleMultiplier`) prosedürel dizilimi `[VERIFIED]`.
- `Assets/Jelly/Runtime/BlenderKitModelSpawner.cs` (~43 KB)
  - Semboller: `BlenderKitModelSpawner`
  - Sorumluluk: Blender Python kitiyle uyumlu 3B engel, geçit, küre ve çevre modellerinin çalışma zamanında/editörde oluşturulması `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyRealtimeDeformer.cs`
  - Semboller: `JellyRealtimeDeformer`
  - Sorumluluk: Gerçek zamanlı jöle mesh/ölçek esneme ve darbe deformasyonu `[VERIFIED]`.

### B. Hareket, Fizik ve Girdi (`Movement & Input`)
- `Assets/Jelly/Runtime/JellyController.cs`: Ana oyuncu bileşeni; girdi, motor ve kütle sistemlerini koordine eder `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMotor.cs`: `FixedUpdate` içinde yuvarlanma kuvveti, frenleme ve eğim fiziğini uygular `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyInputAdapter.cs`: `JellyInputFrame`, `ControlMode` (`DragAnalog`, `SmoothedTarget`, `VirtualJoystick`) ve klavye/dokunmatik girdi adaptörü `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyPhysicsBody.cs` & `Assets/Jelly/Runtime/JellyGroundDetector.cs`: `Rigidbody` sarmalayıcısı (`ContinuousDynamic`, `Interpolate`) ve zemin temas kontrolü `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMovementConfig.cs`: Hız, ivme ve kontrol parametrelerini tutan `ScriptableObject` (`Assets/Jelly/Configs/DefaultMovementConfig.asset`) `[VERIFIED]`.

### C. Kütle, Hacim ve Morfoloji (`Mass & Morph`)
- `Assets/Jelly/Runtime/JellyMassAndMorphSystem.cs`: `JellyMassModel` ($r = r_0 (m/m_0)^{1/3}$ küp kök yarıçap hesabı, `[20, 200]` sınırları) ve `JellySizeInterpolator` `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMassController.cs`: Aktif kütle değişimlerini yönetir ve olayları (`OnMassChanged`) tetikler `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMorphController.cs` & `Assets/Jelly/Runtime/JellyDeformationController.cs`: Dar geçitlerde yanal/dikey sıkışma (`MorphRatio`) ve hacim koruyucu 3 eksenli yay deformasyonu `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMassPickup.cs` & `Assets/Jelly/Runtime/JellyMassDrain.cs`: Kütle artıran (`+`) ve azaltan (`-`) tetikleyiciler `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyMassConfig.cs`: Kütle sınırları ve referans yarıçap `ScriptableObject` tanımı (`Assets/Jelly/Configs/DefaultMassConfig.asset`) `[VERIFIED]`.

### D. Bölünme ve Birleşme (`Split & Merge`)
- `Assets/Jelly/Runtime/JellySplitAndMergeSystem.cs`: `JellyMassConservation` (bölünme ve birleşmede toplam kütle korunumu matematiği) `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellySplitController.cs` & `Assets/Jelly/Runtime/JellyMergeController.cs`: Jöleyi `50/50` veya özel oranlarla parçalara ayırma ve mesafe/komut ile yeniden birleştirme `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyFragment.cs`, `Assets/Jelly/Runtime/JellyFragmentRegistry.cs`, `Assets/Jelly/Runtime/JellyFragmentPool.cs`, `Assets/Jelly/Runtime/JellyFragmentInputRouter.cs`: Aktif parçaların takibi, GC üretmeyen nesne havuzu ve çoklu parça girdi yönlendirmesi `[VERIFIED]`.

### E. Geçitler ve Etkileşimli Engeller (`Gates & Obstacles`)
- `Assets/Jelly/Runtime/JellyGateSystem.cs`: `GateClearanceEvaluator`, `GateBase`, `GateCandidateState`, `GateEvaluationResult` `[VERIFIED]`.
- Geçit Sınıfları: `CircularGate.cs`, `RectangularGate.cs`, `NarrowSlotGate.cs`, `CompressibleGate.cs`, `MassThresholdGate.cs`, `SplitGate.cs`, `TimedGate.cs`, `ObstacleStateController.cs` `[VERIFIED]`.
- Faz 2 Çok Delikli Engeller: `Assets/Jelly/Runtime/Phase2CoreObstacleSystem.cs` (`MultiHoleVariantType`, `Phase2CoreObstacleEvaluator`) `[VERIFIED]`.
- Fiziksel Etkileşimler: `WeightedPressurePlate.cs`, `DualFragmentSwitchGroup.cs`, `PushableBlock.cs`, `MomentumBreakableBarrier.cs`, `MassSensitiveBridge.cs`, `BalancePlatform.cs`, `MovingPlatform.cs`, `RotatingObstacle.cs`, `SpringLauncher.cs`, `SurfaceFrictionZone.cs` `[VERIFIED]`.

### F. Seviye, Kayıt, Kamera ve Arayüz (`Level, Save, Camera & UI`)
- `Assets/Jelly/Runtime/JellyLevelDefinition.cs` & `Assets/Jelly/Runtime/JellyLevelCatalogFactory.cs`: `Level_01`..`Level_50` veri yapıları ve prosedürel katalog fabrikası `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyLevelSystem.cs`: `LevelValidator`, `LevelGoalEvaluator`, `SaveGameService` (JSON tabanlı kayıt), `LevelProgressionService` `[VERIFIED]`.
- `Assets/Jelly/Runtime/LevelLoader.cs`, `Assets/Jelly/Runtime/LevelManager.cs`, `Assets/Jelly/Runtime/LevelGoalZone.cs`, `Assets/Jelly/Runtime/CheckpointManager.cs`, `Assets/Jelly/Runtime/CheckpointTrigger.cs` `[VERIFIED]`.
- `Assets/Jelly/Runtime/JellyCameraController.cs`, `Assets/Jelly/Runtime/JellyFXPool.cs`, `Assets/Jelly/Runtime/JellySafeAreaFitter.cs`, `Assets/Jelly/Runtime/JellyUIManager.cs`, `Assets/Jelly/Runtime/Phase3MobileHUDSystem.cs` `[VERIFIED]`.

---

## 3. Editor, Test ve Dış Araç Giriş Noktaları
- **Editor Varlık Üreticileri:** `Assets/Jelly/Editor/JellyProjectBuilder.cs`, `Assets/Jelly/Editor/Phase2CoreObstaclePrefabBuilder.cs` `[VERIFIED]`
- **Otomatik Testler:** `Assets/Jelly/Tests/EditMode/JellyEditModeTests.cs`, `Assets/Jelly/Tests/PlayMode/JellyPlayModeTests.cs` `[VERIFIED]`
- **Blender Python Model Kiti:** `Blender/build_project_jelly_kit.py` `[VERIFIED]`
- **Web Vitrin (Vercel):** `index.html`, `vercel.json`, `images/` `[VERIFIED]`
