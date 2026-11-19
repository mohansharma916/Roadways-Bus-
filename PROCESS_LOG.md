# Development Process & Instructions Log

This document records the step-by-step progress, specifications, design guidelines, and latest instructions for the **Roadways Bus Game** project. It is updated after each user prompt and committed to Git to maintain a clean revision history.

---

## 1. Project Specifications & Design Rules

### A. Environment & Visual Theme
- **Theme**: Cherry Blossom Park (soft pastel pinks, subtle concentric ring paths, stone garden accents).
- **Restrictions**:
  - **Zero Trees**: Strictly no trees permitted in the park.
  - **Zero Leaves**: Strictly no leaf particles or petal meshes on the ground or in the air.
  - **Zero Obstacles/Vehicles/Characters**: Completely clean plane park surface.
- **Camera Perspective**:
  - Direct bird's-eye top-down view looking down along the Z/Y axes (`camera.up.set(0, 0, -1)`, `lookAt(0, 0, targetZ)`).
  - Completely **static**: No camera dragging, panning, orbit rotation, or idle bobbing/movement.

### B. UI Overlay Structure
1. **Bottom 60% Panel (`.bottom-boundary-panel`)**:
   - Covers bottom 60% of the screen (`bottom: 0; height: 60%; width: 100%`).
   - Fill color: `#d3daed`.
   - Bottom, left, and right borders are invisible (`border: none`).
   - Top border: Thick 3D embossed border (`9px solid #e1e6ec`) with specular highlights and bevel shading.
2. **Trapezium Panel (`.trapezium-panel`)**:
   - Sits directly above the bottom 60% panel (`bottom: 60%`).
   - Height: **10%** vertical coverage (reduced from initial 20% to exactly 10%).
   - Fill color: `#7a7d90`.
   - Upper Width: Expanded to cover **80% of the screen width** at the top edge, leaving only a 10% margin on each side.
   - Bottom Width & Shape: Bottom corners extended so that **30% is off-screen/invisible on both the left and right sides** (`left: -30%`, `width: 160%`, `clip-path: polygon(25% 0%, 75% 0%, 100% 100%, 0% 100%)`).
3. **Oval Roundabout (`.roundabout-container`)**:
   - Position: Shifted up by 30px (`bottom: calc(67.8% + 30px)`, centered horizontally, width `86%`, height `20%`).
   - Structure & Dimensions:
     - Outer oval radius: `rx=192, ry=100`.
     - Inner oval radius: `rx=92, ry=40` (thick ring road).
   - Junction & Open Path Architecture:
     - **Trapezium Junction**: Extended neck connector reaches down into the trapezium; bottom outer border opened by ~35-40px.
     - **Horn Road Junctions**: Outer ring border is **completely removed** across the left junction ($x \in [14.6, 40.9], y \in [54, 84]$) and right junction ($x \in [359.1, 385.4], y \in [54, 84]$), allowing seamless, borderless road flow into the ring.
   - Styling:
     - Outer & Inner borders: 3D/curb stroke `#e1e6ec` (4.5px).
     - Road track surface: Filled with `#7a7d90`.
     - Central island: Hollow/empty (`mask="url(#roundabout-center-hole)"`).
4. **Cow-Horn Curved Feeder Roads (`.horns-container`)**:
   - Structure: Two sweeping horn-shaped roads connecting device screen corners into the oval roundabout at 30° angles.
   - Road Width: Expanded to a wider road profile (~65px equivalent road surface).
   - Borderless Junction Merging:
     - Road surfaces filled with `#7a7d90` extend into the roundabout ring.
     - Outer curbs (`#e1e6ec`) connect directly to the ring's Top Arc.
     - Inner curbs (`#e1e6ec`) connect directly to the ring's Lower Arcs.
     - The ring's outer border is eliminated across the road entrances for a 100% clean merge.

---

## 2. Process & Commit History

| Step | Timestamp | Change Description |
|---|---|---|
| 1 | 2026-11-19 | Generated 3D Showcase & Unity asset pipeline for game elements. |
| 2 | 2026-11-19 | Converted scene into Plain Cherry Blossom Park (stripped trees, leaves, vehicles). |
| 3 | 2026-11-19 | Set camera to direct top-down bird's-eye view and locked all motion/interaction. |
| 4 | 2026-11-19 | Added bottom 60% panel filled with `#d3daed` and 3D top border `#e1e6ec`. |
| 5 | 2026-11-19 | Added `#7a7d90` trapezium panel above the 60% boundary. |
| 6 | 2026-11-19 | Adjusted trapezium: lowered height to 12%, expanded width with bottom corners cut off by 30% on both sides. |
| 7 | 2026-11-19 | Initialized repository tracking, created `PROCESS_LOG.md`, and established per-turn commit routine. |
| 8 | 2026-11-19 | Widened trapezium upper width to 80% screen width (10% margins) and set height coverage to exactly 10%. |
| 9 | 2026-11-19 | Created oval roundabout with inner and outer rings (#e1e6ec / #7a7d90) and hollow center above trapezium. |
| 10 | 2026-11-19 | Shifted roundabout toward trapezium and opened a ~35-40px entrance path in the outer border to merge the roads. |
| 11 | 2026-11-19 | Increased ring thickness (gap between inner and outer) and expanded overall oval width/radius. |
| 12 | 2026-11-19 | Added dual 40px cow-horn curved roads from top screen corners connecting to roundabout at 30°. |
| 13 | 2026-11-19 | Shifted ring up by 30px, widened horn roads to ~65px, and removed ring borders at junctions for seamless merge. |

---

## 3. Active Test Environment
- **Target Device**: Android Phone (`Realme RMX3690`, ADB id `2905321030008921`).
- **Live Preview Server**: Python HTTP server on port `8089` bridged via `adb reverse tcp:8089 tcp:8089`.
- **Target File**: `3D_Showcase_Preview.html` mirrored to `/sdcard/Download/3D_Showcase_Preview.html`.
