# Visual & Structural Target — derived from the *Bus Fever Party!* reference screenshot

**Created:** 2026-11-21
**Source:** one gameplay screenshot of *Bus Fever Party!* (Lumi Games), Level 28, supplied by the project owner
**Supersedes:** §J.0 of [`UI_UX_AUDIT_2026-11-21.md`](./UI_UX_AUDIT_2026-11-21.md), which recorded that I had **no** visual evidence for this game
**Status:** reference analysis + redirection. **Both decisions in §6 are now resolved** — unblock mechanic **adopted**, theme **switched to beach**. Planned in [`FIX_ROADMAP_PHASE13-20.md`](./FIX_ROADMAP_PHASE13-20.md) (now ≈ 46 developer-days).

---

## 1. What the screenshot actually shows

Describing only what is visible. Measurements are proportions of a ~720 × 1600 frame, ±3 %.

### 1.1 Screen budget

| Band | Share | Content |
| :--- | :--- | :--- |
| Top HUD | ~9 % | Three elements only, floating over the scene |
| **Queue zone** | **~21 %** | Horseshoe track packed with characters, on a beach backdrop |
| **Bay apron** | **~14 %** | Grey apron, 7 slots |
| **Depot puzzle** | **~44 %** | ~30 vehicles, the actual puzzle |
| Booster bar | ~9 % | Three large buttons |
| | **~97 %** | **Functional. Almost nothing is empty.** |

Compare Roadways as measured in audit §D: top sliver 12.5 %, circuit 27.5 %, bays 10 %, depot content ~10 %, and **~40 % flat empty grey**.

> The reference gives **44 % of the screen to the puzzle**. Roadways gives **10 % to its depot and 40 % to nothing.**

### 1.2 Camera

Perspective, looking down roughly 50–60°, not top-down and not flat. Vehicles show their tops *and* their sides; the horseshoe track recedes with visible foreshortening; the apron is a trapezoid because of the perspective, not because it is drawn as one. Every object has a soft contact shadow directly beneath it.

Roadways currently renders a `ScreenSpaceOverlay` canvas with no camera, no perspective and no shadows. (The 3D rig that exists is specified in `GameConstants` at `(0,30,42)` → `(0,0,8)`, ~35° pitch — **shallower than the reference**, see §4.2.)

### 1.3 The queue — the single most important structural difference

- A **horseshoe / U-shaped band**, open at the bottom, with a hole in the middle showing the background through it.
- Characters packed **4–5 abreast**, shoulder to shoulder, overlapping — a dense crowd, not a thin line.
- Solid **colour blocks** of roughly 8–16 characters each. Visible left→right: blue, red, teal, blue, red, yellow / green, pink, purple, yellow.
- **Two exit gates** — notches in the inner wall at the bottom centre — feeding down onto the apron.
- A light grey barrier wall with a subtle 3D lip encloses the band on both edges.
- Characters are simple rounded capsules with a slight head taper, softly shaded (lighter crown, darker base). No faces readable at this size.

**Why this matters more than anything else in the screenshot:** the audit's §B-3 finding was that Roadways' "front row" is the row nearest an unmarked arc-length `s = 0` on a closed loop, flipping every ~0.2 s, with nothing on screen indicating it. The reference solves that problem **with geometry instead of UI**. The queue is a horseshoe with visible exits, so "the front" is self-evident from the shape — it is where the crowd drains out. No marker, no next-up strip, no tutorial text required.

That is a **simpler and better answer than the one I proposed** in roadmap Phase 17 (a world-space boarding gate plus a screen-space colour strip). Adopt the shape; delete my invention.

### 1.4 The bay apron

- **7 slots**, not 9.
- Tilted only **~10–15°**, not the −30° in `GameConstants.ParkingBayAngleDeg`. Shallow tilt reads as "angled parking" while keeping the slots wide and legible; −30° makes them narrow lozenges.
- Left-to-right: **1 VIP** (gold border, "VIP" set vertically) → **4 standard** (grey fill, light border) → **3 locked** (green tint, "+").
- Slots are **empty outlines** — unmistakably destinations. Generous size relative to the vehicles.

Roadways has the same 1 VIP / standard / 3 unlock structure, so this part is already right in concept — it is the tilt, the count and the scale that differ.

### 1.5 The depot — this is the puzzle

~30 vehicles of **varying lengths** (short cars, long buses), scattered at **many different angles**, each carrying a large white **directional arrow** on its roof.

That arrow is the mechanic: a vehicle can only leave if its path *in the arrow's direction* is clear. The player's skill is reading the jam — which vehicle is unblocked, and what order to clear them in. This is the Rush Hour / Parking Jam lineage.

**Roadways does not have this mechanic.** Its depot is 8 identical buses in a tidy row (4×2 tandem at L3+), and `GetFirstAvailableBay()` auto-assigns the destination, so the only decision the player makes is "which colour matches". The audit flagged this twice (§C-3, §C-4) as the loop having almost no decisions in it. **See §6, Decision A.**

### 1.6 HUD — three elements, total

| Position | Element |
| :--- | :--- |
| Top-left | Circular blue button, undo/restart arrow |
| Top-centre | **"Level 28"** — white bold, dark outline, **no container, no pill, no panel** |
| Top-right | Circular blue button, pause |

No score. No high score. No counters. No status line. Buttons are large circles (~56 dp equivalent) with a light top bevel and a soft drop shadow.

Roadways currently shows two stacked pills carrying four metrics at ~7 sp, with the level printed **twice** (audit §B-7).

### 1.7 Booster bar

Three large rounded-square buttons across the bottom centre: **VIP**, **Arrange**, **Jumble**. Each has a chunky 3D vehicle icon, a white label with a dark outline, and a circular count badge at top-right (green `+` for VIP and Arrange, red `1` for Jumble).

Roadways has two text-and-emoji boosters at the *top* of the depot panel, 290 × 54 units = 97 × **18 dp** tall (audit §B-4).

### 1.8 Colour and rendering

- Background is a **saturated beach**: turquoise sea, warm sand, and real props — umbrella, towels, surfboard, ball, cooler, palm fronds.
- Vehicle palette is high-chroma and high-value: red, pink, blue, purple, green, teal, yellow, orange — comparable in hue to Roadways' set, but brighter.
- Every vehicle has visible shading: a lit top face, darker sides, a specular edge, a darker window band, and a soft contact shadow.
- **Chroma coverage is very high** — the crowd, ~30 vehicles and the beach fill nearly the whole frame. Roadways measures at **≈ 6 %** (audit §J.1).

### 1.9 One thing that is *not* part of the game

The orange circular icon on the right edge is an Android accessibility-service overlay, not a game element.

---

## 2. The honest gap table

| Dimension | Reference | Roadways today | Gap |
| :--- | :--- | :--- | :--- |
| Rendering | Perspective 3D, lit, shadowed | Flat 2D overlay canvas, unlit, no shadows | **Large** — Phase 18 already targets this |
| Functional screen use | ~97 % | ~60 %, with 40 % empty grey | **Large** |
| Chroma coverage | Very high | ≈ 6 % | **Large** |
| Queue legibility | Horseshoe with visible exits — self-evident | Closed loop, invisible trigger point | **Large, and now cheaply solvable** |
| Crowd density | 4–5 abreast, packed, overlapping | 7 abreast, thin single-file band | **Medium** |
| Depot | ~30 vehicles, varied length/angle, arrow-based unblock puzzle | 8 identical buses in a row, auto-assigned bays | **Core-loop difference** — §6 Decision A |
| Bays | 7, shallow ~12° tilt, large | 9, −30° tilt, narrow | **Small** |
| HUD | 3 elements, no containers | 2 pills, 4 metrics, level duplicated | **Small, already planned** |
| Boosters | 3, bottom centre, ~70 dp, 3D icons | 2, top of panel, 18 dp tall, emoji | **Small, already planned** |
| Theme | Beach | Cherry blossom park | **Deliberate — see §6 Decision B** |

---

## 3. What this changes in the roadmap

Mostly good news: the reference **simplifies** two phases and **sharpens** three.

| Phase | Change |
| :--- | :--- |
| **13** Stabilise | No change. |
| **14** Extract game core | No change — still the keystone, and now *more* valuable, because a core-loop change (§6 Decision A) is far cheaper behind a view interface than welded into one. |
| **15** 3D playfield parity | **Grows** if Decision A is yes: the depot becomes a jam generator + path-clearance solver rather than a static row. Add ~4 d. |
| **16** Camera framing | **Sharpened.** Concrete target: ~50–60° pitch, and a 21 / 14 / 44 band budget. The current `GameConstants` rig at ~35° is too shallow to read like the reference. |
| **17** Readable puzzle | **Simplified.** Replace my "world-space gate + next-up strip" with the horseshoe-with-exit-gates. Less work, better result. Save ~2 d. The HUD scale/ergonomics half of the phase is unchanged. |
| **18** Lighting & HD | **Sharpened.** Now has a reference to grade against: lit top faces, darker sides, specular edges, soft contact shadows, saturated backdrop. |
| **19** Performance | **Harder.** A packed 4–5-abreast crowd *plus* ~30 scattered vehicles is more on screen than the current design. The GPU-instancing and Burst work in 19.2–19.3 moves from "important" to "mandatory". |
| **20** Identity & stakes | **Sharpened.** HUD → 3 elements. Boosters → 3 large bottom-centre buttons with 3D icons and count badges. Bays → 7 at ~12°. |

---

## 4. Concrete spec changes

### 4.1 Band budget (replaces audit §D targets)

```
HUD overlay        ~9 %   floating, no panel
Queue zone         ~21 %  horseshoe + backdrop
Bay apron          ~14 %  7 slots
Depot puzzle       ~44 %  the jam
Booster bar        ~9 %   3 buttons
Empty / decorative ≤ 5 %
```

### 4.2 Constants to revise in `GameConstants.cs`

| Constant | Current | Proposed | Why |
| :--- | :--- | :--- | :--- |
| `CameraPitch` | `35.0f` | **`52.0f`** | 35° is too shallow to show vehicle tops *and* sides the way the reference does |
| `CameraPosition` | `(0, 30, 42)` | re-derive from the framing requirement | Must fit all three bands at the 4.1 budget across 16:9 → 20:9 |
| `ParkingBayAngleDeg` | `-30.0f` | **`-12.0f`** | Shallow tilt keeps slots wide and legible |
| `TotalParkingBays` | `9` | **`7`** | 1 VIP + 3 standard + 3 locked, matching the reference's rhythm |
| `ChilbisPerRow` | `7` | **`5`** | Reference packs 4–5 abreast; 5 on a denser band reads as a crowd |
| Row spacing | `13.84f` | tighter, overlapping | The crowd must read as packed, not as a queue of individuals |
| `CircuitMaxChilbis` | `420` | re-derive | A horseshoe holds fewer than a closed loop; 420 was tuned for the loop |

> **`Requirements.md` FR-2.1, FR-3.2, FR-3.9, FR-6.2 and FR-7.3 all need amending for these.** Do it explicitly in the same commit, or a later agent will "restore parity" and revert them — the same failure mode that froze the buses flat under FR-4.3.

### 4.3 Queue geometry

Replace the 16-point closed Catmull-Rom circuit with a **horseshoe**: a U-shaped band, open at the bottom, with **two exit gates** in the inner wall feeding the apron. Keep `CatmullRomSpline` and `SplineArcLengthTable` — the maths is sound and reusable; only the control points and the closure change.

This single change retires audit finding §B-3 almost entirely: the front of the queue becomes self-evident from the geometry.

### 4.4 HUD

Delete both pills. Replace with: circular restart (top-left), **level number as bare outlined text** (top-centre, no container), circular pause (top-right). All ≥ 50 dp, inside the safe area. Score and high score move to the victory modal, where they belong.

### 4.5 Boosters

Three buttons, bottom centre, ~70 dp square, rounded, with 3D icons and circular count badges. Roadways' Recall and Shuffle map cleanly onto the reference's Arrange and Jumble; the third slot is free for VIP or a new booster.

---

## 5. Theme — the recommendation that was not taken

> **DECISION (2026-11-21): switch to the beach theme.** I recommended keeping cherry blossom; the call was made the other way and this is now the plan, in full. The argument below stays as the record of the trade-off, followed by how to execute the beach well.

**The case I made for sakura:** cherry blossom park is the one thing Roadways has that this reference does not, and the audit rated it the strongest asset in the build (§A). Matching the beach competes on the incumbent's ground, with their recognition.

**Executing the beach well.** Since the setting is now shared, differentiation has to come from treatment. Three cheap levers, carried into roadmap Phase 18.0:

1. **Golden hour, not midday.** The reference reads flat and midday-lit. A low warm key with long soft shadows, warmer sand and a deeper teal sea is instantly a different image at identical production cost — and long shadows flatter the chunky vehicle silhouettes.
2. **Keep the Chilbis.** The candy-bean rig is yours and the audit rated it a genuine asset. A packed crowd of *your* characters on a beach is already a different picture from the reference's plain capsules. Do not redraw them to match.
3. **One signature prop line** the reference lacks — a pier, striped cabanas, a lighthouse on the headland — used consistently across art, icons and the store listing.

With those three, you get the reference's production values and still read as your own product. Without them, the clone risk is real.

Note also `Requirements.md` FR-1.3 currently forbids this: *"Strictly zero trees, zero falling leaf particles, and zero obstacles/characters/vehicles on the grass lawn."* That constraint is why the park reads as empty paper (`#fff1f2` at L97 — audit §J.1). **FR-1.3 must be deleted** for the beach to have any props at all. It is one of the two spec rules actively causing the "dull" problem, the other being FR-4.3. FR-1.1 and FR-1.2 (the `#fff1f2` park ground and `#f2d5db` lawn decals) are replaced by the beach ground spec.

---

## 6. Decisions — both now resolved

### Decision A — adopt the arrow/unblock mechanic? → **ADOPTED**

| | Keep dispatch-only | **Adopt unblock (recommended)** |
| :--- | :--- | :--- |
| What changes | Depot stays a tidy row of 8; restyle only | Depot becomes ~30 scattered vehicles of varied length/angle, each with a direction arrow; a vehicle leaves only if its path is clear |
| Player decision | "Which colour matches" | "Which vehicle can get out, and in what order" — plus the colour match |
| Cost | ~0 extra | **+4 d** in Phase 15 (jam generator, path-clearance solver, level data), +2 d in Phase 20 (difficulty curve) |
| Risk | The audit's core criticism stands: the loop has almost no decisions (§B-3, §C-3) | Level design becomes real work — you need solvable, escalating jams |

**Resolved: adopted** (this was the recommendation). It is the genre's actual depth, it is what makes the reference's 44 % screen share worth having, and Roadways already has a primitive version of it (the L3+ tandem blocking, which the audit praised as one of the best-communicated things in the build). Doing it behind the Phase 14 view interface is far cheaper than retrofitting later.

### Decision B — theme → **BEACH**

| | **Keep sakura (recommended)** | Switch to beach |
| :--- | :--- | :--- |
| Upside | Differentiated; the audit's highest-rated asset; same production values achievable | Closest to the reference |
| Downside | Requires amending FR-1.3 to allow props and density | Becomes a visual clone of a 10M-install incumbent; competes on their ground |

---

## 7. Revised headline

With both decisions resolved, the target is:

> A **perspective-3D golden-hour beach** where a dense, colour-blocked crowd of **candy-bean Chilbis** files around a **horseshoe** toward two exit gates; the player clears an **arrow-based vehicle jam** below to get the right-coloured bus into one of **7 shallow-angled bays** before the crowd backs up — rendered with saturated colour, a low warm key light and long soft contact shadows filling **~97 %** of the screen.

Same genre and same production standard as the reference. The distinctness now rests on three things — golden-hour lighting, the Chilbi characters, and one signature prop line (§5) — so treat those as requirements, not polish.
