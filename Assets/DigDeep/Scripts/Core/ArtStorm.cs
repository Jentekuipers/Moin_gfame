using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Storm Caverns sprites: eels, clanking machines, drones and tesla coils.
    public static partial class Art
    {
        static void InitStorm()
        {
            var sp = new Dictionary<char, Color32>
            {
                { 'M', U.C(128, 138, 160) }, { 'm', U.C(74, 80, 102) }, { 'Y', U.C(255, 230, 80) }, { 'y', U.C(200, 150, 40) },
                { 'C', U.C(120, 230, 255) }, { 'W', U.C(245, 250, 255) }, { 'K', U.C(25, 22, 30) }, { 'E', U.C(64, 96, 150) },
                { 'e', U.C(160, 206, 236) }, { 'R', U.C(232, 64, 64) },
            };

            // EEL: a long blue eel with a glowing yellow fin
            AddEnemy("eel", true, sp,
                new[] { "......YY......", "..EEEEEEEEEE..", "EEEEEEEEEEEKE.", ".eeeeeeeeeeeEE", "..........e..." },
                new[] { "..YY..........", "EEEEEEEEEEEE..", ".EEEEEEEEEEKE.", "..eeeeeeeeeeEE", ".............." });

            // CLANK: a squat walking machine with a spark plug on top
            AddEnemy("clank", false, sp,
                new[] { "....YY.....", "....mm.....", "..MMMMMMM..", ".MmCCMMMMM.", ".MMMMMMMMM.", ".MmmmmmmmM.", "..MMMMMMM..", ".mm.mm.mm..", ".KK.KK.KK.." },
                new[] { "....YY.....", "....mm.....", "..MMMMMMM..", ".MmCCMMMMM.", ".MMMMMMMMM.", ".MmmmmmmmM.", "..MMMMMMM..", "..mm.mm.mm.", "..KK.KK.KK." });

            // DRONE: a hovering machine with a red eye and a dangling spark
            AddEnemy("drone", true, sp,
                new[] { "MM.......MM", ".mM.....Mm.", "..MMMMMMM..", ".MMCCRCCMM.", "..MMMMMMM..", "....mYm....", ".....Y....." },
                new[] { "..M.....M..", ".MmM...MmM.", "..MMMMMMM..", ".MMCCRCCMM.", "..MMMMMMM..", "....mYm....", "....Y......" });

            // TESLA COIL: a stack of coils with a crackling bulb (frame 2: charged up)
            string[] coil = { "..CWC..", ".CWWWC.", "..CWC..", "...M...", "..mMm..", ".MMMMM.", "..mMm..", ".MMMMM.", "..mMm..", ".MMMMM.", "..mMm..", "..MMM..", ".mMMMm.", "mmmmmmm" };
            string[] coilHot = (string[])coil.Clone();
            coilHot[0] = "..YWY.."; coilHot[1] = ".YWWWY."; coilHot[2] = "..YWY..";
            AddEnemy("tesla", false, sp, coil, coilHot);

            Bullets["b_zap"] = Make(new[] { ".W.", "WCW", ".W." }, sp, true, true);
        }
    }
}
