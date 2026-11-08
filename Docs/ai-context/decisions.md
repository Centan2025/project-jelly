# Mimari ve Teknik Kararlar Kaydı (L3 Storage)

> **Kullanım:** Tüm dosyayı okumak yerine `Select-String -Path docs/ai-context/decisions.md -Pattern "#etiket"` ile arama yapın. Yeni kararları dosya sonuna delta olarak ekleyin `[VERIFIED]`.

---

### ADR-001: Küp Kök Kütle–Yarıçap Ölçeklemesi ve Kütle Korunumu
- **Etiketler:** `#physics` `#mass` `#morphology` `#split-merge`
- **Durum:** `[VERIFIED]` | **Commit:** `85fdf15` / `32670e5`
- **Dosyalar:** `Assets/Jelly/Runtime/JellyMassAndMorphSystem.cs`, `Assets/Jelly/Runtime/JellySplitAndMergeSystem.cs`
- **Karar:** Jöle yarıçapı kütleyle doğrusal değil, 3B küresel hacimle orantılı olarak $r = r_0 \cdot (m / m_0)^{1/3}$ formülüyle hesaplanır ve kütle `[20, 200]` aralığında sınırlandırılır. Bölünme (`100 -> 50 + 50`) ve birleşme (`50 + 50 -> 100`) işlemlerinde `JellyMassConservation` toplam kütleyi birebir korur.
- **Gerekçe:** 3B fizikte görsel hacim algısını korumak ve `JellyEditModeTests` içindeki deterministik sınır testlerini (`1002`, `1003`, `1004`) garanti altına almak.

### ADR-002: FixedUpdate Fizik Bütünlüğü ve Nesne Havuzlama (Object Pooling)
- **Etiketler:** `#physics` `#performance` `#mobile` `#pooling`
- **Durum:** `[VERIFIED]` | **Commit:** `50261f4` / `32670e5`
- **Dosyalar:** `Assets/Jelly/Runtime/JellyMotor.cs`, `Assets/Jelly/Runtime/JellyPhysicsBody.cs`, `Assets/Jelly/Runtime/JellyFragmentPool.cs`, `Assets/Jelly/Runtime/JellyFXPool.cs`
- **Karar:** Tüm `Rigidbody` kuvvetleri `FixedUpdate` içinde `ContinuousDynamic` çarpışma ve `Interpolate` moduyla uygulanır. Bölünmüş jöle parçaları ve parçacık efektleri çalışma zamanında `Instantiate`/`Destroy` yerine havuzdan (`JellyFragmentPool`, `JellyFXPool`) yeniden kullanılır.
- **Gerekçe:** Yüksek hızda engellerin içinden geçmeyi (tunneling) ve mobil cihazlarda GC kaynaklı kare düşüşlerini (frame spike) önlemek.

### ADR-003: 8 Modüler Pist Segmenti ve 15 Seviyeli İlerlemeli Harita Üretimi
- **Etiketler:** `#map-architecture` `#level-design` `#endless`
- **Durum:** `[VERIFIED]` | **Commit:** `85fdf15` / `32670e5`
- **Dosyalar:** `Assets/Jelly/Runtime/JellyProgressiveMapGenerator.cs`, `Assets/Jelly/Runtime/JellyTrackSegmentNode.cs`, `Assets/Jelly/Runtime/EndlessTrackManager.cs`
- **Karar:** Pist mimarisi 8 modüler segmente (`StandardRoad`, `SplitTwinBridge`, `JumpRampOverAbyss`, `DoubleDeckerOverpass`, `LaserSlicerGate`, `SurfaceHazardZone`, `ColorMatchSection`, `LevelFinaleMultiplier`) ayrıldı ve 5 biyom temasıyla birleştirildi.
- **Gerekçe:** Tekrarlayan düz yollar yerine her seviyede yeni bir mekanik kombinasyonu sunmak ve hem vitrin (`SampleScene.unity`) hem de seviye üreticisinde aynı modülleri kullanmak.

### ADR-004: Prosedürel 3B Blender Kit Üreticisi ve Gerçek Zamanlı Deformasyon
- **Etiketler:** `#3d-models` `#blender` `#rendering` `#deformation`
- **Durum:** `[VERIFIED]` | **Commit:** `32670e5`
- **Dosyalar:** `Assets/Jelly/Runtime/BlenderKitModelSpawner.cs`, `Assets/Jelly/Runtime/JellyRealtimeDeformer.cs`, `Blender/build_project_jelly_kit.py`, `.gitignore`
- **Karar:** İkili (binary) `.blend` ve `.glb` dosyaları Git deposunu şişirmemek için `.gitignore` ile dışlandı; modeller `Blender/build_project_jelly_kit.py` ve çalışma zamanında `BlenderKitModelSpawner.cs` ile deterministik olarak üretilir. Jöle esnemesi `JellyRealtimeDeformer` ve `JellyDeformationController` ile analitik olarak hesaplanır.
- **Gerekçe:** Git klonlama hızını korumak, ikili dosya çakışmalarını önlemek ve her bilgisayarda koddan birebir aynı 3B sahneyi üretebilmek.

### ADR-005: L1/L2/L3 Kalıcı Proje Hafızası ve Taşınabilir Serena Standartları
- **Etiketler:** `#ai-context` `#token-optimization` `#serena` `#git`
- **Durum:** `[VERIFIED]` | **Commit:** `32670e5` (üzerine eklendi)
- **Dosyalar:** `AGENTS.md`, `docs/ai-context/*`, `.agents/skills/memory-manager/SKILL.md`, `.gitignore`
- **Karar:** Proje hafızası L1 (`AGENTS.md`, ≤60 satır), L2 (`current-state.md` ≤50 satır, `module-map.md`) ve L3 (`architecture.md`, `decisions.md`, `integrations.md`, `setup.md`, `history/`) olarak 3 katmana ayrıldı. `.serena/project.yml` ve `.serena/memories/` Git'e dahil edilirken `.serena/*` önbelleği dışlandı.
- **Gerekçe:** Ajanların her oturumda 60+ C# dosyasını (özellikle 210 KB'lık `EndlessTrackManager.cs` dosyasını) baştan sona okuyarak token harcamasını önlemek ve bağlamı GitHub üzerinden taşınabilir kılmak.
