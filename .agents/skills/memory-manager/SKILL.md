---
name: memory-manager
description: Oturum başında proje hafızasını (Docs/ai-context) doğrular, Context Capsule ve iki aşamalı Serena protokolüyle bağlamı hedefler, görev devrini (handoff) ve görev sonu delta güncellemelerini yönetir.
---

# Hafıza Yönetim ve Bağlam Optimizasyon Protokolü (V2.1)

Bu beceri, ajanların tüm kod tabanını gereksiz taramasını önleyen, L1/L2/L3 katmanlarını yöneten ve görevler arası bağlam devrini sağlayan protokolü tanımlar.

## 1. Oturum Başlangıcı (Wake-up)
1. `AGENTS.md` ve `Docs/ai-context/current-state.md` dosyalarını oku; `Last Verified Commit` (`96fe4e1`) değerini al.
2. Aktif handoff kaydı (`.handoff.local.md` veya `Docs/ai-context/handoff.md`) olup olmadığını kontrol et; görev önceki işin devamıysa buradan sürdür.
3. Serena MCP mevcutsa proje için `activate_project` çalıştır.
4. `git status --short` ve `git diff <Last_Verified_Commit>..HEAD --name-status` çalıştır.
5. Görevle ilgisiz L3 dosyalarını otomatik yükleme.

## 2. Görev Yönlendirme & Context Capsule
1. Görevi sınıflandır (UI/UX, Bug fix, Physics, Level, Performance, Refactor, Docs, Testing).
2. `Docs/ai-context/module-map.md` tablosundan ilgili modülü bul.
3. Gerekliyse `Docs/ai-context/decisions.md` ve `Docs/ai-context/integrations.md` içindeki ilgili kayıtları seç.
4. Oturum belleğinde `Docs/ai-context/context-capsule.md` şemasına uygun kompakt bir Context Capsule oluştur.

## 3. Serena İki Aşamalı Sonuç Protokolü
- **Seviye A (Kısa Sonuç — Varsayılan):** `s.lsp.get_symbols_overview` ve `s.lsp.find_symbol(..., include_body=False, include_info=True)` ile yalnız sembol adı, türü, dosya yolu, satır aralığı ve referans konumlarını al.
- **Seviye B (Ayrıntılı Okuma):** Yalnızca iş mantığı değiştirilecekse, veri akışı belirsizse veya güvenlik incelemesi yapılıyorsa sembol gövdesini (`include_body=True`) veya ilgili satır aralığını aç.
- **Fallback:** Dil sunucusu başlatılamadığında `s.fs.search_for_pattern` veya hedefli `Select-String` / `grep` ile satır bul.

## 4. Dosya Değişikliğine Bağlı Hafıza Geçerliliği (Evidence Invalidation)
1. Değişen dosyaları `Docs/ai-context/evidence-index.md` içindeki `source_paths` ile eşleştir.
2. Yalnızca etkilenen kayıtları incele; değişmişse `[STALE]`, çelişiyorsa `[CONFLICT]`, doğrulandıysa güncel commit ile `[VERIFIED]` işaretle.
3. Global Git hook (`core.hooksPath`) ayarlarını değiştirme.

## 5. Handoff, Check-in ve Arşivleme
1. Kesintiye uğrayan görevleri `.handoff.local.md` (veya şablon olarak `Docs/ai-context/handoff.md`) içine kaydet.
2. Görev sonunda tüm hafızayı baştan yazma; yalnızca değişen kısımları (delta) güncelle.
3. `Docs/ai-context/current-state.md` dosyasını <= 50 satırda tut; eski tamamlanan maddeleri `Docs/ai-context/history/` altına arşivle.
4. Secret, token veya `.env` değerlerini asla hafızaya yazma; daima göreli dosya yolları kullan.
