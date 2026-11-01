using UnityEngine;

namespace DigDeep
{
    // Who lives where in the newer biomes (called from PopulateChunk for each open cell).
    public partial class Game
    {
        void SpawnNewBiome(Chunk c, int x, int y, int depth, int bi, byte liquid, float roll, float pr, System.Random r)
        {
            if (TinyPocket(x, y)) return;
            bool floor = Tiles.Solid(world.Peek(x, y - 1));
            bool ceil = Tiles.Solid(world.Peek(x, y + 1));
            float k = (float)r.NextDouble();
            if (liquid != Fluids.None)
            {
                if (liquid == Fluids.Water && roll < pr * 2f && c.liq[(y - c.Y0) * World.W + x] > 0.6f && Fish.PoolSize(c, x, y - c.Y0, 60) >= 5)
                {
                    // the beach's and the reef's exotic fish (and the cloud pools' plain ones)
                    Fish.Kind fk;
                    if (bi == Biomes.Beach) fk = k < 0.25f ? Fish.Kind.Puffer : k < 0.45f ? Fish.Kind.Sword : k < 0.65f ? Fish.Kind.Jelly : k < 0.85f ? Fish.Kind.Clown : Fish.Kind.Biter;
                    else if (bi == Biomes.Reef) fk = k < 0.2f ? Fish.Kind.Angler : k < 0.4f ? Fish.Kind.Jelly : k < 0.55f ? Fish.Kind.Puffer : k < 0.7f ? Fish.Kind.Sword : k < 0.85f ? Fish.Kind.Clown : Fish.Kind.Glow;
                    else fk = k < 0.6f ? Fish.Kind.Minnow : Fish.Kind.Biter;
                    Spawn(new Fish(x, y, depth, fk));
                }
                else if (bi == Biomes.Reef && floor && roll < pr * 1.2f) Spawn(new Urchin(x, y, depth));
                else if (bi == Biomes.Reef && floor && roll < pr * 1.8f) Spawn(new Crabling(x, y, depth));
                return;
            }
            if (roll >= pr) { if (roll >= 0.9994f) TryShrine(x, y, depth, r); return; }
            switch (bi)
            {
                case Biomes.Cloud:
                    if (k < 0.35f) Spawn(new Puffling(x, y, depth));
                    else if (k < 0.6f) Spawn(new Thunderbird(x, y, depth));
                    else if (k < 0.85f) Spawn(new SparkWisp(x, y, depth));
                    else if (floor) Spawn(new TeslaCoil(x, y, depth));
                    break;
                case Biomes.Crimson:
                    if (ceil && k < 0.35f) Spawn(new VampireBat(x, y, depth));
                    else if (floor && k < 0.55f) Spawn(new CrimsonHound(x, y, depth));
                    else if (floor && k < 0.75f) Spawn(new BloodLeech(x, y, depth));
                    else if (floor) Spawn(new Weepling(x, y, depth));
                    break;
                case Biomes.Beach:
                    if (floor && k < 0.5f) Spawn(new Crabling(x, y, depth));
                    else if (k < 0.75f) Spawn(new Gull(x, y, depth));
                    else if (floor && depth > 28) SpawnRaiders(c, x, y, depth, r);   // (pirates, near enough)
                    break;
                case Biomes.Neon:
                    if (ceil && k < 0.35f) Spawn(new NeonMonkey(x, y, depth));
                    else if (floor && k < 0.65f) Spawn(new NeonJaguar(x, y, depth));
                    else Spawn(new GlowMoth(x, y, depth));
                    break;
                case Biomes.Reef:
                    if (floor && k < 0.5f) Spawn(new Crabling(x, y, depth));
                    else if (floor) Spawn(new Urchin(x, y, depth));
                    break;
            }
        }
    }
}
