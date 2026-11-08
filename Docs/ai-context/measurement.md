# Project Jelly — Performans ve Hafıza Kabul Ölçüm Raporu (L3 Storage)

> **Kontrol Tarihi:** `2026-11-08` | **Referans Commit:** `96fe4e1`

## 1. Kabul ve Doğrulama Durumu
- **L1 (`AGENTS.md`) Satır Sayısı:** `48` satır (Hedef: <= 60) `[VERIFIED]`
- **L2 (`current-state.md`) Satır Sayısı:** `34` satır (Hedef: <= 50) `[VERIFIED]`
- **Serena MCP Durumu:** `VERIFIED (Aktif, Project: JellyShift, Memory: s.mem aktif, Arama: s.fs aktif; LSP: csharp-ls PATH eksikliği nedeniyle sınırlı / fallback devrede)`
- **Basic Memory MCP Durumu:** `VERIFIED (Aktif, Project: main, Salt-Okunur İzolasyon Doğrulandı)`

## 2. Keşif ve Bağlam Ölçüm Tablosu

| Senaryo / Metrik | Süre | Araç Çağrısı | Okunan Kaynak Dosya | Okunan Kod Satırı | Token | Sonuç / Doğruluk |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Test A (Modül/UI Keşfi)** | `0.12s` | `1` | `1` (`Phase3MobileHUDSystem.cs`) | `12` satır (L20-32) | `UNVERIFIED` | `PASS` (`MobileHUDViewModel` hedefli tespit edildi) `[VERIFIED]` |
| **Test B (Sembol & Referans Seviye A)** | `0.15s` | `1` | `1` (`JellyGateSystem.cs`) | `11` satır (L27-37) | `UNVERIFIED` | `PASS` (`EvaluateCircular` 27. satırda bulundu) `[VERIFIED]` |
| **Test C (Evidence Invalidation)** | `0.05s` | `1` | `0` (Yalnızca Git diff & Evidence Index) | `0` kod satırı | `UNVERIFIED` | `PASS` (`DEC-001..005` kaynakları değişmedi, `[VERIFIED]` korundu) `[VERIFIED]` |
| **Test D (Handoff Devri)** | `0.02s` | `1` | `0` (Handoff şema & gitignore kontrolü) | `0` kod satırı | `UNVERIFIED` | `PASS` (`SampleScene.unity` çalışma ağacı korundu) `[VERIFIED]` |
