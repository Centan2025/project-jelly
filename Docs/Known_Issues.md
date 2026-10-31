# Project Jelly — Known Issues & Limitations

## 1. Unverified Hardware-Dependent Items (`NOT VERIFIED`)
- **Physical Android Device Deployment & Logcat:**
  - `adb devices -l` returned no connected Android device or running emulator on the host machine.
  - While the Android APK (`Builds/Android/ProjectJelly_V1.apk`, `29.94 MB`) was built and verified with `0` errors using Unity `6000.5.6f1` Android Playback Engine, physical on-device installation, touch latency feel, on-device GPU frame timing, and `adb logcat` capture remain `NOT VERIFIED` until a physical device is plugged in.

## 2. Current Scope Tradeoffs & Future Enhancements
- **Procedural Box/Sphere Primitives for Level Geometry:**
  - Levels `01–50` use clean, minimalist 3D primitives (`Cube` / `Sphere`) with URP pastel materials generated deterministically from `JellyLevelDefinition`. Custom 3D environment meshes (curved pipes, beveled gate frames) can be swapped into `LevelLoader` without changing gameplay logic.
- **Audio & Haptics Hooks:**
  - Save data (`SaveGameData.soundEnabled`, `SaveGameData.hapticsEnabled`) and event hooks (`OnMassChanged`, `OnSplitPerformed`, `OnMerged`, `OnWallImpact`, `OnGroundImpact`, `OnLevelCompleted`) are wired; studio audio clips can be assigned when audio assets are imported.
- **Analytical Visual Deformation vs. Vertex Shader Wobble:**
  - Visual deformation uses volume-preserving analytical 3-axis spring scaling (`JellyDeformationController`). If per-vertex surface ripple is desired on high-tier devices, a custom Shader Graph vertex offset can read velocity/impact vectors from `JellyDeformationController`.
