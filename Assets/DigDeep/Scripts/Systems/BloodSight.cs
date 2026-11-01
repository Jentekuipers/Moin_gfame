using UnityEngine;

namespace DigDeep
{
    // BLOOD SIGHT: soaked in blood you see every creature clearly (they glow and show a red edge) for a few seconds,
    // like the OWL SIGHT combo does.
    public static class BloodSight
    {
        static float until;
        public static void Soak() => until = Mathf.Max(until, Time.time + 4f);
        public static bool On => Time.time < until;
        public static void Reset() => until = 0f;
    }
}
