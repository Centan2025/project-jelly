# AGENTS.md — Project Jelly (L1 Cache)

## Proje Özeti
**Project Jelly**, Unity 6 (`6000.5.6f1`, URP `17.0.1`) ile geliştirilmiş 3B hiper-gündelik morfoloji (büyüme/küçülme), ikiz bölünme (`÷2`), birleşme (`><`) ve 8 modüler pist segmentine sahip koşu oyunudur (`Jelly.Runtime`, `Jelly.Editor`, `Jelly.Tests.*`) `[VERIFIED]`. Statik web vitrini (`index.html`, `vercel.json`) kök dizindedir `[VERIFIED]`.

## Temel Komutlar (Unity CLI & Git)
- **Durum & Son Fark:** `git status --short` | `git diff <Last_Verified_Commit>..HEAD --stat`
- **Varlık/Sahne Üretimi (Editor Menu):** `Jelly -> Generate All Project Assets & Scenes` (`JellyProjectBuilder.GenerateAll`)
- **EditMode Testleri:** `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditModeResults.xml -quit`
- **PlayMode Testleri:** `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayModeResults.xml -quit`

## L2 / L3 Bağlam Yönlendirme Tablosu (Hepsini Otomatik Yükleme!)
| Durum / İhtiyaç | Okunacak Dosya (Göreli Yol) | Katman |
|---|---|---|
| **Her Yeni Oturum Başlangıcı (Zorunlu)** | `docs/ai-context/current-state.md` | **L2** |
| Dosya, sınıf veya modül yeri ararken | `docs/ai-context/module-map.md` | **L2** |
| Veri akışı, fizik/kütle kuralları veya sistem mimarisi | `docs/ai-context/architecture.md` | **L3** |
| Geçmiş tasarım kararları ve nedenleri (`#etiket` ile grep) | `docs/ai-context/decisions.md` | **L3** |
| Kayıt sistemi, Blender, Input/URP, Vercel sözleşmeleri | `docs/ai-context/integrations.md` | **L3** |
| Yeni bilgisayar, Serena MCP, `uv` veya ortam kurulumu | `docs/ai-context/setup.md` | **L3** |
| Eski tamamlanmış oturum ve görev kayıtları | `docs/ai-context/history/` | **L3** |

## Zorunlu Kod Araştırma Sırası & Serena / Hafıza Protokolü
1. **Wake-up (Oturum Başı):**
   - İlk olarak `docs/ai-context/current-state.md` dosyasını oku.
   - `Last Verified Commit` ile `HEAD` arasındaki farkı `git diff <commit>..HEAD --stat` ile kontrol et.
   - Serena MCP aktifse `activate_project` çağır; değilse `Select-String`/`grep` kullan.
2. **Hedefli Kod Erişimi (Dosyaları Baştan Sona Okuma!):**
   - Önce `docs/ai-context/module-map.md` veya sembol araması (`get_symbols_overview`, `find_symbol` / `grep`) ile hedef sınıf/metot satırını bul.
   - Yalnızca ilgili satır aralığını (`StartLine`–`EndLine`) oku. Tüm repo'yu veya 210 KB'lık `EndlessTrackManager.cs` gibi büyük dosyaları tek seferde yükleme.
3. **Güvenilirlik Etiketleri:**
   - Bilgileri `[VERIFIED]`, `[REPORTED]`, `[UNVERIFIED]`, `[STALE]` veya `[CONFLICT]` etiketleriyle işaretle.
4. **Check-in (Görev Sonu):**
   - Sadece değişen kısımları (delta) `docs/ai-context/current-state.md` ve `docs/ai-context/decisions.md` içinde güncelle (`skills/memory-manager/SKILL.md` kurallarına uy).
   - Secret, `.env` içeriği veya mutlak dosya yolu (`C:\...`) yazma; daima göreli yol (`Assets/...`) kullan.
