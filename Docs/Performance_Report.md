# Project Jelly — Performance Report

**Date:** 2026-10-31  
**Target Platform:** Android (Primary) / iOS-compatible URP Mobile Architecture  
**Phase Status:** `CONDITIONAL PASS` (Editor PlayMode & APK build metrics verified; physical Android device runtime profiling is `NOT VERIFIED` due to no attached ADB device)

## 1. Recorded PlayMode Runtime Metrics (`Level_10 — The Jelly Trial`)
Extracted from `Logs/playmode_tests.log` (`[PERF_METRICS]`):

| Metric | Recorded Value (PlayMode Batch) | Mobile Target Budget | Status |
|---|---|---|---|
| **Average Frame Time** | `0.09 ms` (>1000 FPS headless) | `< 16.67 ms` (60 FPS) | `PASS` |
| **99th Percentile (1% Low) Frame Time** | `0.24 ms` | `< 22.00 ms` | `PASS` |
| **Active Rigidbodies (Single / Split)** | `1` (Single) / `2` (Split) | `< 16` | `PASS` |
| **Active Colliders in Level 10** | `11` | `< 128` | `PASS` |
| **Runtime GC Allocations in Steady Loop** | `0 B / frame` (Pooled fragments & FX) | `0 B / frame` steady | `PASS` |
| **Total Allocated Memory (Editor Host)** | `437 MB` | `< 600 MB` | `PASS` |
| **Mono Heap Used** | `~664 KB` | `< 32 MB` | `PASS` |
| **Android APK Size (`ProjectJelly_V1.apk`)** | `29.94 MB` (`29,936,106` bytes) | `< 80 MB` | `PASS` |

## 2. Mobile Optimization Architecture
1. **Object Pooling:**
   - [`JellyFragmentPool`](file:///c:/Users/ASUS/.gemini/antigravity/scratch/JellyShift/Assets/Jelly/Runtime/JellySplitAndMergeSystem.cs#L137-L169) reuses split fragment `GameObject` instances during repeated split/merge cycles so no `Instantiate`/`Destroy` spikes occur mid-level.
   - [`JellyFXPool`](file:///c:/Users/ASUS/.gemini/antigravity/scratch/JellyShift/Assets/Jelly/Runtime/JellyCameraVisualAndUISystem.cs#L95-L141) pools `ParticleSystem` bursts.
2. **Low-Cost URP Visuals:**
   - Uses `Universal Render Pipeline/Lit` pastel materials (`M_JellyStylized.mat`, `M_TrackPastel.mat`, `M_GateAccent.mat`) without expensive screen-space refraction or grab-pass shaders.
   - Squash-and-stretch deformation is computed analytically on the visual child transform (`JellyDeformationController`) in $O(1)$ per frame rather than per-vertex CPU mesh skinning.
3. **Adaptive Quality Profiles:**
   - [`JellyMobileQualityManager`](file:///c:/Users/ASUS/.gemini/antigravity/scratch/JellyShift/Assets/Jelly/Runtime/JellyCameraVisualAndUISystem.cs#L172-L181) supports switching between a **60 FPS Default Profile** and a **30 FPS Battery/Low-End Profile** (`vSyncCount = 0`).
