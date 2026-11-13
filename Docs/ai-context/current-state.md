# Aktif Proje Durumu (L2 Cache — Oturum Başında Okunur)

- **Last Verified Commit:** `e7eba30` `[VERIFIED]`
- **Son Doğrulama Tarihi:** `2026-11-13` `[VERIFIED]`
- **Aktif Dal & Çalışma Ağacı:** `main` (HEAD `e7eba30`), Toy-Box Waterpark V6 (3/4 kamera + ön-yan yüz + zemine sıfır dar geçit) hazır (`WaterparkArtV6Marker`) `[VERIFIED]`
- **Mimari Özet:** Unity 6 (`6000.5.6f1`) URP 3B hiper-gündelik jöle koşu oyunu. İki çalışma modu bulunur:
  1. **Endless / Showcase Vitrin Modu:** `Assets/Scenes/SampleScene.unity` üzerinde `EndlessTrackManager` + `BlenderKitModelSpawner` + `JellyRealtimeDeformer` ile 15 seviyeli/5 biyomlu akış `[VERIFIED]`.
  2. **Modüler Seviye Sistemi (Levels 01–50):** `JellyLevelSystem`, `LevelLoader`, `JellyController` ve `GateClearanceEvaluator` tabanlı 50 ScriptableObject seviyesi ve 12 sahne `[VERIFIED]`.

## Son Tamamlanan İşler (Delta)
- `[VERIFIED]` (`2026-11-13`): Alternatif 1 (Toy-Box Waterpark) V5 görsel ve UI yenilemesi tamamlandı (`EndlessTrackManager.cs`, `BlenderKitModelSpawner.cs`, `JellyModernUI.cs`, `GlassPickupOrb.shader`, `JellySubsurface.shader`, `ProjectJelly_Kit.fbx`).
- `[VERIFIED]` (`e7eba30`): AI Context V2.1 mimarisi (`context-capsule.md`, `evidence-index.md`, `handoff.md`, `measurement.md`) GitHub'a aktarıldı.
- `[VERIFIED]` (`2026-11-08`): Global Basic Memory izolasyon standardı entegre edildi (`Docs/ai-context/*.md` SSOT, `project="JellyShift"`, `main` alanına yazmama kuralı, salt-okunur Test E).

## Doğrulama & Test Durumu
- **Hafıza Doğrulama Testi (`verify-memory.mjs`):** `PASS` (Yapı, satır sınırları, göreli yol ve gizlilik kontrolleri) `[VERIFIED]`
- **Serena MCP Entegrasyonu:** `VERIFIED` (`activate_project`, `s.mem` ve `s.fs` çalışıyor; `csharp-ls` LSP sınırlı/fallback devrede)
- **Basic Memory İzolasyonu (Test E):** `VERIFIED` (Salt-okunur listeleme yapıldı; `main` alanına yazmama kuralı uygulandı; 0 veri yazıldı)
- **Unity EditMode Testleri:** `7/7 PASS` (`Logs/EditModeResults.xml` raporuna göre) `[REPORTED]`
- **Unity PlayMode Testleri:** `7/7 PASS` (`Logs/PlayModeResults.xml` raporuna göre) `[REPORTED]`

## Açık Kalan Görevler / Bilinen Sınırlamalar
- `[UNVERIFIED]` Fiziksel Android cihazda `Builds/Android/ProjectJelly_V1.apk` kurulumu ve `adb logcat` performans doğrulaması (`Docs/Known_Issues.md`).
- `[VERIFIED]` `Assets/Scenes/SampleScene.unity` dosyasında commitlenmemiş yerel sahne değişikliği mevcut; kullanıcı onayı olmadan sıfırlanmamalı/dokunulmamalıdır.
