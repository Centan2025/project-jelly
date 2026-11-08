# Project Jelly — Hafıza Kanıt İndeksi ve Geçerlilik Matrisi (L3 Storage)

Bu indeks, mimari kararların ve sözleşmelerin hangi kaynak dosyalara ve hangi doğrulanmış commit'e dayandığını izler.

## 1. Kanıt Kayıtları Dizini

```yaml
records:
  - record_id: "DEC-001"
    title: "Küp Kök Kütle-Yarıçap Ölçeklemesi ve Kütle Korunumu"
    source_paths:
      - "Assets/Jelly/Runtime/JellyMassAndMorphSystem.cs"
      - "Assets/Jelly/Runtime/JellySplitAndMergeSystem.cs"
    verified_commit: "96fe4e1"
    verified_date: "2026-11-08"
    evidence_type: "source_code"
    status: "VERIFIED"

  - record_id: "DEC-002"
    title: "FixedUpdate Fizik Bütünlüğü ve Nesne Havuzlama (Object Pooling)"
    source_paths:
      - "Assets/Jelly/Runtime/JellyMotor.cs"
      - "Assets/Jelly/Runtime/JellyPhysicsBody.cs"
      - "Assets/Jelly/Runtime/JellyFragmentPool.cs"
      - "Assets/Jelly/Runtime/JellyFXPool.cs"
    verified_commit: "96fe4e1"
    verified_date: "2026-11-08"
    evidence_type: "source_code"
    status: "VERIFIED"

  - record_id: "DEC-003"
    title: "8 Modüler Pist Segmenti ve 15 Seviyeli İlerlemeli Harita Üretimi"
    source_paths:
      - "Assets/Jelly/Runtime/JellyProgressiveMapGenerator.cs"
      - "Assets/Jelly/Runtime/JellyTrackSegmentNode.cs"
      - "Assets/Jelly/Runtime/EndlessTrackManager.cs"
    verified_commit: "96fe4e1"
    verified_date: "2026-11-08"
    evidence_type: "source_code"
    status: "VERIFIED"

  - record_id: "DEC-004"
    title: "Prosedürel 3B Blender Kit Üreticisi ve Gerçek Zamanlı Deformasyon"
    source_paths:
      - "Assets/Jelly/Runtime/BlenderKitModelSpawner.cs"
      - "Assets/Jelly/Runtime/JellyRealtimeDeformer.cs"
      - "Blender/build_project_jelly_kit.py"
    verified_commit: "96fe4e1"
    verified_date: "2026-11-08"
    evidence_type: "source_code"
    status: "VERIFIED"

  - record_id: "DEC-005"
    title: "Üç Katmanlı (L1/L2/L3) Kalıcı Proje Hafızası ve V2.1 Bağlam Optimizasyonu"
    source_paths:
      - "AGENTS.md"
      - "Docs/ai-context/current-state.md"
      - "Docs/ai-context/module-map.md"
      - ".agents/skills/memory-manager/SKILL.md"
    verified_commit: "96fe4e1"
    verified_date: "2026-11-08"
    evidence_type: "repository_structure"
    status: "VERIFIED"
```

## 2. Değişiklik Tespiti ve Etki Analizi
1. Oturum başında `git status --short` ve `git diff --name-status <verified_commit>..HEAD` çalıştır.
2. Değişen dosyaları `source_paths` ile karşılaştır.
3. Yalnızca değişen kaynaklarla ilişkili kayıtları incele; davranış değişmişse `[STALE]`, çelişki varsa `[CONFLICT]`, hâlâ geçerliyse yeni commit ile `[VERIFIED]` olarak güncelle.
4. Etkilenmeyen kayıtlara dokunma; global Git hook ayarlarını (`core.hooksPath`) asla değiştirme.
