# Entegrasyonlar, Veri Sözleşmeleri ve Dış Sistemler (L3 Storage)

> **Güvenlik Kuralı:** Bu dosyaya asla gerçek API anahtarı, token, parola veya `.env.local` içeriği yazılmaz `[VERIFIED]`.

## 1. Unity Paket ve Motor Bağımlılıkları (`Packages/manifest.json`)
- **Unity Sürümü:** `6000.5.6f1` (Unity 6) `[VERIFIED]`
- **Render Pipeline:** `com.unity.render-pipelines.universal` (`17.0.1`) — PC (`Assets/Settings/PC_RPAsset.asset`) ve Mobil (`Assets/Settings/Mobile_RPAsset.asset`) profilleri `[VERIFIED]`.
- **Girdi Sistemi:** `com.unity.inputsystem` (`1.20.0`) — `Assets/InputSystem_Actions.inputactions` ve `JellyInputAdapter.cs` üzerinden çalışır `[VERIFIED]`.
- **Test Çatısı:** `com.unity.test-framework` (`1.4.2`) — `Jelly.Tests.EditMode` ve `Jelly.Tests.PlayMode` assembly'leri `[VERIFIED]`.
- **UI & Navigasyon:** `com.unity.ugui` (`2.0.0`), `com.unity.ai.navigation` (`2.0.14`), `com.unity.timeline` (`1.8.12`) `[VERIFIED]`.

---

## 2. Yerel Kayıt Sistemi Sözleşmesi (`SaveGameService`)
- **Dosya & Sınıf:** `Assets/Jelly/Runtime/JellyLevelSystem.cs` -> `SaveGameService`, `SaveGameData`, `LevelProgressRecord` `[VERIFIED]`
- **Veri Formatı:** `UnityEngine.JsonUtility` ile serileştirilen JSON (`Application.persistentDataPath` altında veya testlerde özel dosya yolunda) `[VERIFIED]`.
- **Şema Özeti (`SaveGameData`):**
  - `unlockedLevelIndex` (`int`): Açık olan en yüksek seviye indeksi.
  - `totalStars` (`int`): Kazanılan toplam yıldız.
  - `soundEnabled` (`bool`), `hapticsEnabled` (`bool`): Ses ve titreşim tercihleri.
  - `levelRecords` (`List<LevelProgressRecord>`): Seviye bazlı tamamlanma durumu, en iyi kütle ve yıldız sayısı `[VERIFIED]`.

---

## 3. Blender 3B Üretim Hattı (`Blender/` <-> `Assets/Jelly/`)
- **Betik:** `Blender/build_project_jelly_kit.py` `[VERIFIED]`
- **Runtime Karşılığı:** `Assets/Jelly/Runtime/BlenderKitModelSpawner.cs` `[VERIFIED]`
- **Sözleşme:** `.blend` ve `.glb` çıktıları `.gitignore` ile yerel tutulur (`Assets/Jelly/Models/*.glb`). Çalışma zamanında `BlenderKitModelSpawner` aynı geometrik kurallarla engelleri, geçitleri ve yarı saydam küreleri doğrudan Unity mesh/materyal hiyerarşisi olarak üretir `[VERIFIED]`.

---

## 4. Vercel Statik Web Vitrini (`index.html` & `vercel.json`)
- **Dosyalar:** `index.html`, `vercel.json` (`{"cleanUrls": true}`), `.vercelignore`, `images/` `[VERIFIED]`
- **Canlı Adres:** `https://project-jelly.vercel.app` `[REPORTED]` (`README.md` içinde belirtilmiş)
- **Güvenlik Notu:** Kök dizinde `.env.local` ve `.vercel/` klasörü mevcuttur ve `.gitignore` tarafından dışlanmaktadır (`.env*`, `.vercel/`). İçerikleri kesinlikle okunmamalı ve Git'e eklenmemelidir `[VERIFIED]`.

---

## 5. MCP Sunucuları ve Çoklu Proje Bağlamı
- **Mevcut Ortam MCP Sunucusu:** `science_biomolecular_structure_viewer_mcp` (Bilimsel 3B molekül görüntüleyici; bu Unity oyun projesiyle doğrudan ilişkisi yoktur) `[VERIFIED]`.
- **Serena MCP & Hafıza:** `.serena/project.yml` ve `.serena/memories/` Git ile taşınabilir şekilde yapılandırılmıştır; kurulum adımları `Docs/ai-context/setup.md` içindedir `[VERIFIED]`.
- **Basic Memory İzolasyon Standardı:**
  - **Tek Doğruluk Kaynağı (SSOT):** Proje deposundaki `Docs/ai-context/*.md` dosyaları birincil doğruluk kaynağıdır `[VERIFIED]`.
  - **Projeye Özgü Ad Alanı:** Basic Memory'ye yazılması gerektiğinde mutlaka projeye özgü ad alanı (`project="JellyShift"`) hedeflenir `[VERIFIED]`.
  - **`main` Alanına Yazma Yasağı:** Global `main` alanı genel sistem alanıdır; bu projeye varsayılan yazma hedefi olarak **asla kullanılamaz** `[VERIFIED]`.
  - **Salt-Okunur Keşif (Test E):** Bağlantı ve durum doğrulamaları `bm_project(command="list")` ile salt-okunur yapılır; kullanıcı onayı olmadan proje oluşturulmaz, test verisi yazılmaz veya kayıt silinmez `[VERIFIED]`.
