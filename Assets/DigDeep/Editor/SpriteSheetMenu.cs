#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DigDeep
{
    // MOIN > Export Sprite Sheet: writes every sprite to Assets/StreamingAssets/SpriteSheet (see SpriteSheet.cs).
    // The sprites are built when the game starts, so it needs play mode.
    public static class SpriteSheetMenu
    {
        [MenuItem("MOIN/Export Sprite Sheet")]
        static void Export()
        {
            if (!Application.isPlaying || Game.I == null)
            {
                EditorUtility.DisplayDialog("Export Sprite Sheet", "Press Play first (the sprites are made when the game starts), then choose this again.", "OK");
                return;
            }
            string path = SpriteSheet.Export();
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
            Debug.Log("MOIN: sprites exported to " + path);
        }
    }
}
#endif
