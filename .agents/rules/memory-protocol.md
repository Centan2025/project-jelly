---
description: L1/L2/L3 Kalıcı Proje Hafızası ve Hedefli Kod Erişim Kuralları
---

# Proje Hafıza ve Token Optimizasyon Kuralı

1. **Oturum Başlangıcı (Wake-up):**
   - Her yeni oturuma `AGENTS.md` (L1) ve `docs/ai-context/current-state.md` (L2) dosyalarını okuyarak ve `git diff <Last_Verified_Commit>..HEAD --stat` çalıştırarak başla.
   - L3 dosyalarını (`architecture.md`, `decisions.md`, `integrations.md`, `setup.md`, `history/*`) yalnızca görev konusu gerektirdiğinde oku.
2. **Hedefli Kod Erişimi:**
   - C# kaynak dosyalarını (özellikle `Assets/Jelly/Runtime/EndlessTrackManager.cs` gibi büyük dosyaları) `Read` ile baştan sona okuma.
   - Önce `docs/ai-context/module-map.md` veya sembol/grep araması (`find_symbol`, `get_symbols_overview`, `Select-String`) ile hedef sınıf/metot satırını bul ve yalnızca ilgili satır aralığını oku.
3. **Görev Sonu (Check-in):**
   - Görev sonunda `.agents/skills/memory-manager/SKILL.md` protokolüne uyarak `docs/ai-context/current-state.md` (≤50 satır) ve `docs/ai-context/decisions.md` dosyalarında yalnızca değişen kısımları (delta) `[VERIFIED]` / `[REPORTED]` / `[UNVERIFIED]` etiketleriyle güncelle.
   - Hafıza dosyalarında mutlak yol (`C:\...`) veya secret/token kullanma; daima göreli yol (`Assets/...`) kullan.
