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
- **Camera Perspective & Scene View Angle**:
  - Three.js camera: Angled view looking into the cherry blossom park from the bottom side (`camY = 30, camZ = 42, targetZ = 8`, `camera.up.set(0, 1, 0)`). Mobile FOV set to 44° (landscape 38°).
  - Overlay Track Network: Pure flat 2D screen overlay (`transform: none`, no `rotateX`), ensuring pixel-perfect screen-edge alignment without any distortion. Road width widened by +40% (outer $R_x=355, R_y=96$; inner island $r_x=145, r_y=22$; feeder width 105px) to comfortably accommodate a multi-character squad.
  - Chilbi Characters: Upright 2.5D side-view character rigs viewed from a standing bottom-side vantage point. 7 Red Chilbi members span the **full road width from border to border** (from inner curb $w=-30$ to outer curb $w=+30$) with zero large gap, moving in a staggered cascading sweep. All walk at the exact same forward road speed while maintaining independent waddling bounce, tilt, and limb animations. Compact scale (~0.75x) matches road width for dense, shoulder-to-shoulder group marching.
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
     - 9 identical vertical rectangular bus bays arranged in a single compact row centered in the trapezium (`display: flex; justify-content: center; gap: 12px;`).
     - Dimensions: `width: 32px; height: 58px`, corner radius `7px`, tilted 30° left (`transform: rotate(-30deg)`), fill `#626479`.
     - Spacing: Tight, uniform small gap (~6px perpendicular clearance between consecutive parallel white borders).
     - **Standard Slots (5 leftmost)**: `2px solid #ffffff` white border.
     - **VIP Slot (4th from right)**: `2px solid #ffd700` golden border with bold gold "VIP" label tilted with the slot.
     - **Unlock Slots (3 rightmost)**: `2px solid #2ecc71` green border with bold green `+` symbol centered inside.
     - Fitting: Contained in one single row inside the trapezium with generous clearance from top neck, bottom boundary, and side sloped edges.
3. **Unified Track & Roundabout Network (`.track-network-container`)**:
   - Layout Architecture: Merged into a unified SVG spanning the upper 40% of the screen (`viewBox="0 0 1000 400"`), eliminating sub-pixel CSS offsets and coordinate mismatches. Pure 2D vector overlay (no CSS `perspective` or `rotateX`).
   - Oval Roundabout Dimensions:
     - Outer oval radius: $R_x=345, R_y=88$.
     - Center island: Masked hollow hole ($C_x=500, C_y=185, r_x=160, r_y=35$) with inner curb (`#e1e6ec`, 5.5px).
   - Bottom Trapezium Highway Connection:
     - 200 viewBox units wide ($X=400$ to $X=600$), creating a wide road highway connection bridging the oval roundabout down into the trapezium.
   - Dual Feeder Roads from Device Sides:
     - Feeder roads connect directly and seamlessly from the device screen sides ($X=-15$ to $X=1015$), spanning edge-to-edge across the screen without gaps or open ends.
     - Top curb: continuous curve from top-left screen corner across the ring top to top-right screen corner.
     - Lower curbs: flow smoothly from screen corners around the roundabout and down to the trapezium connection at $X=400$ and $X=600$.
     - Road surface (`#7a7d90`) is one continuous solid polygon.

4. **Chilbi ("Candy Bean") Characters Walking System (`#chilbi-characters-layer`)**:
   - Two distinct candy bean characters based on the Unity RoundCharacterGenerator pipeline:
     - **Red Chilbi**: Coral Red candy finish (`url(#chilbi-grad-red-body)` & `url(#chilbi-grad-red-head)`).
     - **Cyan Chilbi**: Turquoise Cyan candy finish (`url(#chilbi-grad-cyan-body)` & `url(#chilbi-grad-cyan-head)`).
   - Character Visuals:
     - Rounded bean pill body (`24px x 28px`), glossy head dome (`r=11`) with 3D specular highlight ellipses.
     - Swinging stubby side arms (`width: 5px, height: 13px`), stepping feet (`6px x 11px`), and soft ground drop shadow.
   - **Path Trajectory**:
     - Red Chilbi starts at top-left screen corner ($X=-15, Y=37.5$) and walks along the left curved horn road.
     - Cyan Chilbi starts at top-right screen corner ($X=1015, Y=37.5$) and walks along the right curved horn road.
   - **Continuous Ring Orbit**:
     - At $t=5.0\text{s}$, both characters reach the roundabout ring junctions ($X=281.3, Y=154.25$ and $X=718.7, Y=154.25$).
     - They seamlessly transition onto the oval ring centerline ($C_x=500, C_y=185, R_x=252.5, R_y=61.5$) and circle counter-clockwise round and round continuously at $0.55\text{ rad/s}$ without colliding.
   - **Procedural Waddle & Heading**:
     - Tangent heading orientation: Dynamically calculates path velocity $(\Delta X, \Delta Y)$ to face forward along the curve (`rotate(heading)`).
     - 60 FPS waddle animation: Procedural vertical bounce ($\pm 2.8\text{px}$), body tilt ($\pm 6.5^\circ$), squish-and-stretch, alternating feet stepping ($\pm 4.0\text{px}$), and swinging side arms ($\pm 22^\circ$).

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
| 23 | 2026-11-19 | Reduced gap between the 9 angled rectangles in the single row to a compact 12px flex gap (~6px perpendicular spacing) centered inside the trapezium. |
| 24 | 2026-11-19 | Styled the 4th box from right with golden border (#ffd700) and tilted 'VIP' label, and the 3 rightmost boxes with green borders (#2ecc71) and green '+' symbols. |
| 25 | 2026-11-19 | Implemented animated Chilbi (bean) characters: Red Chilbi entering from left road and Cyan Chilbi from right road, merging onto roundabout ring and looping round and round. |
| 26 | 2026-11-19 | Converted scene camera to true direct top-down OrthographicCamera, eliminating perspective distortion and removing the angled tilt towards the bottom side. |
| 27 | 2026-11-19 | Adjusted camera to angled bottom-up perspective view and converted Chilbi characters to upright 2.5D beans with visible upright body, swinging side arms, stepping legs, and front-facing eyes when approaching the trapezium. |
| 28 | 2026-11-19 | Reverted Chilbi characters back to original candy bean models, and changed the view angle of the scene with 3D perspective (25° tilt from bottom side) and lowered Three.js camera angle. |
| 29 | 2026-11-19 | Reduced top-down view angle of the ring further to 38° tilt and lowered Three.js camera (camY=16, camZ=46), keeping original Chilbi path-facing heading. |
| 30 | 2026-11-19 | Unmerged ring from trapezium to make ring a distinct closed oval, restored feeder roads connecting edge-to-edge from device screen sides, and removed CSS perspective/rotateX. |
| 31 | 2026-11-19 | Restored clean original highway connection (200px wide) between ring and trapezium, edge-to-edge feeder roads from device sides, original Chilbi candy beans, and stable angled 3D camera. |
| 32 | 2026-11-20 | Set mobile camera field of view (FOV) to 90° for wider perspective view. |
| 33 | 2026-11-20 | Reverted mobile camera field of view (FOV) back to 44° (landscape 38°). |
| 34 | 2026-11-20 | Implemented Approach 1: applied CSS 3D perspective and rotateX(24deg) with bottom pivot (transform-origin: 50% 85%) on the track network container, providing an angled view looking at the ring and Chilbi from the bottom side without detaching feeder roads from device edges. Isolated Chilbi walk script. |
| 35 | 2026-11-20 | Reverted ground/track container to pure flat 2D (no rotateX/distortion) and implemented upright 2.5D side-view Chilbi characters. Viewed from the bottom side at a standing height, characters stand upright on their ground shadows, showing their side profile body, swinging side arms, and stepping legs along their path. |
| 36 | 2026-11-20 | Created 5 additional Red Chilbi (6 total in squad) walking together in a staggered V-formation; widened road width by +40% (outer Rx=355, Ry=96, inner rx=145, ry=22) and adjusted Chilbi scale (~0.75x) so all 6 walk together comfortably with generous curb clearance. |
| 37 | 2026-11-20 | Configured Chilbi to span the full road width from border to border with zero empty gap. Added 7th Red Chilbi (7 total), distributed evenly from inner curb (w = -30) to outer curb (w = +30) in a staggered cascading sweep walking at the same forward speed with independent waddling animations. |
| 38 | 2026-11-20 | Replaced abrupt straight turns / flips with continuous circular walk following the ring path tangent. Red squad (7 members) and Cyan Chilbi now smoothly rotate with `rotate(heading)` along concentric elliptical lanes ($R_{xi} = 145 + \alpha_i \times 210, R_{yi} = 22 + \alpha_i \times 74$), curving naturally 360° around the ring while spanning the entire road width from inner curb border to outer curb border. |
| 39 | 2026-11-20 | Calibrated ring width ($R_x=320, R_y=82$, inner $r_x=180, r_y=42$, width 140px/40px) and synchronized angular velocity so the 7 Red Chilbi form a gapless, seamless line spanning border to border. Position-based linear speed scaling enables outer members to travel with extra speed ($v_{out} \approx 1.7 \times v_{in}$) and take faster/larger strides, so all members stay perfectly together in lockstep without lagging. |
| 40 | 2026-11-20 | Fixed corner squeezing and non-uniform speed surges: 1) Made road width completely even and uniform at 70px across the entire ring ($R_x=270, R_y=105$, inner $r_x=200, r_y=35$); 2) Implemented 100% constant linear speed (85 px/s) using arc-length look-up parameterization, eliminating corner running; 3) Assigned constant normal offsets ($d \in [-27, +27]$ px, gap = 9.0px) so the squad never squeezes on bends and spans the road gaplessly everywhere. |
| 41 | 2026-11-20 | Replaced sharp elliptical ends with a true Stadium Race Track with uniform 70px road width throughout ($R_{out}=105$px, $R_{in}=35$px, $W=70$px): 1) Drew 6 crisp circular race-track lane divider lines (dashed white lines, stroke-dasharray "10 8") dividing the road into 7 distinct lanes ($r \in [45, 55, 65, 75, 85, 95]$px); 2) Positioned all 7 Red Chilbis directly down the center of each marked lane ($r_k \in [40, 50, 60, 70, 80, 90, 100]$px) with exact 10.0px spacing, spanning border to border with zero gap; 3) Synchronized linear speed on straights ($V = 85$px/s) and angular velocity on semicircles ($\omega = V/70$) with calm 8.0 step/s waddling, ensuring gentle, round circular turns with no running, squeezing, or sharp corners. |
| 42 | 2026-11-20 | Fixed non-uniform track width and unequal lane gaps between sides and straightaways: 1) Eliminated horizontal aspect ratio stretching by calibrating SVG coordinate system to 1:1 pixel scale ($s_x = s_y = 1.0$), ensuring circles are 100% round with zero distortion; 2) Enclosed both left and right circular turns with full, continuous, symmetrical outer stadium curbs ($R_{out}=91.5$px, $R_{in}=32.0$px, $W_{road}=59.5$px everywhere); 3) Set every track lane and gap between lines to strictly $8.5$px everywhere (top: 8.5px, bottom: 8.5px, left side: 8.5px, right side: 8.5px); 4) Re-centered the 7 Red Chilbis at $r_k \in [36.25, 44.75, 53.25, 61.75, 70.25, 78.75, 87.25]$px, maintaining exact 8.5px spacing and seamless squad alignment from inner curb to outer curb. |
| 43 | 2026-11-20 | Restored the unified, harmonious full-screen composition and fixed the messed-up view: 1) Re-anchored the road network inside `viewBox="0 0 1000 400" preserveAspectRatio="none"` with a 220-unit wide highway neck `[390, 610]` merging directly into the top of the grey trapezium parking bays at $y=320$ (eliminating the disconnected floating ring, pink grass gap, and cut-off stubs); 2) Restored the graceful, sweeping cow-horn feeder roads from screen corners `(-15, -15)` and `(1015, -15)` connecting into the roundabout; 3) Configured the ring with balanced, uniform 50px road width on screen ($R_x=222, R_y=95$ outer, $r_x=180, r_y=25$ inner) and 6 concentric dashed white race-track lines with equal 7.1px lane widths everywhere; 4) Synchronized the 7 Red Chilbis along concentric paths with constant-linear-speed LUT, keeping them in a straight radial row with zero gap and no squeezing on turns. |
| 44 | 2026-11-20 | Implemented exact racing circuit shape and connecting roads from user reference image (`media_1795143434734_d7415e16.jpg`): 1) Created Catmull-Rom closed circuit spline featuring distinctive top chicane dip ($x=480, y=125$) between two rounded crests ($x=360, y=95$ and $x=605, y=80$), rounded left apex ($x=280, y=170$), wide right apex ($x=705, y=210$), and gentle bottom straightaway ($y=275$); 2) Added 4 alternating red-and-white racing kerbs (`#ffffff` and `#e74c3c`, 8px dashes) along the inner apexes (inside chicane dip, top-left crest, bottom-left turn, and bottom-right turn); 3) Built sweeping connecting roads on both sides matching reference image: left road sweeps from top-left `(35, -15)` to merge into left turn, right road sweeps from right crest to top-right `(965, -15)`, both with center dashed lane divider lines; 4) Seamlessly blended the road surface (`#7a7d90`) and curbs (`#e1e6ec`) across all junctions and the bottom highway opening `[390, 610]` into the trapezium parking bays; 5) Calibrated 6 concentric dashed lane lines (7 equal 8.57px lanes) and synchronized the 7 Red Chilbis abreast formation to walk together with zero gap and calm waddling. |
| 45 | 2026-11-20 | Restored Step 35 upright 2.5D side-view character viewpoint for the squad of 7 Red Chilbis and Cyan Chilbi: 1) Replaced top-down rotating candy bean models with Step 35 upright 2.5D side-view character rigs featuring standing posture on ground shadows, upright capsule torso, head dome, profile eye facing horizontal walk direction, near & far swinging arms, and near & far stepping feet; 2) Updated animation to maintain upright vertical orientation using `scale(facingX * depth, depth)` with horizontal direction `dx >= 0 ? 1 : -1`, eliminating flat spinning along curves; 3) Synchronized near/far limb strides, arm swings, and body waddling while maintaining abreast formation across the 7 lanes of the reference racing circuit. |
| 46 | 2026-11-20 | Widened the entire circuit horizontally and routed connecting roads out to the screen sides: 1) Expanded the circuit loop horizontally by +35% (span widened from 425 to 591 units across x = 203 to 794), eliminating the squeezed appearance and opening up the center island; 2) Widened the road width to 70px with 6 concentric dashed lines dividing the track into 7 equal 10.0px lanes; 3) Re-routed both connecting roads to exit to the screen sides instead of going up: left road curves out to left edge (-15, 90) and right road curves out to right edge (1015, 90), both with central dashed divider lines; 4) Re-calibrated the 7 Red Chilbi lanes at d in [-30, +30] (10.0px spacing) in Step 35 upright 2.5D side view. |
| 47 | 2026-11-20 | Calibrated isotropic screen-space normal offsets to ensure 100% mathematically uniform road width (56.0px) across the entire path: 1) Eliminated non-uniform aspect ratio distortion ($s_x / s_y = 1.5$) where turns previously appeared 1.5x wider than straights by projecting tangent vectors to screen coordinates $(s_x dx, s_y dy)$, deriving the true screen-space perpendicular unit normal $(-dY/L, dX/L)$, and scaling SVG offsets accordingly; 2) Enforced strictly constant 56.0px road width ($D_{screen} = 28.0$px) across all 420 samples of the Catmull-Rom spline with zero deviation across turns, chicane dip, crests, and straights; 3) Scaled the 6 concentric dashed lane lines to equal 8.0px lanes ($D_k \in [-20, +20]$ screen px) and synchronized the 7 Red Chilbis squad with exact 8.0px spacing; 4) Fixed side connecting road junction curvature so the left and right hairpin turns remain pure, smooth curves with no bulging or pinch points. |
| 48 | 2026-11-20 | Complete architectural overhaul of the racing circuit for 100% true mathematical road width uniformity: 1) Replaced fragile manual polygon offsets with layered isotropic SVG path strokes (`viewBox="0 0 1200 320"` with `preserveAspectRatio="xMidYMid meet"`), ensuring 100% mathematically uniform road width (60.0px) along every turn, hairpin, crest, chicane dip, and straightway; 2) Added uniform 82px tan dirt shoulder (`#cbb27a`), 64px crisp white edge curbs (`#e6e9ee`), and crisp dashed center divider lines; 3) Formed a spacious, rounded inner grass island matching the reference image (`media_1795143434734_d7415e16.jpg`) with a gentle chicane dip and zero needle spikes; 4) Added 5 alternating red & white racing kerbs (`#ffffff` and `#e74c3c`) along the inner apexes; 5) Routed side connecting roads smoothly to the screen sides with merge stop lines; 6) Fixed JavaScript `pts` definition bug; restored all 7 Red Chilbis walking in abreast formation with constant speed and upright 2.5D side-view posture, plus Cyan Chilbi entering from the feeder road. |
| 49 | 2026-11-20 | Increased width of ring road (+37% wider): 1) Scaled asphalt road width from 60.0px to 82.0px across the entire circuit path and connecting roads; 2) Expanded white edge curbs from 64.0px to 86.0px (constant 2px border on inner and outer edges) and tan dirt shoulder from 82.0px to 104.0px; 3) Re-anchored the 5 red & white racing kerbs to the new inner apex boundary ($d = -38.0$px); 4) Raised the chicane dip and crests by 8-10px to maintain a spacious, open central lawn inside the wider track; 5) Re-spaced the 7 Red Chilbi abreast formation across the 82px road ($d \in [-35.1, +35.1]$, gap = 11.71px), giving the squad generous spacing and clean separation while marching at steady speed. |
| 50 | 2026-11-20 | Zoomed in the entire road network and scaled up the Chilbis for an up-close, prominent view: 1) Expanded the circuit horizontally by +35% (span widened from 710 to 960 units across x = 120 to 1080), bringing the racing track up-close and filling the upper screen; 2) Increased road width to 92.0px (curbs 96.0px, dirt shoulder 116.0px) with 100% constant uniform thickness throughout; 3) Scaled up the Chilbi characters by +70% (`chilbiBaseScale = 1.70`), making their upright 2.5D side-view posture, glossy head, profile eye, swinging arms, and stepping feet clearly visible rather than looking far away; 4) Re-spaced the 7 Red Chilbis abreast formation to 13.14px gaps ($d \in [-39.4, +39.4]$) across the 92px road; 5) Adjusted vertical framing with clear separation above the parking bays. |
| 51 | 2026-11-20 | Built seamless highway connection between ring road and trapezium: 1) Extended a wide (300px) flaring asphalt highway neck (`#525769`) connecting the bottom straight of the ring road directly down into the top edge of the grey trapezium parking lot; 2) Matched the trapezium fill color to the circuit asphalt (`#525769`), eliminating color clashes and gaps; 3) Routed curved white side curbs (`#e6e9ee`) and flaring dirt shoulders along the neck edges; 4) Maintained continuous dashed centerline markings across the neck opening so the 7 Red Chilbi squad continues walking their loop uninterrupted. |
| 52 | 2026-11-20 | Full mobile-first calibration and verification for mobile device portrait viewport: 1) Re-engineered track container to meet trapezium at exact line (`height: calc(30% + 2px)` meeting trapezium top edge at 30% of screen height), eliminating coordinate mismatches, overlapping cuts, and gaps; 2) Built mathematically exact seamless asphalt neck (`#525769`) flaring from bottom straight into the trapezium flat top edge `[90, 410]` at $Y = 320$; 3) Routed curved white edge curbs (`#e6e9ee`) and tan dirt shoulders along the outer neck borders without cutting into the road or parking bays; 4) Calibrated 9 angled parking bays inside trapezium (`25px x 50px`, gap 8px) with generous margins on 360-390px mobile viewports (zero overflow); 5) Zoomed in the circuit loop with bold 72px road width, 76px curbs, 98px dirt shoulders, chicane dip, and 5 red & white inner apex kerbs; 6) Scaled up Chilbis (`1.38x`) in Step 35 upright 2.5D side-view formation with constant linear speed LUT; 7) Adjusted watermark pill to compact header (`top: 3px`, font 8.5px) leaving 22px breathing room above the track. |
| 53 | 2026-11-20 | Synchronized JavaScript walker control points with SVG path geometry to keep Chilbis strictly on road: 1) Diagnosed root cause of Chilbis walking off-road (SVG path crests and bottom straight had been lowered to $Y \in [80, 236]$ for mobile vertical balancing, while JavaScript `pts` inside `initChilbiWalkers()` retained old $Y \in [60, 218]$ coordinates, causing up to 18px vertical tracking mismatch); 2) Updated JavaScript `pts` to 100% identical 16 control points matching SVG Catmull-Rom spline; 3) Re-calculated 829.3px arc-length table with constant 46.0px/s linear speed (18.0s per lap); 4) Verified across 5 time offsets via Chrome CDP screenshots that all 7 Red Chilbis remain strictly on the 72px asphalt road with 8px curb margin on every turn, crest, chicane dip, and straightaway. |
| 54 | 2026-11-20 | Implemented high-speed entrance sprint from connecting road with multi-kid cheering audio and smooth deceleration into ring road walking loop: 1) Extended left connecting road offscreen to $X = -50$ and built continuous arc-length parameterization seamlessly joining the feeder road into the ring circuit at $X = 88, Y = 134$; 2) Implemented two-phase kinematic flow: fast sprint ($150\text{ px/s}$) down the feeder road with energetic $19.5\text{ Hz}$ rapid leg pumping, forward lean, and running arm pumps ($42^\circ$), smoothly decelerating over $t \in [0.9\text{s}, 2.3\text{s}]$ at the merge junction down to cruising walk speed ($46.0\text{ px/s}$), then fanning out from an arrow wedge into 7-abreast lanes looping around the ring road; 3) Designated Cyan Chilbi as lead runner in the center lane $14\text{px}$ ahead of the squad; 4) Synthesized and embedded 12-voice kids cheer chorus ("Hey! Goooo! Let's gooo! Go go go!") encoded directly as base64 MP3 with Web Audio playback and gesture unlocking; 5) Added glassmorphic top-right "Replay Cheer & Run" button and responsive top-left watermark pill. |

---

## 3. Active Test Environment
- **Target Device**: Android Phone (`Realme RMX3690`, ADB id `2905321030008921`).
- **Live Preview Server**: Python HTTP server on port `8089` bridged via `adb reverse tcp:8089 tcp:8089`.
- **Target File**: `3D_Showcase_Preview.html` mirrored to `/sdcard/Download/3D_Showcase_Preview.html`.
