# DIG DEEP: grid mining roguelike for Unity 6

A pixel-art, Dig Dug-style descent roguelike. Everything (terrain, pixel art, lighting, sound, UI) is generated from code at runtime. There are no scenes, prefabs, sprites or audio files to import.

## Setup (2 minutes)

1. Create a **new** Unity 6 project with the **Universal 2D** template (the regular 3D/URP templates also work).
2. Copy the `Assets/DigDeep` folder into the project's `Assets` folder.
3. Open `SampleScene` (or any scene) and press **Play**.

The game boots itself via `[RuntimeInitializeOnLoadMethod]`, so it starts in whatever scene you play. Because of that, keep it in its own project instead of dropping it into Undercroft.

Works with the new Input System (Unity 6 default) or the old Input Manager, and with gamepads.

## Controls

| Key | Action |
|---|---|
| A / D (or arrows) | Move |
| Space | Jump (hold for higher) |
| Mouse | Aim. Your digger always faces the cursor |
| Left click (or Z) | Fire / swing. Hold for automatic fire. Bullets dig through blocks |
| Shift (or right click / X) | Special |
| S / Down in the air | Ground Slam (only with the Ground Slam skill) |
| Q / mouse wheel | Swap between your two weapons |
| R | Reload (weapons also reload by themselves when empty) |
| C | Use item |
| V | Cycle items |
| E | Interact: open chests and crates, buy in shops, take a weapon from the ground |
| W / Up | Climb vines and ropes, float up with Levitation (no longer interacts) |
| Esc | Pause |

Walking or jumping into soft blocks (dirt, sand) still digs them automatically.

### Gamepad

The left stick walks and the right stick aims: the reticle rides a fixed circle around you, twin-stick style. Holding **LOOK / AIM** (RB or clicking and holding the right stick on Standard) doubles the distance for precise long shots. Menus always use the d-pad or stick to move, **A** to confirm, **B** to go back and **LB/RB** to switch option tabs. Prompts switch to pad buttons when you play on a pad.

Layouts (Options > GAMEPAD, or rebind every button yourself, three buttons per action):

| | Standard | Tactical | Dolphin Dive | Platformer | Arena | Southpaw |
|---|---|---|---|---|---|---|
| Jump | A | LB | L-stick click | A | LT | A |
| Shoot | RT | RB | RT | X / RT | RT | LT |
| Special | LT | LT | LB | B | LB | RT |
| Consumable | LB / D-up | B / D-up | B / RB / D-up | RB / D-up | RB / D-up | RB / D-up |
| Interact | X | A | A | LT | A | X |
| Switch gun | Y | Y | Y | Y | Y | Y |
| Reload | B | X | X | LB | X | B |
| Look / aim far (hold) | RB / R-stick click | RT | LT | R-stick click | R-stick click | LB / L-stick click |
| Items back / next | D-left / D-right | same | same | same | same | same |

Southpaw walks with the right stick and aims with the left. Swap sticks and the look / aim distance are separate options too.

## Options

Open **OPTIONS** from the title screen, or press **E** (or click OPTIONS) in the pause menu. Everything is saved and applied straight away.
- **Audio:** master, music and sound-effect volume.
- **Video:** display mode (borderless, fullscreen, windowed), resolution, VSync, frame-rate cap (30-240 or unlimited, when VSync is off), FPS counter.
- **Gameplay & accessibility:** screen shake (0-150%), screen flashes on/off, hit-stop on/off, brightness, UI scale (75-150%), pause when the window loses focus, and reset all progress (asks twice).
- **Keyboard:** rebind every keyboard action, two keys each (click a key cell or press Space, then the new key; Backspace clears, Esc cancels). Esc always pauses and the mouse buttons always shoot and use the special.
- **Gamepad:** a picture of the controller with what every button does, layout presets, swap sticks, look / aim distance, and rebinding (pick a cell, press the new button; VIEW clears, MENU cancels).
- **Reset all settings** returns everything to the defaults.

The title screen also has **CREDITS** and **QUIT**.

## What's in it

- **The story.** The game opens with a slideshow in the style of Undertale's intro: black screen, a square painting at the top, typed white pixel text below, and slow fades. Any button skips straight to the Start screen. The seven paintings are sepia Egyptian tomb-art pixel scenes in `Resources/DigDeepArt/*.bytes`. They're PNG files renamed to `.bytes` and decoded at runtime with point filtering, so Unity import settings can't blur them. To edit a painting, rename it back to `.png`, edit it, and rename it again.
  1. A deity forges the God-Diamond. 2. King Ornin steals it. 3. Its power makes the dwarfs god-like and they live in peace. 4. Ornin builds towers to the clouds to make war on the gods. 5. The gods strike back: the towers fall, the land burns, Ornin is slain. 6. The dwarfs are cursed into half-beasts and driven underground. 7. The God-Diamond waits far below, guarded by what remains of Ornin.
- **Start screen:** START GAME or WATCH INTRO. Esc on the character select screen goes back to it.
- **Intro theme** (`intro_head.wav` + `intro_loop.wav`): mastered to -16.5 LUFS. The first 13-second phrase plays once, then phrases 2-4 loop for as long as the intro runs, with a 120 ms crossfade at the loop point. The theme also plays over the ending.
- **The goal: the God-Diamond, at the bottom of the world (depth ~430).** Below the Magma Core lies the Forgotten Kingdom (from depth 345), full of ancient dwarf masonry. At the bottom is the throne room of **King Ornin**, now undead. He walks you down, slams his sword (with shockwaves), channels the God-Diamond into telegraphed beams (red warning lines first), raises dead dwarfs from the floor, and leaps. Beat him and the curse around the diamond breaks. Walk up to it: time slows to a crawl and the world fades to white. After 2 seconds of white, the final painting fades in (the dwarfs, cured, back in the sunlit land) with the closing text and "THE END".

- **Music** (by Jente, mastered for the game) in `Assets/DigDeep/Resources/DigDeepMusic/`. Both songs are mastered to -16 LUFS integrated with a -1 dBTP ceiling, so they sit under the sound effects.
  - `menu.wav` ("moin"): plays on the character select screen and loops the whole file (24 bars at 170 BPM).
  - `boss_intro.wav` + `boss_loop.wav` ("achtervolging", 131 BPM): the 8-bar intro plays once, then bars 8-55 loop. The loop starts exactly where the intro ends, scheduled to the sample, so there's no gap. The one-bar breather at the end drops straight back into the full groove.
  - Unity import tip: keep the Compression Format at Vorbis or PCM. Don't use MP3, which adds gaps to loops. "Streaming" load type is fine for the long boss loop.
- **Boss fights:** nine of them, at depths 62, 100, 126, 164, 202, 240, 290 and 332, then King Ornin at 430. The Verdant Maw guards the bottom of the jungle (100). A full-width bedrock floor means the only way down is through a brick arena. Step inside and it seals with glowing runes, the boss music kicks in, and a health bar appears. Its leftover shots and warning zones vanish when it dies. Beat the boss to get an altar-tier reward and an ale, and the floor cracks open. Bosses get angrier below 50% HP. Each arena has 3-5 unbreakable one-block metal ledges (two or four blocks up) to dodge onto. Bosses pass straight through them.
  - **The Mole King:** charges, leaps and rains rocks. It burrows, and a trail of dirt tracks you before it erupts under you.
  - **Spore Mother:** rings of spores, aimed volleys, spawns crawlers, and spore rain in phase 2.
  - **Crystal Colossus:** flashes, then charges into the wall and brings crystals down from the ceiling. Stomps send shockwaves along the floor. Takes 1.5x damage while stunned.
  - **Cinder Lord:** flies, spirals of fire, aimed volleys, telegraphed dive bombs, and meteor showers (burning circles on the floor show where fire will land).
  - **The Devourer (105):** a giant worm that swims through the arena's floor and walls. A trail of dust follows you along the floor, stops, then it bursts up in a leaping arc and spits acid at the top. Every third time it lunges straight out of a side wall instead (the wall cracks first). Shoot any part of its body.
  - **Captain Vask (190):** the raiders' leader, with a minigun.
    - A red laser shows where his bullet stream will start, then it turns slowly towards you, so keep moving.
    - Glowing circles show where his grenades will land.
    - He rolls across the hall (untouchable mid-roll) and calls in raider reinforcements.
    - In phase 2, getting too close makes him flash and blast a ring of pellets.
  - **The Ash Wraith (290):** a burning ghost.
    - A ring of embers marks where it will teleport, and it bursts fire when it appears.
    - It sends slow homing skulls (shoot them down).
    - It walks pillars of fire across the floor towards you, and rains ash in phase 2.
  - **New learnable moves:** the Mole King curls up and shakes, then rolls across the arena and bounces off the walls (jump over it). The Spore Mother sends a drifting wall of spores with one gap in it, and plants glowing pods that burst into spore rings. The Crystal Colossus marks the floor with glowing cracks (one under you), then crystal spikes erupt from them. King Ornin sweeps a row of beams across his hall, one after another (step in behind the sweep). Every boss has 50% more health than before.

- **9 playable diggers, half dwarf, half beast** (the gods' curse), each drawn whole with its own silhouette and outfit. Pick one on the select screen (E on the death screen returns there). Each has its own stats, starting gun, special and two starting skills. The first skill is the digger's very own: nobody else can find it until you **win a run with that digger**, after which it can turn up (as a rare skill) for every digger. **Specials never change**: a digger keeps theirs all run (they no longer turn up in chests, shops or altars).

  | Digger | HP | DMG | Rate | Speed | Mine | Jump | Luck | Weapon | Special | Own skill | Second skill |
  |---|---|---|---|---|---|---|---|---|---|---|---|
  | Bram the Mole | 150 | 1.0 | 1.0 | 0.95 | 2 | 1.05 | 0 | Revolver | Quick Dig | Badger Claws (walk through blocks you can break in one hit) | Lucky Coin |
  | Nell the Fox | 90 | 0.95 | 1.15 | 1.2 | 1 | 1.1 | 1.5 | Gilded Revolver | Dash (puts out flames you dash through; burning foes keep burning) | Kindling (25% of shots are fire shots) | Pickpocket |
  | Ribb the Frog | 110 | 1.0 | 1.0 | 1.05 | 1 | 1.25 | 0.5 | Shotgun | Super Jump | Toxic Skin (25% of shots poison) | Ground Slam |
  | Tor the Beetle | 200 | 0.95 | 0.85 | 0.9 | 2 | 1.0 | 0 | Miner's Mortar | Kick | Iron Hide (takes 25% less damage) | Thick Skin |
  | Fin the Dolphin | 110 | 1.0 | 1.05 | 1.05 | 1 | 1.1 | 0.5 | Machine Pistol | Tidal Burst (floods a ball of water around you, shoving foes away) | Gills (never drown) | Flippers (1.5x your land speed in water, shoot up holding jump, big leaps out) |
  | Vex the Owl | 75 | 0.95 | 1.2 | 1.0 | 1 | 1.1 | 0.5 | Bolt Caster | Shadow Step (vanish up to 6 blocks where you hold; a decoy distracts foes for 1.5 s) | Ambush (hits from behind deal 1.5x) | Feather |
  | Nyx the Black Cat | 95 | 1.05 | 1.1 | 1.15 | 1 | 1.15 | 1 | Dagger (weak, fast melee) | Parry | Sharp Claws (+25% melee damage) | Swift Boots |
  | Ink the Octopus | 85 | 0.95 | 1.0 | 1.0 | 1 | 1.0 | 0.5 | Laser Pistol | Camouflage | Tentacles | Night Eyes |
  | Liv the Lynx | 85 | 1.1 | 1.0 | 1.1 | 1 | 1.15 | 1 | Scout Rifle | Grappling Hook | Deadly Stillness | Laser Sight |

  - **Parry (Nyx):** about 1 s where you stand planted (no moving, no shooting) and can't be hurt (burning and poison still tick). Enemy shots that reach you fly straight back and can hit whoever fired them (your own shots aren't parried). Anything that touches you gets a quick counter-slash. Every parried shot or counter adds 0.4 s, up to 2 s in total. Afterwards you get a short grace period, as if you'd just been hit.
  - **Camouflage (Ink):** you fade almost invisible for up to 6 s. Enemies lose track of you and won't pick you as a target. Shooting, swinging or getting hurt ends it early. (Bosses aren't fooled.)
  - **Tentacles (Ink):** four little tentacles, the octopus's own colour, keep feeling for the nearest wall, floor or ceiling within about 2.4 blocks (smaller than Noita's lukki). While at least one grips, you fly wherever you hold, like Levitation; let go of every surface and you fall. A tentacle lets go when its block is dug away or you move out of reach.
  - Ground Slam, Wall Jump, Pickpocket and Death's Guide are ordinary trinkets now (anyone can find them). The boar and the jackal are retired.

- **Game feel (Super Crate Box pass):** every shot visibly shakes and jerks the screen (even pistols), shoves you back harder, and is a bit louder. Shots pop with a white flash where they hit walls, and breaking a block thumps the screen. Hit enemies flash white, swell for a frame and jolt the camera. Kills get a longer freeze frame, a white shockwave ring, a heavier shake and a quick zoom punch towards the action; big guns (shotgun, sniper, rockets, railgun, hammers) zoom-punch on every shot. Movement is tighter: faster acceleration, heavier gravity with the same jump height, and hard landings thump the screen.
- **Playtest round 1 fixes:**
  - The whole UI is bigger (5x pixel scale at 1080p instead of 4x).
  - The character select screen is decluttered: no stat bars, just the digger, a one-line blurb and four big loadout cards (weapon, special, own skill, second skill). Hover a card for its details.
  - Jumps: jump height now scales linearly with the JUMP stat (velocity uses its square root), and Bram (1.05) and Grum (1.0) got higher base jumps, so every digger can reach the 2-high boss ledges.
  - Chests and weapon crates glow brighter, pulse and twinkle. Walk up to one and press **E** to open it ("E: OPEN CHEST" shows above it). Shooting or digging them still works too.
  - At least one weapon crate in every 32 rows (two chunks) below the camp, on a cave floor.
  - Sounds: every synthesized sound is normalized to a clear, full level (block-material ticks stay quiet), master volume is up, and hits and kills are crisper square-wave chirps.
  - When a special charge comes back you hear a clear rising chime and the special slot flashes.
  - Damaging a boss recharges your special a little.
  - Bedrock (world walls and floors) now looks and sounds like the void.
  - Overheating: an overheated energy weapon now vents at about half its normal cooling speed and has to cool almost completely, so the penalty scales with each gun (laser pistol about 2 s, plasma gun about 3.5 s, railgun about 4 s) instead of being a flat amount.
- **Playtest round 2:**
  - Weapon modifiers all wear one loud, pulsing hot pink (in tooltips, labels, the weapon panel where each sits on a pink pill, and as a pink halo with sparkles on modifiers in shops and on the ground). Nothing else uses that colour.
  - Text floating in the world (shop labels, prompts, popups) is sized to the world again, so stacked lines no longer overlap at bigger UI scales.
  - Laser Pistol damage 1.5 -> 1.2 per beam.
  - Energy weapons get shakier as they heat up (up to 9 degrees of extra wobble right before overheating), the railgun included.
  - Falling chests and weapon crates no longer hurt you (spikes and sand still do). Chests, crates and boulders keep their cracks and damage when they fall and land.
  - **Boulders are physics bodies.** Lean into a boulder (with room behind it) and it starts rolling; explosions throw them, the Kick boots them, hard-hitting shots (knockback 5+) shove them, and kicked enemies smash them loose. Boulders are heavy: from the side they are solid, and leaning on one only shoves it along at a slow 2.2 tiles/s (it grinds to a halt fast). Any boulder moving into an enemy (falling or rolling, 2.5+ tiles/s) flattens it outright; bosses take a big hit instead. Only the side it is moving towards hurts (running into the back of a rolling boulder is safe). Moving into you, it hurts by the square of its speed: a 4-block drop does about 250 (deadly for most diggers at the start), a slow roll just a scratch, and pushing one yourself never hurts. A hard hat bonks falling boulders off your head, sets off TNT, presses pressure plates, keeps exactly the same health (cracks) however much it rolls or falls, only shattering on a drop of more than 7 blocks. Explosions still damage boulders, resting or rolling: inside the blast they take the same damage a block would (usually enough to break them), and anything that survives, or sits just outside the blast, gets thrown. Walk into a slow one to roll it along; enemies nudge it too. Once it stops it slides onto the grid and becomes a block again, cracks and all.
  - New diggers Tor (tank, Kick special, Miner's Mortar: a weak lobbed bomb that blows out a 2.4-tile pocket of rock with every shell) and Liv (Scout Rifle: a light sniper, 4 rounds; Grappling Hook; Deadly Stillness). Combos: Juggernaut (Iron Hide + Iron Lung, another 10% less damage), Marksman (Deadly Stillness + Laser Sight, crits deal 4x). Diggers who don't fit on the logs stand around the fire.
- **Art pass (crisp pixels + moodboard palette):**
  - Integer pixel scaling: the camera picks a view size so every art pixel is a whole number of screen pixels (4x at 1080p, 5x at 1440p), no anti-aliasing, and positions snap to art pixels. No more uneven, shimmering pixels.
  - Hue-shifted colour ramps everywhere (`Core/Palette.cs`): shadows lean violet/blue, highlights warm orange, instead of flat brightness multiplies. Block textures use 2x2 clusters instead of single-pixel noise, and are lit from above: a coloured rim on top edges, a lighter line on the sides, a violet shadow underneath.
  - New biome palettes after the moodboard: terracotta dirt and lavender stone over deep violet with teal-green moss (Topsoil), magenta and teal (Fungal Hollows), cold blues and cyan (Crystal Depths), ember reds (Magma Core), cream and teal (Forgotten Kingdom). Water is teal. The darkness and the void are deep violet instead of black.
  - Every outlined sprite (diggers, enemies, items, props) gets automatic volume: warm light on top edges, violet shade on bottom edges, and a deep violet outline.
  - More Super Crate Box: the name of a weapon you pick up or swap to slams onto the screen in big letters (with a shake and zoom punch on pickup), and hits shove enemies back 40% harder.
- **Playtest round 3:**
  - **Save & quit:** the pause screen has a SAVE & QUIT button (or press Q). It saves the whole run (your digger, stats, skills, combos, weapons with modifiers and ammo, special, items, gold, and every block of the world you've changed, plus finished altars and beaten bosses) and returns to the title screen, which then offers CONTINUE, NEW RUN and WATCH INTRO. There is only ever one saved run: it's deleted when you die, win, or start a new run. (Coins and items lying on the ground and enemies aren't saved; shops restock.)
  - Starting health now runs from 75 to 200, lower for more mobile diggers: Vex 75, Liv 85, Nell 90, Ska 110, Bram 150, Grum 175, Tor 200.
  - More enemies right below the camp (spawns start at depth 18 and the early rate is ~45% higher). Bats roost in packs of 1-3 and keep their distance from each other instead of stacking.
  - The camp holds 5 coins in total (tents, barrel, crates, cart). The logs (and the diggers sitting on them, or standing around) can't be broken.
  - The swinging rope only holds for 3 seconds: in the last second it creaks three times, faster and higher, and flashes red, then snaps loose.
  - Boulders never break from falling any more.
- **Health:** a health bar (starting at 200-500 HP depending on the digger) with a white trail that shows the chunk you just lost. Heart trinkets add max HP, more for rarer ones: Stout Heart +25, Thorn Mail +40, Iron Lung +75, Adamant Heart +120. Enemy touches and normal enemy bullets do 60 HP near the surface, rising by 25 per 100 depth. Shotgun pellets do less, marksman shots more. Spikes, lava and magma do 50+, explosions 70+, and falling boulders kill you outright (unless you wear a hard hat).
- **Isaac-style stats**, shown down the left side of the HUD with a green/red +/- flash when they change: Speed, Fire Rate (attacks per second; bonuses above your base count for half, so it climbs slowly), Damage (actual damage per trigger pull), Mining, Jump and Luck. Luck adds crit and proc chances, makes dug blocks drop coins, and tilts chests towards rarer trinkets.
- **Specials (Shift)** belong to your digger and never change. They **recharge over time**, and every kill shaves a little off. The bar next to the special slot shows the recharge; the pips above it show stored uses.

  | Special | Recharge | Max uses | What it does |
  |---|---|---|---|
  | Dash | 3.5 s | 1 | A quick, untouchable burst in the direction you're holding |
  | Grappling Hook | 2.5 s | 2 | Hook a wall where you aim and zip to it |
  | Super Jump | 4 s | 1 | Rocket straight up, smashing through dirt and foes above |
  | Swinging Rope | 2.5 s | 1 | Hold on for up to 3 s. Lasso the ceiling (towards your cursor, or up-forward) and swing. A/D pump, W/S climb, Jump or Shift lets go. You can shoot while swinging |
  | Quick Dig | 4 s | 1 | Drill 6 blocks straight down through rock and brick |
  | Blink Rune | 7 s | 1 | Teleport 7 blocks straight down (from depth 30) |
  | Aegis (rare, from depth 40) | 6 s | 1 | Half a second of total invincibility |
  | Kick (Tor) | 1 s, fixed | 1 | A long boot (reaches about 2.5 blocks), so you can kick foes before they touch you. Kicked foes fly, bowl into other foes (who fly too), slam into walls and knock boulders loose. Kills and boss hits don't shorten the cooldown |

  The Glimmer Stone and Sentry are now consumable items (C).
  Recharge items: Battery Pack (+1 use), Dynamo (25% faster), War Hymn (kills count 1.4x), Soul Jar (kills count 2x), Spark Plug (15% faster), Capacitor (+1 use, 20% faster), Lightning Bottle relic (+1 use, 2x faster).

- **Grid world, 84 tiles wide** (about 3 screens) with bedrock walls, infinite depth, streamed in 16-row chunks.
- **7 biomes** that get nastier with depth: Topsoil, Overgrown Jungle (66), Fungal Hollows (130), Crystal Depths (168), Storm Caverns (210), Magma Core (245), Forgotten Kingdom (345). Block hardness, cave density, enemy count, enemy HP and damage, and darkness all ramp continuously.
- **Blocks:** dirt, sand (falls), stone, hard stone, obsidian, gold ore, gem ore, chests, weapon crates, boulders (wobble, fall and crush), TNT, crystal, volatile crystal (explodes when broken), lava, spikes, magma rock (burns), bedrock, and big gold deposits (below).
- **Liquids (water, lava, steam):** liquids are no longer blocks. They're a fluid simulation layered on the grid (`World/Fluids.cs`):
  - **Flow:** every cell holds an amount from 0 to 1. Liquids fall, pour over edges and spread out thinner over wide floors, but never below a minimum thickness (1/8 of a tile), so a thin puddle doesn't just vanish. Lava is thick and spreads slowly. Liquids flow around and through chests, crates and spikes instead of being blocked by them. Chests, crates, sand, coins, dropped weapons and corpses sink slowly through water and lava (boulders still plunge straight through). Liquid pouring into open air is drawn as a falling stream (Minecraft-style waterfalls and lavafalls) instead of floating slabs.
  - **Mixing:** water meeting lava turns the lava into a layer of stone and boils off as steam. Lava melts ice back into water.
  - **Steam:** rises, spreads along ceilings and slowly fades.
  - **Biomes:** water pools collect in cave bottoms. In the Crystal Depths they're frozen solid (ice blocks), and any water that flows in freezes. In the Magma Core, water open to the air slowly boils away into steam.
  - **Swimming (Noita-style):** you move at about half speed. Let go of the keys and you float up to the surface. Hold Space to swim up and S to dive, and press Space at the surface to hop out. Shots slow down in water.
  - **Air:** with your head under water, or in steam, an 8-second air meter (bubbles next to the health bar) drains, then you start drowning. Enemies drown and choke on steam too.
  - **Fire and water creatures:** water instantly kills fire creatures (imps) and puts out flamethrower flames and fireballs. Lava kills everything not made of fire, including the new water slimes that live in pools.
  - **Frost shots** freeze the water they hit into a stationary ice platform, and turn lava into obsidian.
- **Falling things:** spikes, chests and weapon crates drop when the block under them breaks. Spikes land as new spikes, and crates keep their colour. Every block material has its own quiet, pitch-varied hit sound, and the indestructible dark matter answers with an alien warble.
- **TNT goes off** when it's shot, when anything falls on it (boulders, sand), when lava or fire touches it, or when something slams into it fast (bombs, flung corpses). Walking or landing on it is safe; only hitting it at nearly full falling speed sets it off.
- **Explosions throw you.** Every blast flings the player away from its centre (harder the closer you are), even blasts that can't hurt you, so bombs and grenades can be used to rocket-jump.
- **The camp** is breakable: shoot or blow up the tents, logs and campfire, and dig out the floor under them to make them fall. A long drop smashes them.
- **Big gold deposits:** a semi-rare 2x2 Gold Vein (about 30 per run) and a rare 4x4 Mother Lode with gems in it (about 6 per run, from depth 40). All tiles share one health pool. Every hit flings a coin at you and cracks it further, and when it breaks it bursts into a fountain of gold.

### Weapons (guns that dig)

**Balance pass:** explosions are risky, so they hit harder (Miner's Mortar 3.6, Grenade Launcher 6.3, Plasma 5.6, Rocket 10.5, Seeker 2.6 per rocket; bombs 9, dynamite 6, super bomb 16), and the Explosive modifier and "all shots explode" blasts are bigger (1.4 tiles) and stronger. Black Powder (and every blast-size bonus) now makes blasts stronger as well as bigger. Fully automatic guns do a bit less (Machine Pistol 0.58, Minigun 0.37, Flamethrower 0.21, Assault Rifle 1.4 per round). The Shotgun hits harder up close (0.8 x7) but its pellets only fly about 6 blocks, and it kicks harder. Single-shot guns are now big, slow, precise hitters: Scout Rifle 9 damage, 3 rounds, 1.9 s reload, pierces 3 foes and 2 blocks; Sniper Rifle 18 damage, very fast and long-range, pierces 6 foes and 6 blocks, 1.7 s reload; Bolt Caster 6.5; Railgun 13. The numbers below are the old ones where they differ.

**Your real numbers:** hover a weapon to see what it actually does in your hands: damage per shot after all your bonuses, how many shots each trigger pull fires (Triple, barrels, Mirror Soul) and trigger pulls per second (after Triple, High-Caliber and barrel penalties and your fire rate).

**Reloading:** clicking with an empty magazine reloads. The shotgun's first shell (opening the action) now takes 1.6x a shell instead of 2.4x.

**Recoil** no longer stacks up: rapid shots can't push you faster than a single shot's kick, so you won't get blown across the room (the Curse of Recoil still triples it, and it counts while the cursed gun is in either slot).

Aim with the mouse, left click to fire. There's no ammo. Every bullet that hits a block damages it, so your gun is also your pickaxe. You carry two weapons (Q swaps). Weapons come from **weapon crates** (steel boxes with coloured bands: white common, green uncommon, blue rare, gold relic), shops, and puzzle altars.

| Weapon | Rarity | Notes |
|---|---|---|
| Revolver | Common | Reliable all-rounder, 12-round magazine |
| Machine Pistol | Common | Fast, wild spray |
| Shotgun | Common | 7 pellets, huge recoil, devastating up close |
| Pickaxe | Common | Melee. Fast digging, the old way |
| Dwarven Sword | Common | Melee. Digs 3 blocks a swing, bats bullets back |
| Gilded Revolver | Uncommon | Ore drops double gold |
| Assault Rifle | Uncommon | 3-round bursts |
| Bolt Caster | Uncommon | Piercing bolts that drill rock |
| Laser Pistol | Uncommon | Fast beams that pierce two foes |
| Grenade Launcher | Uncommon | Grenades that explode on impact with walls (or on their fuse in the air). They can hurt you |
| Flamethrower | Uncommon | Short range, melts through any breakable block |
| Disc Gun | Uncommon | Sawblades bounce off walls |
| War Hammer | Uncommon | Melee. Slow, smashes a big area, deflects |
| Sniper Rifle | Rare | Huge damage and knockback, pierces foes, punches through 3 blocks, laser sight |
| Minigun | Rare | A wall of lead, but you walk very slowly while firing |
| Plasma Gun | Rare | Big slow balls that burst and melt rock |
| Rocket Launcher | Rare | Accelerating rockets, enormous blasts |
| Railgun | Rare | Hold to charge, release to pierce everything in a line |
| Energy Sword | Rare | Melee. Wide burning arc, deflects |
| Seeker Swarm / Star Pick / Earthsplitter | Relic | Altars and gold crates only |

Melee weapons deflect enemy bullets back where you aim.

**Reloading.** Guns have magazines, and each kind of reload has its own sound:

| Weapon | Magazine and reload |
|---|---|
| Revolvers | 12 rounds, quick reload. The spent casings drop out |
| Machine Pistol | 25 rounds |
| Assault Rifle | 30 rounds |
| Minigun | 100 rounds, long reload |
| Bolt Caster | 3 bolts |
| Disc Gun | 5 discs |
| Flamethrower | A fuel tank that refills with a hiss |
| Sniper Rifle, Rocket Launcher | Reload after every shot |
| Shotgun, Grenade Launcher | Load one shell at a time; the first shell takes over twice as long as the rest (opening the action). Fire mid-reload to shoot what's loaded, but racking it closed costs a short delay |
| Laser Pistol, Plasma Gun, Railgun | No reloads: they heat up and vent when they overheat (a heat bar shows under the crosshair) |

Empty magazines drop to the floor. Reload skills:

| Skill | Effect |
|---|---|
| Speed Loader | Faster reloads |
| Extended Mag | Bigger magazines |
| Steady Hands | Take 50% less damage while reloading |
| Heat Sink | Energy weapons run cooler |
| Tactical Reload | The first shot after a reload deals 2x damage |
| Ghost Reload (relic, very rare) | Untouchable while reloading an empty weapon |

Matching combos: Quickdraw, Bandolier, Bulwark, Cryo Core, Double Tap and Spectre. Damage is balanced so every weapon of a rarity has about the same damage per second (common ~7.5, uncommon ~9, rare ~11, relic ~14). Fast weapons hit softly per shot. Splash, piercing, bouncing and digging-heavy melee weapons sit a little under that.

### Weapon modifiers

Rare at first, more common the deeper you go. Starting weapons have none, and a weapon can carry at most three (triple-modded weapons are ultra rare and their names glow pink). Shops sometimes sell a modifier. It goes onto **the weapon in your hands** when you buy it, and the label tells you whether it fits.

| Modifier | Effect |
|---|---|
| Homing | Shots steer towards a foe as soon as they can see it, picking the best target when several are visible |
| Ricochet | Shots bounce off walls once. Spray weapons (Machine Pistol, Shotgun, Minigun, Disc Gun, Flamethrower) lose 30% damage |
| Triple | Three shots (left, centre, right), but 50% slower fire for most weapons |
| Crit | +20% crit chance on every shot |
| All-Pierce | Shots pass through every enemy |
| Poison | Poisoned foes take 25% more damage (and a little damage over time) |
| Skipping | Shots arc down (extra gravity) and skip off floors like stones on water, losing half their speed each skip. Explosive shots pop a 25% blast on every skip and blow up in full when they finally stop (too slow to skip). Hitting a foe ends the shot as normal. Not on melee, railgun or flamethrower |
| High-Caliber | Much more damage, knockback and bullet size, much slower fire |
| Fire | Hits set foes (bosses too) alight for 2 s: they burn, glow, and set grass and wood they touch on fire. Shots also light grass and wood they fly past or hit. Not on flame weapons |
| Hunter | Marks a nearby foe in yellow. Killing it gives +speed, +damage or +fire rate for 6 seconds |
| Sharpshooter | Puts a red weak spot in the middle of every enemy. Hit it for a big crit |
| Explosive | Shots explode on anything. Watch out: the blasts can hurt you |
| Wall-Breaker | Triple mining power, and shots smash through rock |
| Frost | Hits freeze the enemy solid for 1.5 s (locked in place, icy look). Each enemy, bosses included, can only be frozen once every 6 s |
| Chain | Every hit fires a spark from that enemy to the nearest other one: up to 10 tiles away with a clear line, or up to 4.5 tiles through rock. Sparks can chain up to 3 times at 70% damage |

- **Enemies:**
  - **Beasts:** crawlers, rock golems, bats, fire imps (ranged), moles that tunnel towards their prey (Dig Dug style), spitter mushrooms, and **mirror beetles**. Every few seconds a beetle hunkers under a shining shell that bounces bullets straight back. Wait it out, blow it up, or hit it with a blade.
  - **Cave worms** (from depth 55) swim through rock and dirt with ease, showing as a dim shape moving under the stone. They burst out into open caves in leaping, dolphin-like arcs and dive back in. Their whole segmented body can be shot.
  - **Raiders** (from depth 28) are surface treasure hunters with guns, in squads of 1-3: Gunners with revolvers, Brutes with shotguns (70+), Marksmen with laser-sighted rifles (95+), Bombers who lob grenades (150+). **Raiders and beasts hate each other.** Raiders shoot beasts, and beasts maul raiders and spit at them, so you can let them fight and clean up.
- **Walking through dirt** (and head-butting it from below) is Badger Claws only. Everyone else digs with their gun.
- **Movement skills:** Wall Jump (hug a wall in the air and press Jump to kick off it; press into the wall to slide down slowly) and Ground Slam (press down in the air to drop like a rock and send out a shockwave). Combos: Parkour (Wall Jump + Cloud Step: wall jumps give back your air jumps) and Seismic Slam (Ground Slam + Aftershock: wider shockwave).
- **Laser Sight** (uncommon trinket): cuts weapon spread by more than half and adds a red aiming laser. Combo with Sharp Edge: **Deadeye** (+5% crit chance, crits deal 3.5x).
- **Pickpocket + Thorn Mail = Phantom:** dashing through foes also hurts them.
- **51 trinkets** (Isaac-style passives shown as icons on the right of the screen). Some only change stats, some only add an effect, and some do both. Pickup labels show the stat changes in green, or orange if there's a trade-off. Four rarities: Common, Uncommon, Rare (from depth 50), Relic (puzzle altars only). Chests are ~70% trinkets.
- **44 combos.** Combos are made from trinkets (skills) only, never from weapons, specials or items. They are small bonuses on top rather than huge power spikes, and a trinket that would finish a combo you've started is about half as likely to show up. Pickups that would progress a combo say `* COMBO PIECE *`. The pause screen lists your trinkets, your combos and any half-finished ones.
- **Healing trinkets are rare and modest:** Moss Bandage (uncommon) heals 40 HP every 60 depth, Vampire Fangs (rare) 30 HP every 12 kills, Gold Tooth (rare) 40 HP per 250 gold. The Ale Keg special heals 60.
- **5 consumables** (Ale, Bomb, Flare, Bore Charge, and the rare **Super Bomb**, which clears the entire screen), **5 shop upgrades** (Pickaxe +1, Backpack +1, Ale Barrel, Hearty Stew).
- **Puzzle altars** (Noita-style rare events) roughly every 40-80 depth, in small brick chambers. Solving one gives a reward you shouldn't have yet: a relic, an altar-only weapon, or loot from deeper down.
  - **All events are a mystery:** no hint text near them and no progress meters. You have to work out what they want.
  - **Pressure plate:** it's too light for you. Something heavy has to fall on it: a boulder, sand, a chest or crate. Nothing is placed for you. The rock above has a few more boulders and sand pockets than usual, so dig upwards and hope, or get creative.
  - **Gold bucket:** you can't toss your own gold in, and there's no gold placed near it. Any coin loose in the bucket's chamber (about 5 tiles to each side and 6 up) gets reeled in and drops through the mouth, so find a way to get gold into that space: dig ore above it, kill enemies in it, open a chest there. A glint of gold shows once anything is inside, but not how much more it wants.
  - **Racing statue:** hit it with anything. A fuse lights and burns along a rope to a flag 1-2 screens down. Reach the flag first.
  - **Meditation statue:** stand still next to it without pressing anything. After 7 seconds it starts to glow, brighter until 10 seconds, then it pays out.
  - **Weapon / skill altar:** the carving shows what it wants. Special altars wake up when you use that special right next to them (dash, grappling hook, super jump, swinging rope, quick dig). Weapons must shoot or strike it. Weapons from altars always come with at least one modifier. Trinket altars wake up when you walk past carrying that trinket.
- **Loot pacing:** the first biome is almost all common weapon crates, with the odd uncommon one and a very rare blue crate. Modded weapons are rare up there. Better crates, rarer trinkets and more modifiers fade in from the second biome on. Puzzle altars and bosses roll their reward as if you were already in the next biome.
- **Weapon glow:** weapons light up the cave around them in their rarity colour, both on the ground and in your hands.
- **Pause screen:** everything you carry is shown as icons. Hover over one for details (the same tooltips as in-game), and use A/D to turn pages if it doesn't all fit. Combos are listed on the right.
- **Shops** every ~50 depth: a lit brick room with a keeper and 4 pedestals (often a weapon, sometimes a modifier). Prices scale with depth.
- **Game feel (the Nuclear Throne list):**
  - **Shooting:** you slow down while firing, recoil pushes you back, and the gun kicks back in your hands. Every gun has a fire delay and slight random spread. Bullets are fat and fast, and the first frame of every shot is a bright muzzle ball.
  - **Impacts:** sparks and rock chips where shots hit. Enemies flash flat white, get knocked back by calibre (sniper huge, SMG tiny), and spray blood or goo.
  - **Kills:** bodies ragdoll away along the killing shot and lie around for a while. Blood and gibs splatter, and the stains stay on walls and floors for good.
  - **Leftovers:** shell casings and rubble stay on the floor.
  - **Screen:** screen shake with a directional kick per shot, and a lingering rumble during chain reactions. The camera leans towards your aim, and hitstop lands on heavy hits and kills.
  - **Explosions:** oversized, with smoke plumes, scorch marks, white screen flashes and bass-heavy booms.
  - **Sound and UI:** every weapon has its own sound, with randomised pitch. The UI is juicy: hearts shake when hit, the gold counter pops, and the weapon panel jolts with every shot.
  - **Movement:** coyote time, jump buffering, corner correction, and pogoing off enemies with a downward melee swing.

### Fire, grass and wood

- **Burnables:** grass patches on dirt (lots in the jungle, some in the topsoil, a little lower down), hanging vines, wooden blocks (dwarven scaffolds, and huts built around some chests and weapon crates), wooden chests and TNT. Grass and vines can't be shot; they vanish when the block under (or above) them goes.
- **Flames** are exactly one block wide. A burning cell lights its neighbours after 1 s (and again at 2.5 s) and burns out after 4 s, shrinking in its last second. A burnt wooden block crumbles (a wooden chest still drops its loot) and its embers drop down, lighting whatever they land on. Fire on rock or dirt just dies out after 4 s.
- **Water** puts flames out with a hiss. **Ice** next to fire, or touched by anything burning, melts into water.
- **Burning foes** take damage over time, glow, and set alight what they touch. Plants burn three times as hard.
- **You can burn too:** stand in fire for more than 0.5 s, touch lava or an imp's fireball, and you're alight for 3 s (damage every half second, and you set burnables on fire). Water, a dash, a fast rope swing or a load of falling sand puts you out.
- **Explosions** throw sparks a bit past the blast. Sparks light grass, wood, ice and foes like a fire bullet would, never you, and die on whatever they touch.
- **Flamethrower (reworked):** lights foes, grass and wood, melts ice, and burns through the jungle like nothing else. It can't roll the Fire modifier (it already is one).

### The Overgrown Jungle (depth 66-130)

Mossy dirt nearly covered in grass, green-grey stone, lots of water and no lava. Vines hang from the ceilings: hold up or down on one to climb it, jump to let go.
- **Snapper:** a rooted flytrap. When you come close it gapes (the tell), then snaps shut on the space in front of it.
- **Seed Pod:** a purple bulb hanging from the ceiling, spitting fans of poison seeds. Cut the block it hangs from and it drops dead.
- **Poison:** poison seeds poison you for 3 s (damage every half second, green tint).
- **Boss, THE VERDANT MAW (depth 100):** a huge rooted flower. Vines burst up out of trembling floor (one spot under you), it lobs arcs of poison seeds, sprouts snappers at the arena edges and spits rings of thorns. Below half health it attacks faster and seeds rain from the ceiling. It takes 50% more damage while burning.

### Run-changers, transformations and synergies (Isaac-style)

These trinkets change how a run plays, and you see it the moment you pick them up. Shots change colour and leave trails, shots grow with your damage, and some trinkets put something on your digger.

| Trinket | Rarity | Effect |
|---|---|---|
| Witch's Brew | Uncommon | Poison and burning hurt 75% more and last 50% longer |
| Dragon's Breath | Rare | All shots set foes alight (and grass and wood). Orange shots |
| Viper Fang | Uncommon | All shots poison. Green shots |
| Frost Heart | Rare | All shots freeze foes (each foe once every 6 s). Icy shots |
| Blasting Cap | Rare | All shots explode. These blasts never hurt you (very fast guns blow up every few shots) |
| Laser Lens | Rare | All shots become fast piercing lasers. Grenades and rockets become lasers that explode on whatever they hit |
| Seeker's Eye | Uncommon | All shots home in on foes. Violet shots |
| Splash Damage | Rare | A shot that hits something bursts into 2 small copies of itself flying outwards (25% of its damage, blast size and speed). Shards don't split again |
| Shrapnel Prism | Rare (very) | Every blast you cause (shots, bombs, chain kills) sprays 6 lasers, roughly evenly spread but never the same twice. Bigger blasts throw more |
| Pressure Chamber | Rare | Hold fire to charge. A full charge is 2.5x the size, damage and blast; a tap is a normal shot. Fully automatic guns (fire delay under 0.13 s) charge a wild, inaccurate burst instead, up to half their magazine. Charging is quicker with faster guns and a higher fire rate |
| Double / Triple / Quad Barrel | Uncommon / Rare / Rare | Every shot fires 2 / 3 / 4 times, for -25% / -40% / -50% fire rate. The best one counts, and it multiplies with the TRIPLE modifier (Quad + Triple = 12 shots). Melee swings and railgun beams multiply too |
| Ogre's Skull | Rare | Damage x1.5 (+0.5). You grow horns |
| Magic Truffle | Rare | Damage x1.25, +50 HP, a bit faster. You grow |
| Hummingbird Heart | Uncommon | Fire rate x2.2, damage x0.5, smaller shots |
| Snail Shell | Uncommon | Fire rate x0.45, damage x2.6, huge shots |
| Wings of Icarus | Rare | Unlimited mid-air jumps. Wings on your back that beat with every jump |
| Levitation | Rare | Float where you hold (W A S D) in the air, or press W on the ground to lift off. Let go and you drift on for a moment before stopping. Jump still gives a hop upwards. Glowing runes under your feet |
| Jetpack | Uncommon | Once a jump peaks, hold jump to thrust up. 1.5 s of fuel, refills on the ground |
| Lingering Shots | Uncommon | Your shots brake hard and hang in the air for about 3.5 s, then blink out. Lingering grenades become floating mines |
| God-Ray | Relic (mythic, altars only) | Every shot becomes a huge, epic laser: 2x the size, 1.5x damage, tears through 2 extra blocks, glows. Every other laser grows too (prism lasers, the railgun beam, the laser pistol) |
| Thousand Blades | Rare | All shots become flying swords. They deal melee damage (+20%, and every melee bonus counts) and bat enemy shots back |
| Brass Knuckles | Common (x2) | +30% melee damage: melee weapons, kicks, stomps, ground slams, dash hits and flying swords |
| Steel Toe Caps | Uncommon | Kicks and stomps hit twice as hard; kicked foes fly further |
| Giant's Gauntlet | Rare | Melee damage x1.6, melee weapons reach 25% further |

**Lasers and colours:** every laser (Laser Lens shots, the laser pistol, prism lasers, the railgun, pet bolts) takes on the colour of the damage types it carries, from trinkets *and* weapon modifiers alike: poison is green, fire orange, frost icy blue, explosive red-orange, homing violet, chain blue, and mixes blend. Plain lasers keep their own colour (Lens red, God-Ray gold, laser pistol cyan, prism pink). Homing bends lasers hard towards foes, and with Seeker's Eye even the railgun beam bends towards foes it can see.

**Everything stacks with everything:** your shots, Shrapnel Prism lasers and Splash Damage shards all go through the same effects. Prism lasers turn green with Viper Fang, bend with Seeker's Eye, grow with the God-Ray, pierce everything with the Laser Lens, linger with Lingering Shots, become melee with Thousand Blades and freeze with Frost Heart. You get more of them with each laser trinket (Lens +2, God-Ray +3) and each extra barrel (+2 per barrel).

### Pets

Pets follow you and fight for you. They never get in your way: they don't block you, can't be hurt, fly through rock (except the beetle, which walks and pops back to you if it gets stuck or left behind), and never touch the world (no digging, no TNT, no burning grass, no altars). Their damage scales with yours, and they pass on Viper Fang poison and Frost Heart frost.

| Pet | Rarity | How it follows | How it fights |
|---|---|---|---|
| Baby Bat | Uncommon | Flutters in loops around your head | Dives at a foe it can see within 6 blocks, bites, flies back |
| Will-o'-Wisp | Uncommon | Hovers at your shoulder | Shoots little bolts at foes it can see within 8.5 blocks |
| Pet Beetle | Common | Trots after you on the ground, hops up ledges | Charges and rams foes near it |
| Spirit Orb | Rare | Circles you closely | Hurts every foe it touches and swallows enemy shots |
| Spore Buddy | Uncommon | Plods along your trail like a duckling | Puffs poison at every foe around it |
| Sawblade Pup | Uncommon | Trots at your heels | Hold fire and it winds up beside you; let go and it flies at the cursor, bouncing off walls and slicing through foes (harder the longer you charged), then comes home |
| Frost Sprite | Uncommon | Drifts at your other shoulder | Freezes and nips every foe that comes close, then rests for 4 s |
| Magpie | Rare | Flits over your head | Snaps up gold and gems lying around before you can. At 200 gold worth it drops 4 treasures of the kind found at that depth |

**Temporary pets:** the Spirit Lantern (uncommon) makes slain foes sometimes (12%, more with luck) leave a ghostly spirit pet (any pet but the Magpie) that fights until it has dealt its share of damage (or 25 s). The Fly Swarm (rare) hatches a little fly from every hit (up to 12) that hunts foes through rock for 6 s. Both count as pets.

**Pet trinkets:** Beast Treats (pets, sentries and other helpers hit 60% harder), Venom Treats (pets poison what they hit), Ember Treats (pets set what they hit alight, but never the grass themselves).

**New sets:** Brawler (Brass Knuckles, Steel Toe Caps, Giant's Gauntlet, Thousand Blades, Stomp Boots, Ground Slam, Sharp Edge): melee damage x1.25, kicks and stomps x1.25, and a red headband. Beastmaster (any 3 pets): pets deal double damage. Lingering Shots, God-Ray and the Will-o'-Wisp count towards Technomancer, the Baby Bat towards Seraph, the Spore Buddy towards Plague Lord, the Gauntlet towards Behemoth.

**New combos:** Lightsabres (Thousand Blades + Laser Lens: damage x1.2), Guardian Angel (Spirit Orb + Wings of Icarus: pets hit 50% harder).

**Smarter enemies:** bats and imps find their way around rock (a short path search a few times a second) instead of pressing against walls, and wobble less while squeezing through tunnels. With no way through, they circle about. Crawlers, golems and beetles hop up one-block steps when they chase you instead of turning around.


The **x** multipliers stack on top of everything else (fire rate multipliers go past the usual fire-rate cap).

**Transformations:** own any 3 trinkets from a set and you change for good. A big flash, a banner, and a new look. Tooltips show which sets a trinket belongs to and how far along you are (e.g. "TRANSFORMATION SERAPH 2/3").

| Transformation | Set | Bonus | Look |
|---|---|---|---|
| Seraph | Feather, Cloud Step, Spring Boots, Wings, Levitation, Jetpack | +0.5 damage, +1 mid-air jump, better glide | Halo |
| Inferno | Ember Core, Phoenix Feather, Lantern Oil, Dragon's Breath, Witch's Brew | You can't burn; all your shots do | Crown of flames |
| Plague Lord | Thorn Mail, Moss Bandage, Rage Mushroom, Viper Fang, Witch's Brew, Magic Truffle | Immune to poison, poisoned foes burst into poison clouds when they die, damage over time +25% | A cloud of flies |
| Bombardier | Black Powder, Blast Proof, Chain Reaction, Bomb Bag, Blasting Cap, Shrapnel Prism | Immune to explosions, blasts 20% bigger, 2 free bombs | Bomb helmet |
| Gunslinger | Echo Chamber, Speed Loader, Extended Mag, Tactical Reload, Laser Sight, Hair Trigger, the Barrels, Hummingbird Heart, Splash Damage, Pressure Chamber | Extra barrels cost half the fire rate, reloads 20% faster | Cowboy hat |
| Behemoth | Glass Cannon, Whetstone, World Eater, Iron Lung, Rage Mushroom, Mole Jerky, Ogre's Skull, Magic Truffle, Snail Shell | +60 max HP, damage x1.2, you grow | Big red horns |
| Technomancer | Heat Sink, Capacitor, Spark Plug, Lightning Bottle, Frost Heart, Laser Lens, Seeker's Eye, Shrapnel Prism, Pressure Chamber | Energy weapons heat half as fast, fire rate x1.15 | Glowing visor |

**Synergies:** the shot effects all stack, so they combine on their own. Homing lasers, exploding lasers, poison-fire-frost shots, 12 charged rockets that each explode into prism lasers, splash shards that carry every effect of the shot they came from. A few pairs also form combos:

| Combo | Needs | Effect |
|---|---|---|
| Napalm | Blasting Cap + Dragon's Breath | Your blasts leave the ground burning |
| Death Star | Shrapnel Prism + Laser Lens | Prism lasers explode, and there are 4 more |
| Rocket Man | Jetpack + Dragon's Breath | Jetpack flames set foes below you alight |
| Plague Bearer | Viper Fang + Witch's Brew | Poisoned foes burst into poison clouds |
| Bullet Hell | Quad Barrel + Hummingbird Heart | Extra barrels slow your fire rate half as much |
| Holy Light | Seeker's Eye + Laser Lens | Damage x1.15 (on top of homing lasers) |
| Shrapnel Storm | Splash Damage + Blasting Cap | Splash shards hit twice as hard and blow up twice as big |

### More run-changers

| Trinket | Rarity | Effect |
|---|---|---|
| Bouncy Rounds | Uncommon (x3) | All shots bounce off walls; each one you own adds another bounce |
| Swell Shot | Rare | Shots leave the barrel 3x as big and strong, then shrink and slow down fast (damage follows the size, so a shot that hits late barely scratches). Lasers start wide and thin down to half size at least |
| Giant Rounds | Uncommon | All shots twice as big, damage x1.15 |
| Wasp Nest | Uncommon | Fire rate x2.5, but shots are tiny and damage x0.6 |
| Spectral Shots | Rare | Where a shot stops (rock or foe), a ghost of it flies on through the rock: further the more mining power the shot had, up to 14 blocks. Ghosts hurt foes and set off TNT and spikes, but never harm blocks. Exploding ghosts still explode (for real) when they run out |
| Soul Splinters | Rare | A homing ghost bursts out of every shot (35% damage) and hunts foes through rock without harming it |
| Wild Card | Uncommon | Some shots (30%, more with luck) get a random extra: explosive, poison, fire, frost, homing, piercing or bouncy |
| Phantom Arsenal | Rare | Now and then a see-through copy of your gun drifts out and fires that gun's shots at the cursor for 3 s |
| Heavy Slugs / Gale Charm / Battering Ram | Common / Uncommon / Rare | Knockback x2 (+0.3 damage) / x1.6 (+speed) / x1.3, and hard hits send foes flying into each other and the walls, like a kick |
| Supernova Splash | Mythic | Shots burst into 4 small copies (25%) when they hit |
| Cloud Burst | Mythic | Shots split into 3 small shots (30%) that drift away slowly and hang in the air for ~3 s |
| Mirror Soul | Mythic | Every shot fires twice, with no fire-rate penalty (multiplies with the barrels and Triple) |
| The Writhing | Mythic | Your shots hatch into friendly worms (up to 24 at once). A worm burrows through rock (without harming it, but it bumps off the void) to the nearest foe, bites 3 times and dies, or wriggles off after 10 s. It keeps everything the shot carried: explosions, poison, fire, frost and the weapon's modifiers |
| Buckler | Rare | A shield floats on the side you aim and swallows enemy shots |
| Wardstone | Rare | A holy bubble that soaks the first hit in every biome |
| Fireproof Cloak / Antidote | Uncommon | You can't be set alight / poison can't hurt you |
| Flak Suit | Uncommon | Immune to explosions, but it's heavy: slower, lower jumps |
| Storm Heart | Rare | Electricity can't hurt you, and water you wade into crackles with charge that hurts foes (never you) |
| Rabbit's Foot | Rare | A 25% chance (more with luck) to get back up with 50 HP when you die |
| Ankh | Rare | Come back once with full health |
| Twin Souls | Rare | Come back twice, with only 10 HP |
| Member's Card | Rare | Shops are 50% off |
| Merchant's Ledger | Uncommon (x2) | Shops stock one more thing |

**Cheating death** (Phoenix Feather, Ankh, Twin Souls, Rabbit's Foot, in that order; all rare): a holy choir chord, a flash, and 3 seconds where nothing can touch you.

**Mythic** trinkets are the relic tier: puzzle altars, golden crates, and now also a small chance in any chest (about 2%, more with depth and luck).

**Better loot early on:** uncommon and rare trinkets show up from the start (rare from depth 25 instead of 70), better crates and modded weapons are more common in the first biomes, and **luck raises every chance in the game** a little: procs, revives, drops, crate quality, modifiers, a second chest treasure, the Ale drop. Luck also keeps curses away.

**Splits keep everything:** splash shards and cloud-burst shots keep the shot's remaining ricochets/bounces, piercing, homing (with a moment to find a target), lingering, skipping and every damage type.

**Pets never get lost:** a pet that's left more than 9 blocks behind, or gets stuck, turns into a little ghost and flies straight back to you through anything.

**Steam** only fades while it can still rise. Trapped under a ceiling it stays.

**Flamethrower under water:** the first instant of each flame still burns and digs, so point blank still works.

**Pressure plate:** anything heavy rolled, pushed or dropped onto it presses it: rolling boulders, settled boulders, chests, crates, stone, TNT.

**Nothing passes the void:** every shot, ghost, returning boomerang, worm and pet stops at bedrock (bombs still go off against it).

### The Storm Caverns (depth 210-245)

Dark slate rock with yellow sparks and lots of water.
- **Electricity** lives in water. Sources let off bursts at random moments, and each burst floods out through every connected water cell, including falling streams (how far it gets down a waterfall is random). It throbs in and out for about a second. The first bursts are thin forks of lightning and a few sparks; a source that keeps firing grows stronger, until its bursts fill the water with white crackling sparks. Sparks leap off the edges of the charge.
- **Getting shocked:** touching charged water stuns you for a moment (you twitch and flicker) and hurts. Enemies are stunned and hurt too. The Wardstone, Parry and other invulnerability block it, and Storm Heart makes you immune.
- **Charge sets off explosives:** TNT and volatile crystal touching charged water, bombs, explosive shots, and volatile enemies (drones).
- **Tesla Coil** (hazard, shootable): hums and glows, then throws lightning at you, at foes and into the nearest water (harder each time).
- **Eel:** lives in the water and lights the whole pool up every few seconds, each burst stronger. Fished out, it flops about and dies.
- **Clank:** a walking machine. Wading through water, it charges it.
- **Drone:** hovers at range and spits sparks that shock you. Electricity blows it up.
- The Crystal Colossus now guards the bottom of the Crystal Depths (202), and the Raider Captain holds the Storm Caverns (240).

### Curses

A weapon sometimes rolls a curse (10% early on, rising to 30% deep down). A cursed weapon always carries one extra modifier as payment, even past the usual three. The curse is on you as long as you carry the weapon in either slot, and it lifts after **10 kills** (the weapon stays). Every curse has its own lantern icon with a differently coloured ghost and its own mark on the cap. The curse, its kill count and a tooltip sit above the weapon's name, and a cursed weapon on the ground gives off wisps.

| Curse | Effect |
|---|---|
| The Digger | Pickaxes orbit you and dig away the ground around you. They never hurt foes |
| The Flame | Every block you step off bursts into flame. It won't light you as you walk away, only if you walk back into it |
| Speed | Everything moves 1.5x as fast |
| Glass | You take 50% more damage |
| Darkness | Your light shrinks to half |
| Poverty | Every hit knocks 15% of your gold out of your pockets (you can pick it back up) |
| Recoil | Your guns kick three times as hard |
| Jamming | Reloads take twice as long, energy guns heat up 50% faster |
| Weight | You jump 25% lower |
| Shaking | Triple spread |
| The Hunted | Enemies spot you from twice as far away |
| Thirst | Healing does nothing |
| The Restless Dead | Slain foes sometimes rise again as bats |
| Lead Boots | You sink in water and can barely swim |
| Rust | Shop prices +50% |
| Water | Every shot leaves a puddle of water where it hits |
| Lava | Slain foes leave a pool of lava |
| The Boomerang | Your shots stall fast and fly back to you (through rock), hitting foes again on the way, and vanish in your hands. Short range |

The old floating-pickaxes trinket is gone from the loot pool. It's the Curse of the Digger now.

### Physics, shops and bosses (batch 33)

- **Boulders** roll with momentum: lean on one and it starts slow, then speeds up the longer you keep pushing (up to a cap). You can push them while swimming or from a ledge, and loose boulders you've already moved stay pushable. Slow boulders only nudge you; only fast ones (above ~4.5 tiles/s) hurt. You can stand on a rolling boulder and ride it.
- **Kicks** smash the blocks in front of you (never the floor).
- **Kicked or launched foes** become loose bodies: they fly, bounce, skid and float. They go back to normal once they've rested for 0.5 s, wherever they land. Moles carry on digging from there, and seed pods grab the nearest ceiling or wither.
- **Flippers** also make you dive fast.
- **Jetpack** works while falling and on a fresh press mid-air: the burners fire harder until you're rising, then give the same push as before.
- **Levitation** is quicker, scales with speed, and gets +12% for every other flight item (jetpack, glide, mid-air jumps).
- **Premium shops** (gilded stands, golden halos, a dressed-up keeper) stock as if you were much deeper and offer the best of several trinket rolls, sometimes a mythic. Prices are 1.6x. They get more likely with depth and luck.
- **Split shards** can hit the foe the shot burst on (after a split second).
- **The Verdant Maw** lives in bushes around its arena. It attacks from one, sinks in, and bursts out of another (watch for the shaking bush), throwing thorns when it emerges. In phase 2 it also drops out of ceiling bushes, upside-down.
- **Pressure Chamber** bursts always fire in full, even when the magazine runs dry partway.
- **Pickpocket** is only offered to diggers with a dash.
- **Biome title cards** show only the first time you reach each biome.
- **Writhing worms** (the enemy) bounce off void blocks. Only the Devourer boss tunnels through them.
- **Trapped gas** (steam under a ceiling) chokes you like water, even when it has spread thin.
- **Icy biome water** freezes over time instead of at once: the surface and water touching ice go first.
- **The Writhing** shots fly, dig and hit like normal shots; a worm hatches wherever each one ends. With **Thousand Blades** the worms carry swords: melee damage, a slash around each bite, a 4th bite, and they bat enemy shots back.
- **The dragon worms**:
  - **Unda, the water dragon**, can rule the crystal depths (instead of the Crystal Colossus). Its **Flood** fills the whole arena with water, then it swims through twice, slowly: right to left at ledge height, then left to right at two-thirds height. Afterwards the water drains away. It also casts **Geysers** under you, an **Ice ring** and slow **Bubbles**.
  - **Ignis, the fire dragon**, can rule the storm caverns (instead of Captain Vask). Its **Inferno** rises to the ceiling and slams down, setting every floor tile alight, so get up on the ledges. It also has a fireball **Volley**, a sweeping **Breath** from a wall, and in phase 2 **Dashes** through the hall.
  - **Both at once** (more likely in harder runs, after you've broken the curse): they take turns while the other hides in a wall corner, or split the hall and attack a half each. Their combo: Ignis sets the floor on fire, Unda floods the hall before it burns out (putting it out) and swims through, then Ignis tears through the water low then high, boiling it into steam under the ceiling. Two boss bars; the survivor fights on alone.
- **Boss arenas are inescapable** once sealed: no dash, blink, grapple, super jump or teleport takes you out.
- **TEST MODE** (title screen) starts a run with a test panel (F1, or VIEW on a pad). It never touches your saved run. The panel has:
  - Every weapon, modifier, trinket and item (click to get it).
  - Every boss, including the dragons and the twins (you're dropped into the next unused arena).
  - God mode and damage numbers on every hit.
  - Training dummies that show your DPS.
  - Gold, heal, max HP, special refill, kill all, and spawning common enemies.
- **Fish** live in the cave's pools. The **minnow** is harmless and darts away from you. The **biter** goes for anything in the water and leaps out at things near the surface. The **grouper** only lives in big pools: it's slow but tough and hits hard. Out of water they fall with it; once they hit the ground they flop frantically back towards water, and suffocate after a while. A draining pool sucks them along a curling path to the hole, and they pour down with the water and dip deep into the pool they land in. Water that freezes around them traps them in the ice: smash the ice and the fish dies, melt it and the fish swims off.
- **Melee reach** is 25% longer, and hits are measured to the nearest part of a foe's body, so big bosses are much easier to hit.
- **Prompts follow your controller**: the help, the special/item/weapon slots and all "press X" prompts show pad buttons while you play on a pad, and keys and the mouse otherwise.
- **Melee hits much harder** (x1.6, and x1.3 more point-blank).
- **Phantom blades**: shot items grow a bigger blade on melee weapons, swung at the same time as the real one (a foe right in front of you takes both). There are laser blades (gold with God-Ray), worm blades (each hit flings worms, up to 2 a swing), steel blades (Thousand Blades), ghost blades (Spectral), big swollen blades (Swell Shot) and a homing blade that lashes at the nearest foe you missed. Splash items make struck foes burst with a shockwave, and fire, poison, frost and wild-card items work on melee hits too.
- **Parry (Nyx)**: once you've deflected or countered something, moving or jumping ends the parry at once with a short burst of invincibility. Otherwise it lingers as before.
- **Loose boulders keep their cracks** while they roll and fall.
- **Main menu (the game is called MOIN for now)**: the start screen sits on top of the live campfire cave of a fresh run. It has a logo and a short list (Continue, New Run, Options, Credits, Quit) that works with mouse or keys. New Run lines every digger up by the fire, leftmost first, with their info across the top. Pick one with A/D or a click. Click them again or press Space and they hop out and the run begins, right there. The death screen's CHANGE DIGGER goes back to that lineup.
- **Text** has a solid dark outline and dim colours are brightened, so everything reads clearly.
- **Loose boulders** stop shots like boulder blocks do: they take mining damage, get shoved by hard hits, and bounce bouncy shots.
- **Chests and crates** never shove you: one that lands on you just lets you walk out of it.
- **Worms take after their shot**: fire, ice, poison, explosive, homing, laser, bouncy and piercing shots hatch matching worms. They take on its colours and leave fire, frost or venom on whatever they wriggle through; their last gasp is a blast, a frost ring or a venom cloud. Flamethrowers hatch the odd fire worm too.
- **Frost shots** no longer freeze water on contact: they swim on and dig like any shot. One in five flash-freezes a little water where it stops, and foes hit while swimming can get iced in. Ice never forms where you stand.
- **Shops** never show the same thing twice on one shelf.
- **One-of-a-kind trinkets** (cap 1) turn up only once a run, taken or not. Other trinkets with a similar effect still can.
- **Reflect pings** from mirror foes are only heard nearby.
- **Text**: world labels show a name, one short line and the action. Hover an item or weapon for full details. Tooltips wrap instead of running off screen. Pause-menu combos show their names only; hover one for what it does.

### Hearts, second bosses, elites, mimics and the gods (batch 49)

- **Ignis's burning floor** (and the Forge Golem's) works like any fire: stand in it for half a second and you catch fire, then burn over time. The flames also cover the ceiling above and the walls they reach, and those set you alight too.
- **The twin dragons**: when one dies, the other is **enraged** for the rest of the fight. It turns red, moves and dashes 35% faster, and recovers quicker. In the combo, Ignis no longer copies Unda's swim heights: its two dashes take two of three other heights (low, middle, high) in a random order and direction.
- **Heart of Unda** and **Heart of Ignis**: the dragons drop their hearts instead of the usual boss reward (the twins drop both). Unda's gives +1 heart, gills, flippers and frost shots. Ignis's gives +1 heart, fire and lava immunity and burning shots. They never turn up anywhere else.
- **A second boss for every biome** (half the time the hall holds this one instead):
  - Topsoil, the **Boulder Troll**: throws boulders at you, leaps and pounds the floor (shockwaves and falling rock), and lowers its head to charge. A charge into the wall stuns it, and it takes 50% more damage while stunned.
  - Jungle, the **Hive Queen**: calls her swarm of bees, aims her stinger and dives (a poison ring where she lands), sheds rings of pollen, and rains sticky honey that slows you.
  - Fungal Hollows, the **Toadstool King**: hops and slams (a fan of poison spores), coughs up drifting spore clouds, lashes his tongue across the hall (watch the band at floor height), and makes mushrooms burst out of the floor under you.
  - Magma Core, the **Forge Golem**: pounds the floor alight (your half, or all of it in phase 2: get on a ledge), rains molten iron, sweeps a flamethrower from the floor up over your head, and throws rings of fireballs.
  - Crystal and Storm already have the dragons. All the new bosses are in the test panel.
- **New creatures**, most of them elemental:
  - **Bees** (jungle): swarms that buzz about and dive at you. The sting poisons.
  - **Flame wisps** (magma) keep their distance and throw fireballs that set you alight. Water kills them.
  - **Frost wisps** (crystal) fire three ice shards that **chill** you (slower for a moment). Chill also puts out fire.
  - **Magma slugs** crawl through the magma core leaving burning embers.
  - **Toxic toads** hop after you and spit poison, and burst into a poison cloud when they die.
  - **Spore puffs** (fungal) drift towards you, swell up and burst into a poison ring.
  - **Spark beetles** (storm) stop, crackle and let out a ring of sparks that shock you.
  - The forgotten kingdom gets a mix of them.
- **Elites**: about 1 in 80 creatures is born an elite. It's 40% bigger and glows in its biome's colour, with ELITE over its head. It has 6x health, hits 1.5x harder and drops 5x gold. Every few seconds it lets out a ring of its element: stone, poison, frost, lightning, fire or gold. Killed, it drops an item from that depth.
- **Mimics**: a tiny chance (0.4%) that a chest is a mimic. It springs open when you come close (or when you break it from afar), then bounces after you and bites hard. Killed, it spits out a rare reward.
- **The God above the clouds**: once **every** digger has found the God-Diamond, touching it starts **the ascent**:
  - The white fades and you're back in Ornin's hall with the power of flight, and an arrow over you pointing up.
  - Fly up and you shoot upwards, very fast. An invisible sphere around you wipes out every block it touches (even the void and the dark-matter crust) and nothing can hurt you.
  - You burst through the crust at the surface (the run is saved here), rise on into the open night sky, and hit a thick bank of cloud that hides the whole screen for a moment.
  - You come out above it, in the God's sky.
  - **Saving**: quitting anywhere on the way up or during the fight brings you back to the surface, ready to fly up again, with the God reset. Quitting before you reach the surface also brings you back there.
  - It's a robed figure against the stars, with blue hands and a blazing sunburst for a head.
  - **Only the head can be hurt**, and touching the god does nothing. It has an enormous amount of health.
  - **Every attack is signalled by the arms and hands**:
    - Hands raised high: **starfall**.
    - Arms spread wide: rings of light (**halo**).
    - A hand pointing at you: pillars of light where you are (**judgment**).
    - Hands apart at your height: a **clap**, and a band of light across the sky.
    - Palms pressed down: **tremor** rings bursting from the hands.
    - From half health: both hands to one side, then a **sweep** across the sky raining light. The head also spits light between attacks.
    - Near the end: arms out, palms up, head flaring: a **spiral** storm.
  - **Everyone flies up there.** Flight is free, quick and the same for every digger, whatever items they carry. JUMP is a short dash with a moment of invincibility, and movement specials become that dash too. You can't leave the screen.
  - Beat it for **the true ending**.
- **Endings**:
  - Until every digger has the diamond, the ending adds a second painting: the same peaceful land, with the God looming over it. Under it is how many diggers still need the diamond (THE END?).
  - The character lineup shows a little **diamond with a tick** over every digger who has found the God-Diamond. It turns gold once they've also beaten the God. A line under the info band shows the count.

### Readability, test mode and menus (batch 52)

- **HUD labels**:
  - The weapon panel shows only the weapon's name, a bit smaller and fitted to its slot.
  - Its modifiers show when you hover the weapon, and in the pause menu, where each weapon is listed with its modifiers (hover one for what it does).
  - The special's and the item's names each sit over their own slot, shrunk to fit, so nothing overlaps.
- **Character lineup**:
  - The God-Diamond count sits in the top-left corner (hover it for what it means).
  - Each digger's achievements show under their loadout: for now, the God-Diamond. It's greyed out until found, ticked once found, and gold once they've beaten the God.
- **Death screen**:
  - It has a QUIT TO MENU button next to CHANGE DIGGER.
  - ESC (START on a pad) also quits to the menu.
- **Test mode**:
  - **Set-up screen**: a test run starts on its set-up screen. ESC there goes back to the main menu with nothing kept. To play, use **START A RUN WITH THIS GEAR** in the BOSSES tab, or pick a boss.
  - **Modifiers**: click one to put it on the weapon in hand and click it again to take it off, up to 3. Modifiers on your weapon, weapons you carry and trinkets you have are lit up (with a count for stacked trinkets).
  - **After dying**: TEST AGAIN starts a new test run back at the set-up screen with everything you picked: weapons, modifiers, trinkets, items, extra hearts and gold.
  - **THE GOD (FROM ORNIN'S HALL)** in the BOSSES tab drops you into Ornin's hall with Ornin beaten and the God-Diamond free, as if every digger had theirs. Touch it to test the whole ascent and the fight. Test runs never count towards your real diamonds or God wins.
  - **Bug fixed**: the click that opened the set-up screen could land on a boss button underneath (usually Ignis) and drop you straight into that fight. It no longer counts, and a test boss pick never carries over into another run.

### Hand-drawn art, the void key and the void world (batch 56)

- **Your art from "New stuff"**:
  - It was cleaned up: backgrounds removed, the zoomed screenshots sampled back down to real pixels, cropped and cut into strips. The results are in `Resources/DigDeepArt/new`.
  - Some pieces got colour where they had none, missing legs and bodies were added, and everything gets the game's violet outline.
  - The build script for them is `art_tools/build_art.py`, if you want to redo or tweak them.
- **Replaced art**:
  - The **bats**, with their own death animation.
  - The **water slimes**: they wobble, stretch as they fall, squash as they land and jiggle when hit.
  - **Coins** glint, with a pickup sparkle.
  - **Fire**.
  - The **shopkeeper**.
  - The **toxic toad**, which is now your frog, with a body.
  - The **Crystal Colossus**, which is now your ice golem.
- **New creatures**:
  - **Turtles** (topsoil, jungle): they pull into their shell and slide across the floor at you, bouncing off walls. Bullets glance off a sliding shell.
  - **Skeletons** (topsoil, fungal hollows, the kingdom): a heap of bones that pulls itself together when you come close, then shambles after you and punches. Smashed, it scatters bones.
  - **Wolves** (topsoil, jungle): often in pairs. They run you down and leap the last bit.
  - **Glider bats** (jungle, crystal, storm): they hang from the ceiling, then swoop down at you in a long glide.
  - **Rock mages** (topsoil, crystal): they raise rock spikes under your feet and blink away when you get close.
  - **The Lantern Shroom** (fungal hollows): an elite. It lobs poison lanterns, calls spore puffs, and always carries an item.
- **The Bone Slime**: a new boss, sometimes instead of the Devourer at depth 100. It hops and slams (shockwaves and falling bones), spits globs of slime, and swells up to call slimes and skeletons.
- **Pets**:
  - **Good Dog** bites foes and noses coins over to you.
  - **Slime Pal** spins into foes that come close.
  - **Bone Buddy** throws bones.
  - **Baby Dragon** breathes a short cone of fire.
  - **Ace Fox** is very rare (relic). It dashes, slashes hard, then blasts the next foe.
- **Weapons**:
  - Three **bows**: the Hunting Bow, the Twin Bow and the Storm Bow. They fire quick, piercing arrows.
  - The legendary **Prism Greatsword** (relic): huge reach, deflects everything.
- **Shrines**, rare, on flat floors:
  - **Altar of Chance**: rerolls the weapon in your hand into another of the same rarity, keeping its modifiers.
  - **Altar of Death**: costs one heart of max health, gives a relic.
  - **Golden Chalice**: pour in half your gold. Half the time you get double back.
  - **Sleeping Stone Dragon**: land on it hard three times (a ground pound, or a long fall) and it gives up its hoard.
- **The void key**:
  - Beat the **first boss within 3 minutes** of the start of a run and it drops part 1 of the void key. Beat the **second boss within 6 minutes** and it drops part 2.
  - While a part can still be won, a small purple timer sits under the depth. The parts you carry show next to your gold.
- **The void world**:
  - Beat **Ornin** carrying the whole key and a black portal opens beside the God-Diamond. Take the diamond for the normal ending, or step through the portal.
  - The portal starts the whole world again from the top, but everything is void: near-black, violet-rimmed walls crumbling into bands of loose squares, and darker caves. You keep everything you carry.
  - **Every creature is a void version**: 60% more health and harder hits. When it dies it bursts into black shards.
  - **Every boss** also strikes out every few seconds: a ring of void shards, or void spikes stabbing up under you.
  - Black **void spikes** lurk in the floors.
- **The void biome and the Void Archmage**:
  - The forgotten kingdom is replaced by **THE VOID**, home to fire, ice and lightning **void mages** (they drift through rock) and the odd void archmage elite.
  - At the bottom waits **the Void Archmage**. It casts fans of void-fire, rings of ice, lightning strikes, rolling lines of void spikes and blinks, and calls mages later in the fight.
  - Beat it for **the void diamond**: a second achievement next to the God-Diamond on the character lineup, plus its own ending.
- **Game icon**: your icon is at `Assets/DigDeep/Icon/MoinIcon.png`. To use it, go to **Edit → Project Settings → Player → Icon** and set it as the Default Icon.
- **Test mode**: spawn buttons for every new creature and the shrines, **VOID KEY (BOTH PARTS)** and **ENTER THE VOID NOW**, and the Bone Slime and the Void Archmage in the boss list.

### Vision, dragons and fixes (batch 58)
- **Line of sight.** Underground, your light only reaches what you can actually see: open cells a ray can reach, plus the rock that borders them. Everything you've had in sight stays dimly lit (`SeenLight` in `Game.cs`). Gold, crystals, chests, lava and entity lights still glow through walls. Lantern oil, night eyes and the like still widen your light, and now also let a faint glow seep into the unexplored dark. Below the first few rows the darkness is the same everywhere.
- **Longer biomes.** Biome starts are stretched 1.5x (`Biomes.Stretch`). Enemy stats scale by depth / Stretch, so difficulty per biome stays the same. Arena depths are `ArenaRoom.A0..A7` and `Final`.
- **Dragon worms.** VIRIDIS, the jungle dragon, can take the jungle arena (1 in 3). NIHIL, the void dragon, can replace a void-changed boss once per void run (25%, saved in the run). The jungle arena has its normal backdrop again.
- **Premium shops** are built of gilded brick and run by the old shopkeeper. There's no label any more. The regular keeper is 1.25x size.
- **Supply crates** (wooden, red cross): break or open them for 1-2 consumables. Chests now only hold trinkets.
- Boulders: their cracks look the same after every quarter turn and wherever they rest. Rolling or falling boulders flatten spikes.
- Flamethrower under water: the flames boil the water away and still dig point-blank.
- Fire with nothing left to burn and nothing under it falls as an ember.
- Swimming up with flippers works by holding up as well as jump.
- With levitate you can't ground slam.
- Explosions from melee hits don't knock you back.
- Grass and vines no longer float after the block under them is gone.
- Elites have no ELITE label: you can tell them by their size and stronger aura.
- The combo list is gone from the in-game HUD.
- Hit flashes now also show on the slime, skeleton and turtle animation strips.

### Shops, boss choices and a storm dragon (batch 59)
- **Vision edges.** Rock behind the walls you can see fades out over two blocks (half, then a fifth), sampled per light texel, so the falloff is pixelated rather than a hard cut at a cell. What you remember gets a soft edge too.
- **Shops** come in three tiers by depth: normal, then **premium** (gilded brick, the old keeper, a second shelf right above the first), then **legendary** (purple glow, mythics more likely).
  - Shelves hold a modded weapon (more mods deeper; 20% are cursed and 30% cheaper), trinkets or a modifier, and a heal spot. Half the time the heal spot holds another trinket instead.
  - Consumables are gone from shops, apart from the heal.
  - Prices rise with depth and with tier.
- **Shopkeepers** fall when the floor goes, and can be killed by blasts, bullets, swings, boulders, fire, lava and long falls. When one dies, its line disappears and its till spills. The line now sits above the sprite.
- **Bosses** drop a choice of three rare trinkets, each with a 5% (luck-boosted) chance to be mythic. They float spread across the hall. Take ONE with interact; the others vanish. A dragon's heart counts as one of the three.
- **VOLTAR, the storm dragon**, can take the Storm Caverns hall (instead of Captain Vask). **Ignis** moved to the Magma Core (1 in 3, alongside the Forge Golem and the Cinder Lord).
- **The Boulder Troll** throws real boulders. They land, roll and stay, and it sends any boulder it bumps into bouncing away.
- **Bows** are hold-to-draw, release-to-loose. Fire rate draws faster, and a full draw hits hardest.
- **No mining stat.** Removed: the MINE stat, STEEL GRIP, AFTERSHOCK, PICKAXE +1, and the mining combos (Pick Frenzy, Master Smith, Molten Core, Steamroller, Seismic Slam). Altars and death altars only give rare or mythic things.
- **YOU SEE ALL** (rare trinket): no darkness at all.
- **Events and altars:**
  - The gold event uses the golden chalice.
  - The gold-doubling 50/50 is now the GAMBLER'S ALTAR, a poker chip on a plinth.
  - Meditation just needs you to touch nothing.
  - The floor under altars and shrines can't break.
- **Options** has a CONTROLS tab with KEYBOARD and CONTROLLER sub-tabs.
- **Loot and spawns:**
  - Fewer chests.
  - Rare and mythic loot ramps up more slowly.
  - Foes keep to biomes that suit them.
  - Enemy health scales a bit more steeply with depth.
  - The bomb is a much bigger blast.
- **Removed text:**
  - The ending's "find the god-diamond with every digger" line.
  - The pause menu's "swap to it" line.
  - The world labels are spaced further apart.

### Spears (batch 60)
- There are four thrown spears, one per rarity: WOODEN SPEAR, IRON SPEAR, CRYSTAL PIKE and STAR LANCE (mythic, altars only). They break through 3, 4, 5 and 7 blocks, then lodge in the next thing they hit.
- A spear lodges straight away in anything it can't break: unbreakable blocks, big ore, chests, crates and altar floors.
- **Hitting foes.** A foe takes one hit and then wears the spear. Spears stuck in things never deal damage again.
- **Dropping.** When the block a spear is in breaks, or the foe it's in dies, the spear drops. It is head-heavy, so it topples point-down like a pendulum and lines up with the air as it speeds up. On the way down it can hit one foe and lodge in it, or lodge in whatever block it lands on, but it never breaks blocks again.
- **Ledges.** A spear lodged roughly sideways in a wall is a ledge, like a Terraria platform. You and foes can stand on it, and walk or jump through it from below. Hold down to drop through. See `Spear.Under` and `World.Ground` / `World.MoveY`.
- At most 20 spears stay lodged; the oldest one crumbles away.
- Modifiers that fit spears: TRIPLE, HIGH CALIBER, WALL BREAKER (+2 blocks) and FIRE.

### Tidy-ups (batch 61)
- The void key timer is gone from the HUD. The time limits themselves still apply.
- Melee weapons smash boulders, both lodged and rolling, for 2x their digging power.
- NIGHT EYES (and the owl digger) widen your light a little instead of lifting all the darkness. MAGNET pulls gold from 8+ blocks away.
- Items are picked up with interact. AUTO PICK-UP in Options > Gameplay brings back walking over them.
- **Shorter text.** Item descriptions are one short line, and "attacks" replaces "shots". A trinket's stat changes show under it, e.g. "ATTACKS GAIN A RANDOM EFFECT" / "LUCK +0.5".
- **Combo hints.** The "? COMBO WITH ???" lines and the half-finished combo list are gone. A combo only shows when you have it, or on the item that would complete it, which pulses "COMPLETES <COMBO>!" (stronger while you hover it).
- **Spears and other spears.** A spear lodging in a block it can't break doesn't burst into debris any more. A spear coming in almost level (within 12 degrees) lodges against a lodged spear and makes the ledge longer; it drops if that spear goes. Coming down any steeper, it snaps the lodged spear and carries on.

### Records, rocket jumps and arrows (batch 62)
- **STATS** (title menu): runs, finished runs, void wins, deaths, kills, bosses beaten, gold, best depth, time played, and every cause of death with its count. `Systems/Records.cs` keeps them; test runs never count.
- **UNLOCKS** (title menu), with found / total in each corner:
  - CHARACTERS: which diggers have the god-diamond (and the void diamond).
  - WEAPONS and SKILLS: icon grids, with unfound ones as dark shapes.
  - BOSSES: names you've beaten. Unseen ones show as ???.
- **Death screen** shows KILLED BY: the nearest foe or boss, or LAVA / SPIKES / FIRE / POISON / DROWNING / AN EXPLOSION / YOUR OWN BLAST / A BOULDER / FALLING SAND, and so on.
- **Stomping** is for everyone. Landing on a foe from above bounces you off just before its body touches you, for 6 damage (+2 per STOMP BOOTS level). That one-shots most first-biome foes.
- **Bug fix:** the ground-slam line had swallowed its own body in a comment. That made stomps only work while a slam was possible.
- **Rocket jumps:**
  - A blast under you throws you up hard, and jumping first adds to it.
  - While you're blown upwards, letting go of jump doesn't cut the height.
- **TNT** only goes off from a really hard knock (speed 20+), a shot, a blast or a slam. Things falling only set it off if they fall fast.
- **Falling sand and spikes** only hurt you if they come down on top of you, not when you're standing on them.
- **Bows:**
  - Arrow speed is 1-100% of full, by how far you drew.
  - Arrows dig deeper the faster they fly (+0-3 blocks).
  - Weak arrows drop.
  - Arrows lodge in the block that stops them, just as clutter (max 40). They fall away when that block goes.
- **Spears:**
  - Spear-on-spear checks the whole shaft, so snapping a lodged spear from above works. Merging (a level throw, within 12 degrees) has a generous vertical aim window.
  - Spears chip big gold, or smash it and keep going; if it holds, they lodge in it.
- **Weapons:**
  - The pickaxe is for digging: tiny damage, very fast, MINE 4.
  - The disc gun cuts 2 blocks in before it bounces.
- **Boss rewards** wait on the hall's ledges. TROPHY CASE (common) makes bosses drop 4 items instead of 3.
- **Rarity:** PROSPECTOR is rare, GILLS is common.

## Where to tune things

| What | Where |
|---|---|
| World width, cave density, ore/TNT/spike rates | `World/Gen.cs` |
| Block HP and properties | `World/Tiles.cs` (`Tiles` static constructor) |
| Biome colours and start depths | `World/Tiles.cs` (`Biomes.All`) |
| Movement, jump, dash | `Entities/Player.cs` (`TickNormal`), `Entities/PlayerGun.cs` (`TickQuick`) |
| Weapon stats (damage, fire delay, spread, recoil, kick, knockback, mining) | `Gameplay/Weapons.cs` (`Armoury.Define`) |
| Modifier odds and effects, crate rarity | `Gameplay/Weapons.cs` (`Mods`, `Crates`), `Entities/Bullet.cs` |
| Specials, trinkets, combos, items, prices | `Gameplay/Loot.cs` (`DB.Init`) |
| What trinkets and combos actually do | `Entities/Entity.cs` (`Stats`) + the code that reads those fields |
| Puzzle altars, their chambers and how often they appear | `Gameplay/Events.cs` (`EventGen.Build`) |
| Enemy spawn rates | `Game.cs` (`OnChunkGenerated`) |
| Enemy stats | `Entities/Enemies.cs`, raiders and beetles in `Entities/EnemiesGun.cs` |
| Big gold deposit rarity, HP and payout | `World/BigOre.cs` |
| Darkness curve | `Game.cs` (`Darkness`) |
| Pixel art | `Core/Art.cs` (character maps) |
| Sounds | `Core/Sfx.cs` |
| Camera zoom | `Systems/CameraRig.cs` (`Size`) |
| Boss HP, attacks, arena depths | `Gameplay/Bosses.cs` (second bosses in `Gameplay/BossesAlt.cs`, dragons in `Gameplay/Dragons.cs`) |
| Elite odds and strength | `Entities/Enemies.cs` (`Setup`, `MakeElite`) |
| New creatures, mimics | `Entities/Creatures.cs`, spawns in `Game.cs` (`SpawnElemental`) |
| The God above the clouds | `Gameplay/SkyGod.cs`; sky flight in `Entities/Player.cs` (`TickSky`) |
| Music volume and fades | `Systems/Music.cs` (`volume`) |

## Code map

```
Scripts/
  Game.cs              boot, main loop, hitstop, spawning, explosions, chests
  Core/                input, pixel font, procedural art + audio, utils
  World/               block defs, biomes, generator, shop rooms, chunk renderer, collision
  Entities/            player, enemies, projectiles, pickups, falling blocks, fuses
  Gameplay/            loot + combo database, characters, shops, puzzle altars, bosses
  Systems/             camera, particles, light map, HUD
```
