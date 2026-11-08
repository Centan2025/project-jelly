# Aktif Proje Durumu (L2 Cache — Oturum Başında Okunur)

- **Last Verified Commit:** `32670e5` `[VERIFIED]`
- **Son Doğrulama Tarihi:** `2026-11-08` `[VERIFIED]`
- **Aktif Dal & Çalışma Ağacı:** `main` (veya aktif HEAD `32670e5`), yerel değişiklik: `M Assets/Scenes/SampleScene.unity` `[VERIFIED]`
- **Mimari Özet:** Unity 6 (`6000.5.6f1`) URP 3B hiper-gündelik jöle koşu oyunu. İki çalışma modu bulunur:
  1. **Endless / Showcase Vitrin Modu:** `Assets/Scenes/SampleScene.unity` üzerinde `EndlessTrackManager` + `BlenderKitModelSpawner` + `JellyRealtimeDeformer` ile 15 seviyeli/5 biyomlu akış `[VERIFIED]`.
  2. **Modüler Seviye Sistemi (Levels 01–50):** `JellyLevelSystem`, `LevelLoader`, `JellyController` ve `GateClearanceEvaluator` tabanlı 50 ScriptableObject seviyesi ve 12 sahne `[VERIFIED]`.

## Son Tamamlanan İşler (Delta)
- `[VERIFIED]` (`32670e5`): 3B Blender kit model üreticisi (`BlenderKitModelSpawner.cs`), gerçek zamanlı jöle deformasyonu (`JellyRealtimeDeformer.cs`), yarı saydam `+/-` küreler ve 15 seviyeli müfredat (`JellyProgressiveMapGenerator.cs`) tamamlandı.
- `[VERIFIED]` (`50261f4`, `952443e`): 8 modüler pist segment sistemi, karşılaştırma görselleri (`images/`) ve Vercel statik vitrini (`index.html`, `vercel.json`) eklendi.
- `[VERIFIED]` (`2026-11-08`): L1/L2/L3 kalıcı proje hafızası (`AGENTS.md`, `docs/ai-context/*`, `.agents/skills/memory-manager/SKILL.md`) ve `.gitignore` Serena taşınabilirlik kuralları kuruldu.

## Doğrulama & Test Durumu
- **Unity EditMode Testleri:** `7/7 PASS` (`Logs/EditModeResults.xml` raporuna göre) `[REPORTED]`
- **Unity PlayMode Testleri:** `7/7 PASS` (`Logs/PlayModeResults.xml` raporuna göre) `[REPORTED]`
- **Fiziksel Android Cihaz & Logcat:** Bağlı cihaz olmadığı için (`adb devices` boş) cihaz üstü GPU/dokunmatik testi yapılmadı `[UNVERIFIED]`.

## Açık Kalan Görevler / Bilinen Sınırlamalar
- `[UNVERIFIED]` Fiziksel Android cihazda `Builds/Android/ProjectJelly_V1.apk` kurulumu ve `adb logcat` performans doğrulaması (`Docs/Known_Issues.md`).
- `[VERIFIED]` `Assets/Scenes/SampleScene.unity` dosyasında commitlenmemiş yerel sahne değişikliği mevcut; kullanıcı onayı olmadan sıfırlanmamalı.
- `[REPORTED]` Ses ve titreşim (haptics) event kancaları (`SaveGameData.soundEnabled`, `OnMassChanged` vb.) hazır; stüdyo ses dosyaları eklendiğinde bağlanacak.
