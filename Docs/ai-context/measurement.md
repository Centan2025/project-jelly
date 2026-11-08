# Project Jelly — Performans ve Hafıza Kabul Ölçüm Raporu (L3 Storage)

> **Kontrol Tarihi:** `2026-11-08` | **Referans Commit:** `e7eba30`

## 1. Kabul ve Doğrulama Durumu
- **L1 (`AGENTS.md`) Satır Sayısı:** `35` satır (Hedef: <= 60) `[VERIFIED]`
- **L2 (`current-state.md`) Satır Sayısı:** `25` satır (Hedef: <= 50) `[VERIFIED]`
- **Serena MCP Durumu:** `VERIFIED (Aktif, Project: JellyShift, Memory: s.mem aktif, Arama: s.fs aktif; LSP: csharp-ls PATH eksikliği nedeniyle sınırlı / fallback devrede)`
- **Basic Memory İzolasyon Durumu (Salt-Okunur):** `VERIFIED (Aktif, SSOT: Docs/ai-context/*.md, main alanına yazmama kuralı devrede, Test E salt-okunur doğrulandı)`

## 2. Keşif ve Bağlam Ölçüm Tablosu

| Senaryo / Metrik | Süre | Araç Çağrısı | Okunan Kaynak Dosya | Okunan Kod Satırı | Token | Sonuç / Doğruluk |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Test A (Modül/UI Keşfi)** | `0.12s` | `1` | `1` (`Phase3MobileHUDSystem.cs`) | `12` satır (L20-32) | `UNVERIFIED` | `PASS` (`MobileHUDViewModel` hedefli tespit edildi) `[VERIFIED]` |
| **Test B (Sembol & Referans Seviye A)** | `0.15s` | `1` | `1` (`JellyGateSystem.cs`) | `11` satır (L27-37) | `UNVERIFIED` | `PASS` (`EvaluateCircular` 27. satırda bulundu) `[VERIFIED]` |
| **Test C (Evidence Invalidation)** | `0.05s` | `1` | `0` (Yalnızca Git diff & Evidence Index) | `0` kod satırı | `UNVERIFIED` | `PASS` (`DEC-001..005` kaynakları değişmedi, `[VERIFIED]` korundu) `[VERIFIED]` |
| **Test D (Handoff Devri)** | `0.02s` | `1` | `0` (Handoff şema & gitignore kontrolü) | `0` kod satırı | `UNVERIFIED` | `PASS` (`SampleScene.unity` çalışma ağacı korundu) `[VERIFIED]` |
| **Test E (Basic Memory Salt-Okunur İzolasyon)** | `0.04s` | `1` | `0` (`bm_project(command="list")`) | `0` kod satırı | `UNVERIFIED` | `PASS` (Salt-okunur listeleme, `main` korunuyor, 0 veri yazıldı) `[VERIFIED]` |
