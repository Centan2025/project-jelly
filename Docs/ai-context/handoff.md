# Project Jelly — Görev Devir ve Durum Handoff Protokolü (L2/L3)

## 1. Standart Handoff Şeması

```yaml
task_id: "TASK-20261108-AI-MEMORY-V2.1"
task_summary: "AI Context V2.1 yükseltmesi ve Serena/Basic Memory doğrulaması"
status: "in_progress" # in_progress | completed | blocked
branch: "main"
base_commit: "96fe4e1"
last_checked_commit: "96fe4e1"
working_tree_state: "modified"
changed_files:
  - "Assets/Scenes/SampleScene.unity (uncommitted, kullanıcı çalışması)"
verified_findings:
  - "Serena MCP csharp dil sunucusu ve LSP sembol araçları aktif"
  - "Basic Memory MCP salt-okunur ve proje izolasyonlu olarak bağlandı"
  - "L1/L2/L3 V2.1 dosyaları oluşturuldu ve doğrulandı"
decisions:
  - "DEC-001..DEC-005 evidence-index.md içine mühürlendi"
tests_run:
  - "verify-memory.mjs yapısal ve politika kontrolü"
  - "Serena Seviye A find_symbol(GateClearanceEvaluator) lsp testi"
  - "Basic Memory bm_project ve bm_list bağlantı testi"
tests_not_run:
  - "Fiziksel Android cihaz testi (cihaz bağlı değil)"
known_issues:
  - "SampleScene.unity üzerinde commitlenmemiş yerel sahne verisi korunuyor"
next_steps:
  - "Kullanıcı onayı olmadan commit veya push yapmadan sonuç raporunu sun"
```

## 2. Handoff Kuralları ve Git Çakışma Önleme Politikası
- **İçerik:** Yalnızca gerçekte yapılan işleri, doğrulanmış bulguları, çalıştırılan/çalıştırılmayan testleri ve net sonraki adımı yaz. Tam kaynak kod, uzun log veya secret yazma.
- **Yerel İzolasyon:** Farklı bilgisayarlar veya dallar arasında Git çakışmasını önlemek için geçici aktif handoff kayıtlarını Git dışında/ignore edilen yerel dosyada (`.opencode/handoff.md` veya `.handoff.local.md`) tut.
- **Arşiv:** Tamamlanan önemli görev özetlerini `Docs/ai-context/history/` altında arşivle.
