---
description: L1/L2/L3 Kalıcı Proje Hafızası, Context Capsule ve Serena V2.1 Kuralları
---

# Proje Hafıza ve Token Optimizasyon Kuralı (AI Context V2.1)

1. **Oturum Başlangıcı (Wake-up):**
   - Her yeni oturuma `AGENTS.md` (L1) ve `Docs/ai-context/current-state.md` (L2) dosyalarını okuyarak ve `git diff <Last_Verified_Commit>..HEAD --name-status` çalıştırarak başla.
   - Değişen dosyaları `Docs/ai-context/evidence-index.md` ile eşleştir; ilgisiz L3 dosyalarını otomatik yükleme.
2. **Context Capsule & Hedefli Kod Erişimi:**
   - C# kaynak dosyalarını (özellikle `Assets/Jelly/Runtime/EndlessTrackManager.cs` gibi büyük dosyaları) `Read` ile baştan sona okuma.
   - Önce `Docs/ai-context/module-map.md` ile hedef sınıfı bul; ardından Serena Seviye A (`include_body=False`) veya hedefli `Select-String` / `grep` ile satır bulup yalnız ilgili satır aralığını oku.
3. **Görev Sonu (Check-in & Handoff):**
   - Görev sonunda `.agents/skills/memory-manager/SKILL.md` protokolüne uyarak `Docs/ai-context/current-state.md` (<=50 satır) ve `Docs/ai-context/decisions.md` dosyalarında yalnızca değişen kısımları (delta) güncelle.
   - Yarım kalan görevleri `.handoff.local.md` (veya `Docs/ai-context/handoff.md`) içine kaydet.
   - Hafıza dosyalarında mutlak yol veya secret/token kullanma; daima göreli yol (`Assets/...`) kullan.
