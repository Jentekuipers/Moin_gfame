using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigDeep
{
    // One saved run ("save & quit" on the pause screen, "continue" on the title screen). Roguelike rules:
    // there is only ever one, and it's thrown away when that run ends (death, victory or a new run).
    [System.Serializable]
    public class RunSave
    {
        public int version = 1;
        public int seed, charIndex, maxDepth;
        public float time;
        public float px, py;
        public bool isVoid;      // a run through the void world
        public bool voidDragon;  // the void dragon has already turned up this void run
        public int voidShards;   // the parts of the void key you carry (1 + 2)
        public bool ascent;      // saved on the way to the gods: come back at the surface (ax, ay), ready to fly up
        public float ax, ay;

        // player
        public string stats;
        public List<string> skillIds = new List<string>();
        public List<int> skillCounts = new List<int>();
        public List<string> combos = new List<string>();
        public List<string> combosFired = new List<string>();
        public List<string> weaponIds = new List<string>();
        public List<string> weaponMods = new List<string>();   // comma-separated Mod numbers per weapon
        public List<int> weaponAmmo = new List<int>();
        public List<int> weaponCurse = new List<int>();
        public List<int> weaponCurseKills = new List<int>();
        public int curWeapon;
        public string special;
        public int charges;
        public float energy;
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public int itemSel, nextMoss;
        public List<string> seenOnce = new List<string>();

        // world
        public List<ChunkSave> chunks = new List<ChunkSave>();
        public List<float> doneAltarX = new List<float>();
        public List<float> doneAltarY = new List<float>();
        public List<int> clearedArenas = new List<int>();
    }

    [System.Serializable]
    public class ChunkSave
    {
        public int ci;
        public string t, hp, lt, liq, fl;   // base64
    }

    public static class SaveGame
    {
        static string PathOf => Path.Combine(Application.persistentDataPath, "digdeep_run.json");

        static int exists = -1;   // cached so the title screen doesn't hit the disk every frame
        public static bool Exists
        {
            get
            {
                if (exists < 0) { try { exists = File.Exists(PathOf) ? 1 : 0; } catch { exists = 0; } }
                return exists == 1;
            }
        }

        public static void Write(RunSave s)
        {
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(s)); exists = 1; }
            catch (System.Exception e) { Debug.LogWarning("DigDeep: couldn't save the run: " + e.Message); }
        }

        public static RunSave Read()
        {
            try
            {
                if (!File.Exists(PathOf)) return null;
                var s = JsonUtility.FromJson<RunSave>(File.ReadAllText(PathOf));
                return s != null && s.version == 1 ? s : null;
            }
            catch (System.Exception e) { Debug.LogWarning("DigDeep: couldn't read the saved run: " + e.Message); return null; }
        }

        public static void Delete()
        {
            try { if (File.Exists(PathOf)) File.Delete(PathOf); } catch { }
            exists = 0;
        }

        // ---- byte packing helpers

        public static string Pack(byte[] b) => System.Convert.ToBase64String(b);
        public static byte[] Unpack(string s) => string.IsNullOrEmpty(s) ? new byte[0] : System.Convert.FromBase64String(s);

        public static string PackFloats(float[] f)
        {
            var b = new byte[f.Length * 4];
            System.Buffer.BlockCopy(f, 0, b, 0, b.Length);
            return Pack(b);
        }

        public static void UnpackFloats(string s, float[] into)
        {
            var b = Unpack(s);
            System.Buffer.BlockCopy(b, 0, into, 0, Mathf.Min(b.Length, into.Length * 4));
        }
    }
}
