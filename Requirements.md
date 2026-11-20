# Roadways Bus Game: Product Requirements Document (`Requirements.md`)

## 1. Executive Summary & Product Vision

**Roadways Bus Game** is a mobile arcade puzzle sorting game engineered for Android devices. The game combines the visual charm of an elevated cherry blossom zen garden with the cognitive thrill of traffic jam sorting mechanics:
- An endless parade of **430 candy bean characters ("Chilbis")** enters a circuit track in color-coded rows.
- Below the circuit, a depot holds **color-matched buses**.
- Players tap buses to dispatch them into **angled parking bays** in a trapezium terminal.
- Matching color characters peel off the circuit and board their bus.
- Full buses depart, freeing the bay. If all bays fill with mismatched vehicles, the player reaches **gridlock**.

---

## 2. Functional Requirements (FR)

### FR-1: 3D Environment & Static Camera System
- **FR-1.1**: The environment must present a minimalist, pastel **Cherry Blossom Park** theme (`#fff1f2` background).
- **FR-1.2**: Ground plane must feature layered, subtle concentric circular lawn decals (`#f2d5db`, alpha `0.35`) and rounded zen garden stone accents around the lawn perimeter.
- **FR-1.3 (Negative Constraint)**: Strictly **zero trees**, **zero falling leaf particles**, and **zero obstacles/characters/vehicles** on the grass lawn.
- **FR-1.4**: Camera position is fixed at `(0, 30, 42)` looking at Target `(0, 0, 8)` with pitch $\approx 35^\circ$ and up vector `(0, 1, 0)`.
- **FR-1.5**: Camera must remain **100% static** (no user dragging, orbit rotation, panning, or idle bobbing).
- **FR-1.6**: Camera vertical Field of View must dynamically adapt to tall mobile screens (`9:16`, `19.5:9`, `20:9`) maintaining consistent vertical framing of the circuit and depot.

### FR-2: Procedural Track & Roadway Network
- **FR-2.1**: The circuit must implement the exact 16-point Catmull-Rom spline with distinctive top chicane dip $(480, 125)$ between rounded crests $(360, 95)$ and $(605, 80)$, left hairpin apex $(203, 170)$, right apex $(794, 210)$, and bottom straightaway ($Y = 236$).
- **FR-2.2**: Road asphalt surface must be mathematically uniform at **82 units** width (`#525769`).
- **FR-2.3**: Road edges must feature **2-unit white curbs** (`#e6e9ee`) flanked by **11-unit tan dirt shoulders** (`#cbb27a`).
- **FR-2.4**: Exactly **5 alternating red & white apex racing kerbs** (`#ffffff` and `#e74c3c`, 8-unit dashes) must line the inner turn apexes.
- **FR-2.5**: Dual cow-horn feeder roads must connect seamlessly from screen edges ($X = -50$ and $X = 1015$) into the circuit.
- **FR-2.6**: Feeder road must feature a distinct white stop line bar at $X = 30$ ($25\text{px}$ clearance from outer ring curb).
- **FR-2.7**: A flared asphalt highway neck (220 units wide) must bridge the bottom straight of the ring directly down into the top edge of the trapezium parking area.

### FR-3: 430 Chilbi Character Formation & Animation
- **FR-3.1**: Squad must consist of exactly **430 Chilbi characters**.
- **FR-3.2**: Characters must be arranged in **7-abreast rows across 62 rows** (61 rows of 7 + 1 row of 3).
- **FR-3.3**: Strict **Single-Color Rows**: Every row must consist of characters of a single uniform color.
- **FR-3.4**: Rows must cycle in repeating color blocks:
  $$\text{5 Red} \to \text{7 Yellow} \to \text{4 Blue} \to \text{8 Green} \to \text{6 Purple} \to \text{5 Orange} \to \text{6 Pink} \to \text{5 Cyan}$$
- **FR-3.5**: 2.5D character silhouette: seamless candy bean body combining head dome and torso into one continuous silhouette with zero neck gap.
- **FR-3.6**: Procedural bipedal stepping legs ($Y = -6$) and curved arms ($Y = -17$) with rounded elbow joints swinging synchronously.
- **FR-3.7**: Upright posture: character faces horizontal direction of travel (`facingX = dx >= 0 ? 1 : -1`) while remaining vertically upright on ground shadow.
- **FR-3.8**: Two-phase arrival: High-speed entrance sprint ($150\text{ px/s}$) with $19.5\text{ Hz}$ rapid leg pumping, decelerating over 1.4s at junction into cruising loop ($46\text{ px/s}$).
- **FR-3.9**: Ring road circuit capacity must lock at exactly **60 rows ($420$ Chilbis)** with uniform $13.82\text{px}$ row spacing and zero visible loop seam.
- **FR-3.10**: Remaining 10 Chilbis (Rows 60 & 61) must halt behind feeder stop bar ($X = 30$) with idle breathing waddle.

### FR-4: Master Color Palette & 1:1 Parity Condition
- **FR-4.1**: Strict condition: **Whatever color exists for a Chilbi walker must have an identical corresponding bus of the exact same solid primary hex color**.
- **FR-4.2**: Exactly 8 master color themes:
  | ID | Theme Name | Primary Hex | Limb Accent | Horn Freq |
  | :--- | :--- | :--- | :--- | :--- |
  | `red` | Crimson Express | `#c0392b` | `#a82c1f` | 440 Hz |
  | `yellow` | Amber School Buggy | `#f39c12` | `#d4880f` | 392 Hz |
  | `blue` | Cobalt Commuter | `#2548ad` | `#1c3987` | 494 Hz |
  | `green` | Emerald Eco | `#20bf6b` | `#168d4f` | 523 Hz |
  | `purple` | Royal Violet | `#8854d0` | `#6c3db5` | 587 Hz |
  | `orange` | Tangerine Transit | `#e67e22` | `#cf6717` | 622 Hz |
  | `pink` | Bubblegum Shuttle | `#e84393` | `#cf2d7c` | 659 Hz |
  | `cyan` | Cyan Deck-Master | `#0fbcf9` | `#0998cc` | 740 Hz |
- **FR-4.3**: Matte finish: Zero specular shine, zero glossy reflections, and zero eyes on characters; zero drop shadows or roof gloss on buses.

### FR-5: Bus Fleet Depot & Vehicle Models
- **FR-5.1**: Depot must display all 8 matching buses in a single horizontal row below the trapezium.
- **FR-5.2**: Vehicle dimensions must be exactly **$25\text{px} \times 50\text{px}$** with $5.5\text{px}$ corner radius.
- **FR-5.3**: Vehicles must be rendered in pure top-down aerial perspective showing front visor, rear window, side windows, and solid matte roof.
- **FR-5.4**: Each vehicle roof must feature a forward-facing crisp white navigation arrow with rounded corners (`stroke-linejoin="round"`).
- **FR-5.5**: Layout must use responsive horizontal spacing (`clamp(10px, 2.5vw, 16px)`) fitting all mobile screens without wrapping.

### FR-6: Trapezium Parking Terminal
- **FR-6.1**: Trapezium terminal panel sits directly above the depot, colored slate grey (`#525769`) with rounded top corners.
- **FR-6.2**: Terminal must house **9 identical rectangular parking bays** tilted at **`-30°`**:
  - **Slots 1–5 (left)**: Standard Bays (`2px` solid white border `#ffffff`).
  - **Slot 6 (4th from right)**: VIP Bay (`2px` solid gold border `#ffd700` with gold "VIP" text).
  - **Slots 7–9 (right)**: Unlock Bays (`2px` solid green border `#2ecc71` with bold `+` icon).
- **FR-6.3**: Bay dimensions: exactly $25\text{px} \times 50\text{px}$, corner radius $6\text{px}$, spacing gap $8\text{px}$.

### FR-7: Vehicular Driving Kinematics & Head-First Bay Entry
- **FR-7.1**: When a bus is clicked:
  - If destination bay is occupied or vehicle is in transit: reject move with horizontal wiggle shake ($0.35\text{s}$) and warning double buzz.
  - If path is clear: dispatch vehicle.
- **FR-7.2**: Driving motion must use a **Cubic Hermite Spline** where vehicle heading $\theta(t) = \operatorname{atan2}(v_x, -v_y)$ continuously follows the velocity tangent vector (no sliding, no crab-walking, no being dragged).
- **FR-7.3**: 3-Phase Driving Maneuver:
  - **Phase 1 (Roadway Drive)**: Accelerate forward out of depot in arrow direction ($0^\circ$), then steer smoothly across roadway to bay approach mouth at $(145.5, 368.4)$.
  - **Phase 2 (Head-First Bay Entry)**: Align nose to **`-30.0°`** and pull straight forward inside the parking bay along its $-30^\circ$ centerline.
  - **Phase 3 (Braking & Settle)**: Deceleration braking dip (`scale(1.02, 0.975)`), rebound settle to $(1.0, 1.0)$, and pneumatic air-brake audio ("pssshh-clack!").
- **FR-7.4**: Reverse back-out: Tapping parked bus reverses it straight out of the bay along $-30^\circ$ axis before returning to depot slot.

### FR-8: Roof Arrow Disappearance
- **FR-8.1**: Roof arrow must remain clearly visible throughout the entire transit and driving phase.
- **FR-8.2**: The exact microsecond the vehicle docks into the parking bay, the roof arrow must smoothly fade out and scale down to $0$ opacity over $0.28\text{s}$.
- **FR-8.3**: If the parked vehicle is tapped to return to depot, the arrow must smoothly fade back in.

### FR-9: Passenger Boarding & Capacity Counter
- **FR-9.1**: When a bus is parked in Bay 0, system checks if its color matches the active front row on the circuit.
- **FR-9.2**: If colors match: the 7 Chilbis in the front row peel off the circuit, walk down the highway neck, and hop into the bus with cheerful pop sounds.
- **FR-9.3**: Each bus holds exactly **7 passengers** (1 full row).
- **FR-9.4**: Display floating seat counter over bus (`0/7` $\to$ `7/7`).

### FR-10: Bus Departure & Queue Advancement
- **FR-10.1**: When bus reaches capacity (7/7): doors seal, departure horn sounds, and bus drives forward along $-30^\circ$ axis out of bay to exit screen top.
- **FR-10.2**: Parking bay resets to vacant.
- **FR-10.3**: Circuit rows advance forward by 1 row gap ($13.82\text{px}$); 1 row from feeder queue enters circuit.

### FR-11: Win & Gridlock Game Over Rules
- **FR-11.1 (Win Condition)**: All 430 Chilbis successfully boarded and all 62 rows cleared. Trigger victory fanfare and confetti.
- **FR-11.2 (Gridlock Condition)**: All active parking bays are occupied, and none match the color of the front waiting circuit row. Trigger Game Over modal.

### FR-12: Audio & Native Android Haptics
- **FR-12.1**: Synthesize/play 12-voice kids cheer chorus, 8 harmonic dual-tone bus horns, pneumatic air-brake hiss, engine acceleration rumble, and boarding pops.
- **FR-12.2**: Integrate Android native haptic vibration:
  - Bus selection: $15\text{ms}$ light tick.
  - Path blocked: $40\text{ms} + 60\text{ms}$ double warning rumble.
  - Bay docking: $25\text{ms}$ firm click.
  - Passenger boarding: $10\text{ms}$ soft pop.

---

## 3. Non-Functional Requirements (NFR)

### NFR-1: Mobile Performance & Frame Rate
- **Target Frame Rate**: Locked **60 FPS** (16.6ms frame time) on mid-range Android devices (`Realme RMX3690`, Snapdragon 680 / Helio G35 or equivalent).
- **GPU Draw Calls**: Less than **50 draw calls** per frame across entire scene using GPU Instancing (`Graphics.RenderMeshInstanced`).
- **Memory Footprint**: RAM usage must not exceed **120 MB**.
- **Garbage Collection (GC)**: Zero per-frame memory allocation ($0\text{ B}$ GC Alloc in `Update()`) to prevent GC pause stutters.

### NFR-2: Target OS & Platform Compatibility
- **Minimum OS Version**: Android 8.0 Oreo (API Level 26).
- **Target OS Version**: Android 14 (API Level 34+).
- **Architecture**: 64-bit ARM64 (mandatory for Google Play Console) with ARMv7 32-bit fallback.
- **Graphics API**: Vulkan (Primary) with OpenGL ES 3.2 fallback.
- **Texture Compression**: ASTC (Adaptive Scalable Texture Compression).

### NFR-3: Thermal & Battery Stability
- Game must run continuously for **15+ minutes** without causing device overheating or CPU/GPU thermal throttling.
- Background tasks and walker kinematics must execute within $< 1.5\text{ms}$ CPU time per frame using C# Jobs and Burst Compiler.

### NFR-4: Screen Adaptation & Safe Area
- Responsive scaling across all Android mobile screen aspect ratios (`16:9`, `18:9`, `19.5:9`, `20:9`).
- Notch and cutout handling: Game canvas elements and top watermark must respect Android `DisplayCutout` safe areas.
