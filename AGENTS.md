# AGENTS.md — Project Jelly (L1 Cache — AI Context V2.1)

## Proje Özeti
**Project Jelly**, Unity 6 (`6000.5.6f1`, URP `17.0.1`) ile geliştirilmiş 3B hiper-gündelik morfoloji (büyüme/küçülme), ikiz bölünme (`÷2`), birleşme (`><`) ve 8 modüler pist segmentine sahip koşu oyunudur (`Jelly.Runtime`, `Jelly.Editor`, `Jelly.Tests.*`) `[VERIFIED]`. Statik web vitrini (`index.html`, `vercel.json`) kök dizindedir `[VERIFIED]`.

## Temel Komutlar (Unity CLI & Git)
- **Durum & Son Fark:** `git status --short` | `git diff 96fe4e1..HEAD --name-status`
- **Varlık/Sahne Üretimi:** `Jelly -> Generate All Project Assets & Scenes` (`JellyProjectBuilder.GenerateAll`)
- **EditMode Testleri:** `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditModeResults.xml -quit`
- **PlayMode Testleri:** `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayModeResults.xml -quit`

## L2 / L3 Bağlam Yönlendirme Tablosu (Hepsini Otomatik Yükleme!)
| Durum / İhtiyaç | Okunacak Dosya (Göreli Yol) | Katman |
|---|---|---|
| **Oturum Başı (Zorunlu Wake-up)** | `Docs/ai-context/current-state.md` + aktif handoff | **L2** |
| Görev Kapsülleme & Önceliklendirme | `Docs/ai-context/context-capsule.md` | **L2** |
| Dosya / Modül / Sınıf Konumu | `Docs/ai-context/module-map.md` | **L2** |
| Hafıza Kanıt & Diff Geçerlilik İndeksi | `Docs/ai-context/evidence-index.md` | **L3** |
| Genel Mimari ve Veri Akışı | `Docs/ai-context/architecture.md` | **L3** |
| Mimari Kararlar (`#etiket` ile grep) | `Docs/ai-context/decisions.md` | **L3** |
| Entegrasyonlar, Kayıt & URP/Vercel Sözleşmeleri | `Docs/ai-context/integrations.md` | **L3** |
| Görev Devri & Kesinti Handoff Kaydı | `Docs/ai-context/handoff.md` (Yerel: `.handoff.local.md`) | **L3** |
| Yeni Bilgisayar, Serena & Basic Memory Kurulumu | `Docs/ai-context/setup.md` | **L3** |
| Ölçüm Raporu & Arşiv | `Docs/ai-context/measurement.md` / `Docs/ai-context/history/` | **L3** |

## V2.1 AI Kod Araştırma ve Hafıza Disiplini
1. **Wake-up:** `current-state.md` ve aktif handoff oku -> Serena `activate_project` çağır -> `git diff 96fe4e1..HEAD --name-status` ile `evidence-index.md` kontrol et -> İlgili modül için Context Capsule oluştur.
2. **Serena İki Aşamalı Protokol:**
   - **Seviye A (Kısa — Varsayılan):** Önce sembol adı, tür ve satır aralığını al (`include_body=False`); fonksiyon gövdelerini varsayılan olarak yükleme.
   - **Seviye B (Derin):** Yalnızca mantık değişikliği, güvenlik analizi veya veri akışı gerektiğinde gövdeyi aç. Dil sunucusu yoksa `module-map.md` + hedefli `grep` kullan.
3. **Check-in & Handoff:** Görev bitiminde `current-state.md` (<=50 satır) delta güncelle; kararları `decisions.md` ve `evidence-index.md` içine mühürle; yarım kalan işleri handoff kaydına yaz.
4. **Güvenlik Sınırı:** Secret, token, `.env` asla hafızaya yazılmaz. Göreli dosya yolu kullanılır. Onaysız `git commit`, `git push` veya deploy yapılamaz.
