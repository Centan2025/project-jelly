# Project Jelly — Gameplay Specification

## 1. Core Loop & Controls
- **Desktop:** `WASD` / Arrow keys for directional rolling, `Left Shift` / `Ctrl` for braking, `Space` for splitting, `E` / `M` for manual reuniting.
- **Mobile Touch:**
  - Horizontal drag: Left/Right steering.
  - Vertical drag: Forward/Backward speed control.
  - Hold stationary (>0.35s) or Brake UI button: Controlled braking.
  - Action button: Split / Reunite toggle.
- **Physics Feel:** Acceleration, slope projection, uphill torque assist, downhill speed cap, and surface friction multipliers (`SurfaceFrictionZone`) all operate through `Rigidbody.AddForce` and `AddTorque` in `FixedUpdate`.

## 2. Mass, Volume & Deformation Model
- **Default Mass Parameters:**
  - Starting mass: `100`
  - Playable range: `[20, 200]`
  - Pickup delta: `+20` (`JellyMassPickup`)
  - Drain delta: `-20` (`JellyMassDrain`)
- **Physical Radius Scaling:**
  $$r = r_0 \cdot \left(\frac{m}{m_0}\right)^{1/3}$$
  where $r_0 = 0.5\text{ m}$ and $m_0 = 100$.
- **Safe Collider Interpolation:** `JellySizeInterpolator` smoothly transitions radius over time and elevates the `Rigidbody` center by $\Delta r$ when growing so the sphere never penetrates the floor.
- **Visual vs. Gameplay Deformation:** `JellyDeformationController` applies volume-preserving squash-and-stretch on the child visual mesh for acceleration, braking, ground landing, wall impact, and slot squeezing, while `GateClearanceEvaluator` strictly checks physical radius and maximum compression limits (`0.62` lateral, `0.65` vertical).

## 3. Gate & Obstacle Rules
1. **CircularGate:** Requires `candidate.Radius <= apertureRadius`.
2. **RectangularGate:** Requires `candidate.Radius <= halfWidth` and `halfHeight`.
3. **NarrowSlotGate:** Allows passage only if `slotHalfWidth / candidate.Radius >= maxLateralCompressionRatio` (`0.62`). Applies real lateral squeeze on passage.
4. **CompressibleGate:** Expands effective aperture based on approach momentum ($m \cdot v$).
5. **MassThresholdGate:** Requires `minAllowedMass <= m <= maxAllowedMass` plus geometric fit.
6. **SplitGate:** Blocks unsplit high-mass jellies; requires active split state and fragment mass $\le 60$.
7. **TimedGate:** Opens and closes on a deterministic cycle (`openDuration` / `closedDuration`).

## 4. Split & Merge Conservation Rules
- **Conservation Law:** Splitting $100 \to 50 + 50$ (or configurable ratio such as $70 + 30$) strictly preserves total mass ($m_A + m_B = m_{\text{total}}$).
- **Reuniting:** Merging $50 + 50 \to 100$ combines mass and momentum-weighted velocity at a collision-safe midpoint.
- **Fragment Loss:** If one $50$-mass fragment falls into the abyss, `JellyFragmentRegistry.ReportFragmentLost` records $50$ lost mass and leaves the surviving fragment at $50$ mass without phantom mass regeneration.

## 5. First 10 Vertical Slice Levels & 11–50 Expansion
- **Level 01 — First Roll:** Flat and sloped movement, wide circular gate, goal zone.
- **Level 02 — Grow:** Collect `+20` mass pickups to reach `140` mass and activate the `120`-mass pressure plate & threshold gate.
- **Level 03 — Shrink:** Pass over mass drains (`100 -> 50`) to fit through the `0.43m` small circular aperture.
- **Level 04 — Choose Your Size:** Dual route—grow on the left or shrink on the right.
- **Level 05 — Split:** Split `100 -> 50 + 50` to pass the `SplitGate`.
- **Level 06 — Reunite:** Split to pass `SplitGate`, then reunite (`50 + 50 -> 100`) to pass the `90`-mass gate and satisfy `requireSingleMergedJellyAtGoal`.
- **Level 07 — Heavy Impact:** Grow to `140` mass and hit the `SpringLauncher` to shatter the `MomentumBreakableBarrier`.
- **Level 08 — Squeeze:** Squeeze through a `0.35m` half-width `NarrowSlotGate`.
- **Level 09 — Mass Survival:** Navigate alternating pickups and drains to reach the goal with $\ge 80$ mass.
- **Level 10 — The Jelly Trial:** Combines narrow slot squeeze, split gate, reunite, pickup growth, spring boost, and final momentum barrier.
- **Levels 11–50:** 40 data-driven expansion levels across 4 difficulty tiers (`11–20` Combined Mechanics, `21–30` Multi-Path & Split, `31–40` Momentum & Timing, `41–50` Master Trial).
