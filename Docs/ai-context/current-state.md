# Aktif Proje Durumu (L2 Cache — Oturum Başında Okunur)

- **Last Verified Commit:** `96fe4e1` `[VERIFIED]`
- **Son Doğrulama Tarihi:** `2026-11-08` `[VERIFIED]`
- **Aktif Dal & Çalışma Ağacı:** `main` (HEAD `96fe4e1`), yerel uncommitted sahne: `M Assets/Scenes/SampleScene.unity` `[VERIFIED]`
- **Mimari Özet:** Unity 6 (`6000.5.6f1`) URP 3B hiper-gündelik jöle koşu oyunu. İki çalışma modu bulunur:
  1. **Endless / Showcase Vitrin Modu:** `Assets/Scenes/SampleScene.unity` üzerinde `EndlessTrackManager` + `BlenderKitModelSpawner` + `JellyRealtimeDeformer` ile 15 seviyeli/5 biyomlu akış `[VERIFIED]`.
  2. **Modüler Seviye Sistemi (Levels 01–50):** `JellyLevelSystem`, `LevelLoader`, `JellyController` ve `GateClearanceEvaluator` tabanlı 50 ScriptableObject seviyesi ve 12 sahne `[VERIFIED]`.

## Son Tamamlanan İşler (Delta)
- `[VERIFIED]` (`96fe4e1`): L1/L2/L3 kalıcı proje hafızası (`AGENTS.md`, `Docs/ai-context/*`, `.agents/skills/memory-manager/SKILL.md`) GitHub'a aktarıldı.
- `[VERIFIED]` (`2026-11-08`): AI Context V2.1 yükseltmesi yapıldı; `context-capsule.md`, `evidence-index.md`, `handoff.md`, `measurement.md` oluşturuldu. Serena ve Basic Memory MCP bağlantıları test edilip mühürlendi.

## Doğrulama & Test Durumu
- **Hafıza Doğrulama Testi (`verify-memory.mjs`):** `PASS` (Yapı, satır sınırları, göreli yol ve gizlilik kontrolleri) `[VERIFIED]`
- **Serena MCP Entegrasyonu:** `VERIFIED` (`activate_project`, `s.mem` ve `s.fs` çalışıyor; `csharp-ls` LSP sınırlı/fallback devrede)
- **Basic Memory MCP Entegrasyonu:** `VERIFIED` (`bm_project`, `bm_list` salt-okunur ve proje izolasyonlu)
- **Unity EditMode Testleri:** `7/7 PASS` (`Logs/EditModeResults.xml` raporuna göre) `[REPORTED]`
- **Unity PlayMode Testleri:** `7/7 PASS` (`Logs/PlayModeResults.xml` raporuna göre) `[REPORTED]`

## Açık Kalan Görevler / Bilinen Sınırlamalar
- `[UNVERIFIED]` Fiziksel Android cihazda `Builds/Android/ProjectJelly_V1.apk` kurulumu ve `adb logcat` performans doğrulaması (`Docs/Known_Issues.md`).
- `[VERIFIED]` `Assets/Scenes/SampleScene.unity` dosyasında commitlenmemiş yerel sahne değişikliği mevcut; kullanıcı onayı olmadan sıfırlanmamalı/dokunulmamalıdır.
