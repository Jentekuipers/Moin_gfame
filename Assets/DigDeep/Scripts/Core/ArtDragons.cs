using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The dragon worms: a fire dragon (red, orange belly, bone horns) and a water dragon (deep blue, pale belly).
    public static partial class Art
    {
        static void InitDragons()
        {
            var fire = new Dictionary<char, Color32>
            {
                { 'R', U.C(176, 40, 30) }, { 'r', U.C(110, 22, 20) }, { 'O', U.C(250, 150, 52) }, { 'Y', U.C(255, 226, 120) },
                { 'K', U.C(24, 14, 12) }, { 'W', U.C(250, 245, 230) }, { 'H', U.C(236, 214, 170) },
            };
            var water = new Dictionary<char, Color32>
            {
                { 'R', U.C(44, 96, 168) }, { 'r', U.C(24, 56, 112) }, { 'O', U.C(130, 206, 232) }, { 'Y', U.C(226, 250, 255) },
                { 'K', U.C(10, 18, 34) }, { 'W', U.C(240, 250, 255) }, { 'H', U.C(196, 232, 244) },
            };
            AddEnemy("firedragon", true, fire, new[] { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWWWWW.", "rRRRRRRRRRRRRRRO", ".rOOOOOOORRRRr..", "..rOOOOOOOrr....", "....rrrrr......." }, new[] { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWW.W..", "rRRRRRRRRK......", ".rOOOOOOOKWW.W..", "..rOOOOOOOOOOOO.", "....rrrrrrrrr..." });
            AddEnemy("firedragonseg", true, fire, new[] { "....HH....", "...rRRr...", ".rRRRRRRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOOOOOOOOr", ".rOOOOOOr.", "..rrrrrr..", ".........." });
            AddEnemy("waterdragon", true, water, new[] { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWWWWW.", "rRRRRRRRRRRRRRRO", ".rOOOOOOORRRRr..", "..rOOOOOOOrr....", "....rrrrr......." }, new[] { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWW.W..", "rRRRRRRRRK......", ".rOOOOOOOKWW.W..", "..rOOOOOOOOOOOO.", "....rrrrrrrrr..." });
            var jungle = new Dictionary<char, Color32>
            {
                { 'R', U.C(46, 150, 92) }, { 'r', U.C(22, 84, 64) }, { 'O', U.C(244, 200, 72) }, { 'Y', U.C(255, 236, 120) },
                { 'K', U.C(16, 24, 14) }, { 'W', U.C(250, 240, 200) }, { 'H', U.C(60, 200, 200) },
            };
            var voidP = new Dictionary<char, Color32>
            {
                { 'R', U.C(40, 22, 64) }, { 'r', U.C(14, 8, 24) }, { 'O', U.C(150, 80, 230) }, { 'Y', U.C(236, 190, 255) },
                { 'K', U.C(255, 255, 255) }, { 'W', U.C(220, 200, 255) }, { 'H', U.C(110, 60, 190) },
            };
            string[] headA = { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWWWWW.", "rRRRRRRRRRRRRRRO", ".rOOOOOOORRRRr..", "..rOOOOOOOrr....", "....rrrrr......." };
            string[] headB = { "...HH...........", "....HHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWW.W..", "rRRRRRRRRK......", ".rOOOOOOOKWW.W..", "..rOOOOOOOOOOOO.", "....rrrrrrrrr..." };
            // the jungle dragon has a frilled crest and fins down its back, like the temple serpents
            string[] jHeadA = { ".H.H.H..........", "..HHHHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWWWWW.", "rRRRRRRRRRRRRRRO", ".rOOOOOOORRRRr..", "..rOOOOOOOrr....", "....rrrrr......." };
            string[] jHeadB = { ".H.H.H..........", "..HHHHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWW.W..", "rRRRRRRRRK......", ".rOOOOOOOKWW.W..", "..rOOOOOOOOOOOO.", "....rrrrrrrrr..." };
            string[] seg = { "....HH....", "...rRRr...", ".rRRRRRRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOOOOOOOOr", ".rOOOOOOr.", "..rrrrrr..", ".........." };
            string[] jSeg = { "..H.HH.H..", "...rRRr...", ".rRYRRYRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOYOOOOYOr", ".rOOOOOOr.", "..rrrrrr..", ".........." };
            AddEnemy("jungledragon", true, jungle, jHeadA, jHeadB);
            AddEnemy("jungledragonseg", true, jungle, jSeg);
            AddEnemy("voiddragon", true, voidP, headA, headB);
            // the storm dragon: slate blue with a lightning-yellow belly and a spiked crest
            var storm = new Dictionary<char, Color32>
            {
                { 'R', U.C(54, 70, 120) }, { 'r', U.C(26, 32, 64) }, { 'O', U.C(255, 226, 80) }, { 'Y', U.C(140, 240, 255) },
                { 'K', U.C(255, 255, 255) }, { 'W', U.C(220, 245, 255) }, { 'H', U.C(255, 240, 120) },
            };
            string[] sHeadA = { "H..H..H.........", ".HHHHHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWWWWW.", "rRRRRRRRRRRRRRRO", ".rOOOOOOORRRRr..", "..rOOOOOOOrr....", "....rrrrr......." };
            string[] sHeadB = { "H..H..H.........", ".HHHHHrr........", "..rrrRRRRrr.....", ".rRRRRRRRRRrr...", "rRRRRYKRRRRRRrr.", "rRRRRRRRRRRRRRRO", "rRRRRRRRRRWW.W..", "rRRRRRRRRK......", ".rOOOOOOOKWW.W..", "..rOOOOOOOOOOOO.", "....rrrrrrrrr..." };
            string[] sSeg = { "...H..H...", "...rRRr...", ".rRRYRRRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOOOOOOOOr", ".rOOOOOOr.", "..rrrrrr..", ".........." };
            AddEnemy("stormdragon", true, storm, sHeadA, sHeadB);
            AddEnemy("stormdragonseg", true, storm, sSeg);
            AddEnemy("voiddragonseg", true, voidP, seg);
            AddEnemy("waterdragonseg", true, water, new[] { "....HH....", "...rRRr...", ".rRRRRRRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOOOOOOOOr", ".rOOOOOOr.", "..rrrrrr..", ".........." });
        }
    }
}
