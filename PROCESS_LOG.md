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
1. **Bottom Panel (`.bottom-boundary-panel`)**:
   - Covers bottom of the screen (`bottom: 0; height: calc(60% - 50px); width: 100%`), reduced by 50px to accommodate downward expansion of the trapezium.
   - Fill color: `#d3daed`.
   - Bottom, left, and right borders are invisible (`border: none`).
   - Top border: Thick 3D embossed border (`9px solid #e1e6ec`) with specular highlights and bevel shading.
2. **Trapezium Panel (`.trapezium-panel`)**:
   - Bottom edge lowered downwards by 50px (`bottom: calc(60% - 50px)`).
   - Height: **`calc(10% + 50px)`** vertical coverage (expanded downwards by 50px from initial 10%).
   - Fill color: `#7a7d90`.
   - Upper Width: Covers **80% of the screen width** at the top edge ($Y=70\%$), leaving a 10% margin on each side.
   - Bottom Width & Shape: Bottom corners extended so that **30% is off-screen/invisible on both the left and right sides** (`left: -30%`, `width: 160%`).
   - **Rounded Top Corners**: Rendered with an SVG path (`preserveAspectRatio="none"`) using quadratic bezier curves (`Q 400,0 480,0` and `Q 1200,0 1272,180`) to provide a smooth, organic corner radius where the top edge bends into the diagonal sloped sides, eliminating sharp corners.
   - **Angled Rectangular Parking Bays (`.trapezium-boxes-container` & `.trapezium-box`)**:
     - 9 identical vertical rectangular bus bays evenly spaced in a horizontal flex container across the trapezium (`width: 82vw; justify-content: space-between`).
     - Dimensions: `width: 30px; height: 54px`.
     - Corner radius: `8px` rounded corners.
     - Orientation: Tilted 30° towards the left (`transform: rotate(-30deg)`).
     - Fill Color: `#626479`.
     - Border: `2px solid #ffffff` (white).
     - Fitting: Entirely contained inside the trapezium with generous clearance from top neck, bottom boundary, and side sloped edges.
3. **Unified Track & Roundabout Network (`.track-network-container`)**:
   - Layout Architecture: Merged into a unified SVG spanning the upper 40% of the screen (`viewBox="0 0 1000 400"`), eliminating sub-pixel CSS offsets and coordinate mismatches.
   - Oval Roundabout Dimensions:
     - Horizontal width reduced from corner edges (`Rx=345, Ry=88`), leaving generous breathing room (~154 units on each side) for sweeping feeder curves.
     - Center island: Masked hollow hole (`cx=500, cy=185, rx=160, ry=35`).
   - Bottom Trapezium Connection:
     - Width **substantially expanded** to 200 viewBox units (~145 screen px, from $X=400$ to $X=600$), creating a wide, prominent highway connection bridging the oval roundabout down into the trapezium.
   - Dual Horn Feeder Roads:
     - Road width widened significantly (~65-70px profile).
     - Curvature smoothed into gentle, organic curves with tangential fillets, eliminating sharp/pointy corners.
   - Borderless Junctions:
     - Ring border is completely removed at the left, right, and bottom junctions.
     - Curbs (`#e1e6ec`, 5.5px) flow continuously from screen corners around the roundabout and down to the trapezium.
     - Road surface (`#7a7d90`) is one continuous solid polygon.

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
| 14 | 2026-11-19 | Widened trapezium neck by +30px, reduced oval width from corners, expanded horn widths, and smoothed curves into a unified vector network. |
| 15 | 2026-11-19 | Expanded connection width between ring and trapezium to 200 units (~145 screen px) for a wide road highway trunk. |
| 16 | 2026-11-19 | Added centered small rectangular box (140x42px) with 2px #626479 border inside the trapezium. |
| 17 | 2026-11-19 | Updated trapezium box: set fill color to #626479 and border to 2px solid white (#ffffff). |
| 18 | 2026-11-19 | Reshaped trapezium box to vertical orientation (width < height, 42px x 68px). |
| 19 | 2026-11-19 | Expanded trapezium height 50px downwards (lowered bottom boundary to calc(60% - 50px)) and reduced bottom block height by 50px. |
| 20 | 2026-11-19 | Added smooth corner radius to trapezium top corners where they bend into sloped sides using SVG quadratic beziers. |
| 21 | 2026-11-19 | Added 8px corner radius to trapezium inner rectangular box and tilted it 30° towards the left. |
| 22 | 2026-11-19 | Created 8 additional identical angled rectangles (9 total) and adjusted them evenly inside the trapezium using an 82vw flex container. |

---

## 3. Active Test Environment
- **Target Device**: Android Phone (`Realme RMX3690`, ADB id `2905321030008921`).
- **Live Preview Server**: Python HTTP server on port `8089` bridged via `adb reverse tcp:8089 tcp:8089`.
- **Target File**: `3D_Showcase_Preview.html` mirrored to `/sdcard/Download/3D_Showcase_Preview.html`.
