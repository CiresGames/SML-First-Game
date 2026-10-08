# Manta progression

The existing MantaController creates MantaProgression at runtime. The MantaFlight scene needs no rebuild or scene migration. Its shared flight settings remain unchanged; progression multiplies the controller's session values.

## Playing

- Press **Y / Triangle** while standing within 9 m of the manta's seat or while mounted to open Manta Info. **P** on keyboard and **Start / Options → MANTA INFO** also open it. Close with Y / Triangle, P, Escape, the Close button, or gamepad B / Circle. Y retains wingsuit control while airborne on foot; View / Share retains its return-to-spawn function.
- Press **F / Xbox X / PlayStation Square** beside a landed, stationary manta to climb onto its back. Mount reach includes its collision shell so the wings do not force the rider underneath the saddle. The nearby prompt shows the mount control or the exhaustion/overload reason; recovery and load limits still apply.
- The panel pauses rider/manta input and movement, releases the cursor, and uses normal UI navigation. It shows current effects and the next fruit's effects at full stamina without cargo; temporary penalties still affect actual flight.
- Collect glowing fruit on foot or while riding, then use FEED FRUIT in the panel. Each fruit is collected once per save. Fruit is currently the only source of stat upgrades; training points, purchasable ranks and passive practice bonuses are removed.
- Walk within 2 m of a fruit tree's trunk on foot and press **Xbox X / PlayStation Square / F** when the colored **Punch tree** prompt appears. The rider steps up, winds up and punches; impact releases the fruit to tumble, bounce and roll under gravity. Walk beside the landed fruit to collect it, then FEED FRUIT in Manta Info. Trees remain after collection. Walls block interaction, and trees without hanging fruit do not offer the action.
- Land near a cargo cube, stand within 4 m of it and within 9 m of the seat, then press **B** to load it. B unloads on clear ground beside a landed manta. The initial scene supplies 25 kg and 90 kg prototype cargo. Overweight cargo may be loaded on the ground but prevents piloted and autonomous takeoff.
- The endurance HUD appears during flight and recovery. Amber below 25%, red below 10%; slowed wingbeats and a synthesized tired-breath cue accompany fatigue. Below 10%, speed, turning, acceleration and climbing progressively weaken.
- Empty endurance causes a short descent, fade, and ground recovery beside the manta. Rest refills 3 endurance/second. Riding remains locked until 30%. The ground pose uses slowed breathing and drooped wings through the existing procedural animation.

## Balance

| System | Rules |
| --- | --- |
| Overall level | 1–20; next level requires `100 + 45 × (level − 1)` XP |
| Fruit | Two per stat, including carried inventory; each fed fruit adds .12 permanent strength |
| Bond | Separate value; levels 1–10 at 0/100/400/900/…/8100; no gameplay abilities yet |

Speed raises top/cruise/dive speed. Manoeuvrability improves yaw/pitch, response, damping, direction inertia, roll settling and manoeuvre recovery. Endurance expands the fatigue reserve. Force raises capacity (40 kg base) and reduces wind displacement, thereby reducing weather fatigue. Cargo reduces acceleration, climbing and turning and adds fatigue. Obedience fruit is saved and displayed but has no gameplay effect yet.

Base drains per second: descending glide .035; normal flight .18; throttle adds up to .45; high speed .4; climbing .6; full cargo .65; strong wind up to .8 before Force mitigation. Sharp turns add .25/s and cost 1.5 on entry; a started trick costs 1.5. Weather/load/climbing still cost endurance during a glide. Ordinary cruise at base Endurance lasts roughly nine minutes in calm air.

XP and Bond require actual movement while riding. Idle wall contact, menu time and autonomous following do not generate riding XP. Level and Bond track the companion's progress without granting stat upgrades or spendable points. Activity-based training can be introduced in a future design.

## World rewards and extensions

The prototype scene seeds ten finite glowing fruit at deterministic terrain-checked distant locations, two per stat. Their surrounding groves grant a one-time landmark reward. These are procedural exploration placements, not authored puzzle or story rewards. Existing authored MantaFruit components disable automatic fruit seeding. `spawnExplorationFruits` disables the prototype fruit/cargo setup.

Fruit now use five Blender-authored models instead of spheres: cyan teardrop (Speed), violet twisted star (Manoeuvrability), green pear (Endurance), amber pumpkin (Force), and rose heart (Obedience). All are 0.9 m wide × 1 m tall × 0.9 m deep. Their palette is shared with stat headings, row accents and upgrade previews in Manta Info. Editable sources, the rendered lineup and art notes are in `ArtSource/MantaFruits`.

Each fruit site now has a 32 m tree whose foliage matches its fruit and stat color. The five prefabs in `Resources/MantaFruitTrees` are variants of `Trees/Prefabs/ReiTree_ShaderGraph.prefab`, retaining its trunk and leaf shader. They include solid trunk/branch collision, a passable canopy and a fruit anchor beside the lower canopy. Placement rejects steep terrain and obstructed trunks or fruit anchors. Trees and their landmark rewards remain after the fruit is collected. **Manta → Progression → Build colored fruit trees** rebuilds the variants from the source prefab and shared palette. The rendered lineup is `Tools/MantaFruitTrees/FruitTrees-preview.png`.

`RiderTreeInteraction` uses the existing context input and gives reachable fruit trees priority over mounting. The humanoid `Resources/RiderAnimations/TreePunch.anim` runs for 0.9 seconds and releases fruit at 0.34 seconds. The existing rider rig supplies torso motion, arm extension and closed fists; no root motion moves the player capsule. **Manta → Rider → Build tree punch animation** rebuilds this editable Unity clip and its TreePunch animator state. Fruit uses a freely rotating rigidbody and a small bouncing collider; menus pause both fruit physics and rider animation. A dropped but uncollected fruit returns to its tree on reload, while collected IDs remain permanently unavailable.

For authored content, attach **MantaFruit** with a stable unique `fruitId`, stat, and visual child. Do not change shipped IDs or reuse them for another fruit. MantaProgressionReward supports landmark proximity, speed-gated aerial challenge proximity, journey and story rewards. Call `Grant()` from a puzzle/event instead of enabling its proximity Update when appropriate. Unique reward IDs prevent repeat rewards.

MantaProgression exposes `DiscoverLocation`, `CompleteChallenge`, `CompleteJourney`, `RecordStoryEvent`, `SetCargo`, and `Feed`. `Data.BondLevelChanged` emits each crossed Bond level for future unlock listeners. SetCargo rejects invalid masses and over-capacity attachments in flight. MantaCargo supplies a simple ground pickup/drop integration; its contents and mass are session state, not inventory persistence.

## Persistence

Progression uses a version 2 PlayerPrefs JSON snapshot under the existing `Manta.Progression.v1.<saveId>` key, plus a validated backup. Set a unique saveId for each companion/save slot. Level, XP, fruit inventory/consumption/IDs, reward IDs, Bond, name, endurance and exhaustion persist. Valid version 1 saves migrate automatically, keeping fruit and companion progress while removing training ranks, points and practice effects. New snapshots omit those retired fields. Save occurs on feeding, rewards, level-up, recovery, every 20 seconds, application pause/quit and destruction. Invalid snapshots fall back to a valid backup, then fresh data. There is no offline fatigue regeneration.

Safe respawn candidates require a walkable surface, space for the manta and a clear rider capsule beside it. The last candidate is rechecked at failure. The system never intentionally teleports onto an unverified fallback point; custom scenes must contain reachable ground matching the rider's environment mask.

## Validation

- **Manta → Progression → Validate progression rules** checks level caps, fruit bonuses and limits, one-time rewards, Bond separation, invalid values, JSON round-tripping and migration of older saves.
- **Manta → Progression → Validate play mode integration** runs against the active manta in Play Mode. It exercises panel ownership, stat effects, overload, exhaustion, recovery, grounded panel access and spawned fruit. It uses a temporary save key and resets the rider at completion; run in a test session.
- **Manta → Progression → Validate controller and ground mount (Play mode)** walks the rider up to the real collision shell, tests X / Square through the full climb-on transition, preserves exhaustion/overload limits and checks Y / Triangle nearby and mounted access, distance limits, airborne wingsuit input, B / Circle and Start / Options menu controls. Run in a test session; progression is restored and the rider resets afterward.
- **Manta → Progression → Validate tree punching (Play mode)** uses an actual tree variant and simulated gamepad X to check trunk reach, wall obstruction, approach, animated impact contact, physics release, menu pause/resume, grounded recovery, landing and unique inventory pickup. Run in a test session; it restores progression and resets the rider afterward.

The implementation was compiled and progression, controller and tree-interaction checks were run in Unity 6000.1.1f1. The runtime panel and tree prompt were captured and visually inspected at 1920×1080. A live tree punch also released and collected its fruit while retaining all ten trees. Long-session progression pacing and final fruit/puzzle placement still need gameplay tuning.
