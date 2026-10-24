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
            AddEnemy("waterdragonseg", true, water, new[] { "....HH....", "...rRRr...", ".rRRRRRRr.", "rRRRRRRRRr", "rRRRRRRRRr", "rOOOOOOOOr", "rOOOOOOOOr", ".rOOOOOOr.", "..rrrrrr..", ".........." });
        }
    }
}
