Art tools for the hand-drawn sprites (needs Python 3 with Pillow and numpy).
1. Put the raw art in src/ (a copy of Assets/New stuff, with Bone-clutter.zip unzipped into src/bone).
2. python clean_screenshots.py   -> clean/ (zoomed screenshots sampled back to pixels, backgrounds removed)
3. python build_art.py           -> out/ (game strips) ; copy out/<name>.png to Resources/DigDeepArt/new/<name>.bytes
