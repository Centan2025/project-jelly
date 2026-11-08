# Yeni Bilgisayar, MCP (`uv`, `serena`, `basic-memory`) ve Ortam Kurulum Rehberi (L3 Storage)

> Bu rehber, projeyi GitHub üzerinden yeni bir bilgisayara klonladığınızda hem Unity geliştirme ortamını hem de yapay zekâ ajanlarının (Antigravity / Claude / Cursor) token tasarruflu kod analiz araçlarını (Serena MCP & Basic Memory) kurmak için kullanılır `[VERIFIED]`.

---

## 1. Depoyu Klonlama ve Hafıza Doğrulaması
```powershell
git clone <REPO_URL> JellyShift
cd JellyShift
git status --short
```
- Klonlama sonrasında `AGENTS.md`, `docs/ai-context/*` ve `.agents/skills/memory-manager/SKILL.md` dosyaları otomatik olarak gelir `[VERIFIED]`.
- Yeni oturum açan ajan otomatik olarak önce `AGENTS.md` (L1) ve `docs/ai-context/current-state.md` (L2) dosyalarını okur.

---

## 2. `uv` Paket Yöneticisi Kurulumu
Serena MCP ve Basic Memory, Python tabanlı `uv` (`uvx`) paket yöneticisi ile sanal ortam kirletmeden çalıştırılır.

### Windows (PowerShell):
```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
uv --version
```
### macOS / Linux:
```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
```

---

## 3. Serena MCP Kurulumu (LSP / Sembol Tabanlı Akıllı Kod Erişimi)
Serena MCP, C# dosyalarını baştan sona okumak yerine doğrudan sınıf/metot sembollerini (`get_symbols_overview`, `find_symbol`, `find_referencing_symbols`) sorgulamayı sağlar.

### Antigravity / MCP Yapılandırması (`mcp_config.json`)
Kullanıcı MCP yapılandırma dosyanıza (`~/.gemini/antigravity/mcp_config.json` veya IDE MCP ayarlarına) şu sunucuyu ekleyin:

```json
{
  "mcpServers": {
    "serena": {
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/oraios/serena",
        "serena",
        "start-mcp-server",
        "--context",
        "ide-assistant"
      ]
    },
    "basic-memory": {
      "command": "uvx",
      "args": [
        "basic-memory",
        "mcp"
      ]
    }
  }
}
```

### Serena Proje Aktivasyonu ve `.gitignore` Standardı
- `.gitignore` dosyasında `.serena/*` önbellek dosyaları dışlanmış, ancak `!.serena/project.yml` ve `!.serena/memories/` korunmuştur `[VERIFIED]`.
- Serena MCP aktif olduğunda oturum başında şu araç çağrılır:
  - `activate_project` (proje kök dizini veya proje adı ile)
  - `get_symbols_overview` / `find_symbol`
- **Yedek (Fallback) Modu:** Eğer bilgisayarda henüz Serena MCP kurulu değilse, ajan `docs/ai-context/module-map.md` dosyasından ilgili C# dosyasını bulur ve `Select-String` / `ripgrep` (`rg`) ile hedef satır numarasını tespit ederek yalnızca ilgili satır aralığını (`StartLine`–`EndLine`) okur `[VERIFIED]`.

---

## 4. Unity 6 Geliştirme Ortamı Kurulumu
1. **Unity Hub** üzerinden `Unity 6000.5.6f1` (Android Build Support + Universal Render Pipeline dahil) sürümünü kurun `[VERIFIED]`.
2. Projeyi Unity Editor ile açın.
3. `.gitignore` ile dışlanan prosedürel varlıkları yeniden oluşturmak için üst menüden:
   - `Jelly -> Generate All Project Assets & Scenes` (`JellyProjectBuilder.GenerateAll`) komutunu çalıştırın `[VERIFIED]`.
4. İsteğe bağlı olarak Blender (`Blender/build_project_jelly_kit.py`) ile `.glb` modellerini yeniden üretebilirsiniz:
   ```powershell
   blender --background --python Blender/build_project_jelly_kit.py
   ```
