---
name: memory-manager
description: L1/L2/L3 kalıcı proje hafızasını yönetir. Her oturum başında (Wake-up) git diff doğrulaması ve Serena activate_project çağrısı yapar; her görev sonunda (Check-in) yalnızca değişen kısımları (delta) günceller ve eski kayıtları arşivler.
---

# Memory Manager Skill (L1 / L2 / L3 Bağlam & Token Optimizasyonu)

Bu skill, ajanların her oturumda tüm kod tabanını baştan sona taramasını önlemek ve proje hafızasını GitHub üzerinden taşınabilir, şişmeyen bir yapıda tutmak için kullanılır.

---

## 1. Oturum Başlangıcı Protokolü (Wake-up)

Yeni bir oturumda veya görev başında şu adımları **sırasıyla** uygula:

1. **L1 & L2 Okuma:**
   - `AGENTS.md` (L1, ≤60 satır) ve `docs/ai-context/current-state.md` (L2, ≤50 satır) dosyalarını oku.
   - L3 dosyalarını (`architecture.md`, `decisions.md`, `integrations.md`, `setup.md`, `history/*`) görev doğrudan gerektirmedikçe **okuma**.
2. **Serena MCP Aktivasyonu (Varsa):**
   - Ortamda Serena MCP sunucusu tanımlıysa `activate_project` aracını çağır.
   - Sembol aramalarında dosyaları baştan sona `Read` etmek yerine `get_symbols_overview` ve `find_symbol` araçlarını kullan.
   - Serena aktif değilse `docs/ai-context/module-map.md` + `Select-String`/`grep` ile hedef satır numarasını bulup yalnızca ilgili satır aralığını (`StartLine`–`EndLine`) oku.
3. **Git Delta Doğrulaması:**
   - `current-state.md` içindeki `Last Verified Commit` değerini al ve şu komutu çalıştır:
     ```powershell
     git status --short
     git diff <Last_Verified_Commit>..HEAD --stat
     ```
   - Eğer `<Last_Verified_Commit>` ile `HEAD` arasında fark varsa yalnızca değişen dosyaları incele ve `current-state.md` içindeki eski bilgileri güncelle veya `[STALE]` olarak işaretle.

---

## 2. Görev Sırasında Kod Erişim Disiplini

- **Kesin Kural:** `Assets/Jelly/Runtime/EndlessTrackManager.cs` (~210 KB) gibi büyük dosyaları asla tek seferde `Read` aracına verme.
- Önce `docs/ai-context/module-map.md` tablosundan ilgili sınıfı bul, ardından `find_symbol` veya `Select-String -Path <dosya> -Pattern "<sembol>"` ile satır numarasını tespit et.
- Sohbet ekranına (chat) uzun kod blokları veya mimari açıklamalar yazma; çıktıları doğrudan hedef `.md` veya kaynak dosyalarına yaz.

---

## 3. Görev Sonu Protokolü (Check-in & Delta Güncelleme)

Bir görev tamamlandığında tüm hafıza dosyalarını baştan yazma; yalnızca **delta (fark)** güncellemesi yap:

1. **`docs/ai-context/current-state.md` Güncellemesi (Maksimum 50 Satır):**
   - `Last Verified Commit` alanını `git rev-parse --short HEAD` çıktısıyla güncelle.
   - Tamamlanan işi tek maddede `[VERIFIED]` etiketiyle ekle.
   - Gerçekte çalıştırılan testleri `[VERIFIED]`, yalnızca eski rapordan okunanları `[REPORTED]`, test edilmeyenleri `[UNVERIFIED]` olarak işaretle.
   - Dosya 50 satırı aşarsa en eski tamamlanan maddeleri `docs/ai-context/history/YYYY-MM-DD-<konu>.md` dosyasına taşı.
2. **`docs/ai-context/decisions.md` Güncellemesi:**
   - Görev sırasında yeni bir mimari/teknik karar alındıysa dosya sonuna yeni bir `ADR-XXX` maddesi (`#etiket`, göreli dosya yolu, commit hash ve `[VERIFIED]` etiketiyle) ekle.
   - Eski bir kararla çelişen durum varsa eski kararı silmeden `[CONFLICT]` veya `[STALE]` olarak işaretle.
3. **`docs/ai-context/module-map.md` Güncellemesi:**
   - Yalnızca yeni bir sınıf/dosya eklendiyse veya taşındıysa ilgili satırı güncelle.

---

## 4. Güvenlik ve Taşınabilirlik Kuralları

- **Sıfır Secret:** `.env`, `.env.local`, API anahtarı, token veya parolaları asla hafıza dosyalarına yazma.
- **Göreli Yol Standardı:** Hafıza dosyalarında `C:\Users\...` gibi mutlak yollar yerine daima proje köküne göre göreli yollar (`Assets/Jelly/Runtime/...`, `docs/ai-context/...`) kullan.
- **Onaysız Push Yasağı:** Kullanıcı açıkça onay vermeden `git push` çalıştırma.
