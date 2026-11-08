# Project Jelly — Context Capsule & Görev Yönlendirme Rehberi (L2/L3)

Context Capsule, bir görevi tamamlamak için gereken **minimum ve hedefli bağlamı** taşıyan geçici çalışma özetidir.

## 1. Görev Sınıflandırma Tablosu

| Görev Tipi | Örnek | Öncelikli Kaynak |
| :--- | :--- | :--- |
| **UI / UX** | HUD, mağaza, bioma geçiş, menü | `module-map.md` + `JellyUIManager.cs`, `Phase3MobileHUDSystem.cs` |
| **Bug Fix** | Kütle, geçit toleransı, zıplama | İlgili C# dosyası (Serena Seviye A/B) + `JellyEditModeTests.cs` |
| **API / Entegrasyon** | SaveGame JSON, Vercel vitrin | `integrations.md` + `JellyLevelSystem.cs` |
| **Fizik / Mekanik** | Yuvarlanma, sürtünme, rampa | `architecture.md` + `JellyMotor.cs`, `JellyPhysicsBody.cs` |
| **Harita / Seviye** | 8 modüler segment, 15 seviye | `module-map.md` + `JellyProgressiveMapGenerator.cs` |
| **Performans / Havuz** | GC düşüşü, parça havuzlama | `decisions.md` (`#pooling`) + `JellyFragmentPool.cs` |
| **Refactor** | Sınıf ayrıştırma, bağımlılık | `architecture.md` + `s.lsp.find_referencing_symbols` |
| **Dokümantasyon** | Rehber, L3 hafıza güncelleme | İlgili Markdown belgeleri + `evidence-index.md` |
| **Test** | EditMode veya PlayMode doğrulaması | `Assets/Jelly/Tests/` + `measurement.md` |

## 2. Context Capsule Şeması

```yaml
task:
  id: "local-task-id"
  type: "bugfix | ui | physics | level | performance | refactor | docs | test"
  summary: "Kısa ve net görev tanımı"

context:
  project: "Project Jelly"
  branch: "main"
  head: "96fe4e1"
  relevant_modules: []
  relevant_files: []
  relevant_symbols: []
  decisions: []
  integration_contracts: []
  required_tests: []
  risks: []

verification:
  source_commit: "96fe4e1"
  stale_records: []
  unverified_assumptions: []

budget:
  mode: "targeted"
  expand_only_when_required: true
```

## 3. Bağlam Seçme Sırası
1. Görevi sınıflandır -> 2. `current-state.md` ve aktif handoff kontrol et -> 3. `module-map.md` içinde hedef sınıfı bul -> 4. Gerekliyse ilgili L3 karar/sözleşme bölümünü aç -> 5. Serena Seviye A (`include_body=False`) ile sembolleri bul -> 6. Kısa Context Capsule oluştur -> 7. Görevi uygula ve doğrula.
