# Turns the raw art in "New stuff" into clean, game-sized sprite strips (PNG) for Resources/DigDeepArt/new.
import os, numpy as np
from PIL import Image
from proc import downsample, remove_bg, crop
OUT='out'; os.makedirs(OUT, exist_ok=True)
manifest=[]

def save(name, frames, note=''):
    # frames: list of RGBA images; packs them into one strip with a shared frame size, aligned by their union box
    fw=max(f.width for f in frames); fh=max(f.height for f in frames)
    strip=Image.new('RGBA',(fw*len(frames),fh),(0,0,0,0))
    for i,f in enumerate(frames):
        strip.alpha_composite(f,(i*fw+(fw-f.width)//2, fh-f.height))   # bottom-aligned, centred
    strip.save(f'{OUT}/{name}.png')
    manifest.append((name,len(frames),fw,fh,note))

def slice_strip(path, fw, fh=None, grid=None):
    im=Image.open(path).convert('RGBA')
    fh=fh or im.height
    fr=[]
    if grid:
        cols,rows=grid
        for r in range(rows):
            for c in range(cols): fr.append(im.crop((c*fw,r*fh,(c+1)*fw,(r+1)*fh)))
    else:
        for i in range(im.width//fw): fr.append(im.crop((i*fw,0,(i+1)*fw,fh)))
    return fr

def union_crop(frames, pad=0):
    boxes=[f.getbbox() for f in frames if f.getbbox()]
    x0=min(b[0] for b in boxes); y0=min(b[1] for b in boxes); x1=max(b[2] for b in boxes); y1=max(b[3] for b in boxes)
    return [f.crop((x0-pad,y0-pad,x1+pad,y1+pad)) for f in frames]

def from_map(rows, pal):
    h=len(rows); w=max(len(r) for r in rows)
    im=Image.new('RGBA',(w,h),(0,0,0,0))
    for y,r in enumerate(rows):
        for x,ch in enumerate(r):
            if ch in pal: im.putpixel((x,y),pal[ch]+(255,))
    return im

def recolor(im, mapping, tol=12):
    a=np.array(im).astype(int)
    for src,dst in mapping:
        m=(np.abs(a[...,0]-src[0])+np.abs(a[...,1]-src[1])+np.abs(a[...,2]-src[2])<=tol)&(a[...,3]>0)
        a[m,0],a[m,1],a[m,2]=dst
    return Image.fromarray(a.astype(np.uint8),'RGBA')

def shift(im, dx, dy):
    o=Image.new('RGBA',im.size,(0,0,0,0)); o.alpha_composite(im,(dx,dy)); return o

S='src/'
# ---------------- strips that are already game-sized (32px frames)
save('bat', union_crop(slice_strip(S+'Bat_enemy_spritesheet.png',32)))
save('bat_death', union_crop(slice_strip(S+'Bat_enemy_death_spritesheet.png',32)))
save('coin_shine', union_crop(slice_strip(S+'coin shine animation.png',32)))
save('coin_pickup', union_crop(slice_strip(S+'coin pickup animation spritesheet.png',32)))
save('fire', union_crop(slice_strip(S+'fire_spreadsheet.png',32)))
save('key', union_crop(slice_strip(S+'key spritesheet.png',32)))
save('slime_walk', union_crop(slice_strip(S+'Slime_enemy_walk_spritesheet.png',32,32,(2,2))))
save('slime_fall', union_crop(slice_strip(S+'Slime_enemy_fall_and_land_spritesheet.png',32)))
save('slime_hit', union_crop(slice_strip(S+'Slime_enemy_get_hit_spritesheet.png',32)))
save('slimepet', union_crop(slice_strip(S+'Slime_pet_spritesheet.png',32)))
save('slimepet_attack', union_crop(slice_strip(S+'Slime_pet_potential_attack_spritesheet.png',32)))
save('turtle_walk', union_crop(slice_strip(S+'turtle_enemy_walk_spritesheet.png',32)))
save('turtle_slide', union_crop(slice_strip(S+'Turtle_enemy_slide_attack_spritesheet.png',32)))
save('skel_walk', union_crop(slice_strip(S+'skeleton_enemy_walk_spritesheet.png',32)))
save('skel_wake', union_crop(slice_strip(S+'Skeleton_enemy_wake_up_spritesheet.png',32)))
save('skel_punch', union_crop(slice_strip(S+'Skeleton_Enemy_punch_spritesheet.png',32)))
save('bones', [f.crop(f.getbbox()) for f in slice_strip('src/bone/Bones/spritesheet.png',32)])
ss='src/SkeletonSlime boss_or_elite/'
save('bslime_idle', union_crop(slice_strip(ss+'idle_spritesheet.png',96)))
save('bslime_angry', union_crop(slice_strip(ss+'angry_or_summoning_spritesheet.png',96)))
save('bslime_jump', union_crop(slice_strip(ss+'jump_spritesheet.png',96)))
fox=Image.open(S+'Super_rare_melee_fox_mccloud_pet.png').convert('RGBA'); fox=fox.crop(fox.getbbox())
save('foxpet', [fox, shift(fox,0,-1)])

# ---------------- cleaned screenshot art
C='clean/'
def L(n): return Image.open(C+n).convert('RGBA')
save('shopkeeper', [L('Shopkeeper.png'), shift(L('Shopkeeper.png'),0,0)])
save('altar_random', [L('Randomizer_altar_press_E_to_randomize.png')])
save('altar_death', [L('death_altar_event.png')])
gold=recolor(L('Gold_event_bucket_but_color_me_gold.png'), [((173,173,14),(240,190,60)),((156,168,13),(196,140,40)),((176,174,15),(250,206,80)),
     ((165,179,36),(255,226,120)),((177,172,15),(232,176,56)),((161,169,13),(200,150,48)),((234,250,181),(255,248,210)),((234,249,184),(255,248,210)),((232,248,178),(255,240,190))], tol=20)
save('chalice', [gold])
save('rock_mage', [L('Rock_mage_enemy_Remove_whitebackground.png')])
save('mush_elite', [L('Mushroom_elite_enemy.png')])
save('dragon_statue', [L('Groundpound_event_dragon_sprite.png')])
save('void_key', [L('Void_key_full.png')])
sp=recolor(L('Void_spike.png'), [((56,75,81),(26,10,40)),((255,255,255),(200,110,255)),((191,191,191),(120,60,180))])
save('void_spike', [sp])
# bows: grey fill inside the bow is background too
for n,k in (('starter_bow_weapon.png','bow1'),('uncommon_bow_weapon.png','bow2'),('rare_bow_weapon.png','bow3')):
    im=remove_bg(L(n), [(191,191,191),(236,236,236)], tol=20, flood=False); save(k,[crop(im)])
# legendary sword: drop the stray strip on the left and the junk under the hilt
sw=L('legendary_sword_weapon.png'); a=np.array(sw)
a[:, :4]=0; a[-4:, :6]=0
save('lsword', [crop(Image.fromarray(a,'RGBA'))])
# the void mages (the ice one gets icy colours, its stray green blob goes)
ice=L('Void_Ice_mage_add_icy_colors.png'); a=np.array(ice); a[:16,:9]=0; ice=crop(Image.fromarray(a,'RGBA'))
ice=recolor(ice, [((56,75,81),(40,70,110)),((39,52,56),(24,40,72)),((255,255,255),(200,240,255))], tol=40)
for name,img in (('vmage_ice',ice),('vmage_fire',L('Void_fire_mage_enemy.png')),('vmage_elec',L('Void_electric_mage.png')),('vmage_elite',L('Void_elite_mage.png'))):
    save(name,[img, shift(img,0,-1)])
# ice golem: a soft painting, sampled down to pixels
g=Image.open(S+'Ice_golem_boss_remove_grey_background_and_clean_up.png').convert('RGBA')
g=g.resize((g.width//4,g.height//4),Image.NEAREST); g=remove_bg(g,[(202,203,202),(192,192,192)],tol=30); save('ice_golem',[crop(g)])
# jungle temple: the backdrop of the jungle arenas
j=Image.open(S+'Jungle_boss.png').convert('RGBA'); jd,_=downsample(j, 8.55, 8.55); jd=remove_bg(jd,[(255,255,255)],tol=30); save('jungle_temple',[crop(jd)])

# ---------------- art that needed finishing: legs, bodies, colour
A=(56,75,81); B=(255,255,255)
# dragon pet: red, and two little legs that walk
dp=recolor(L('Dragon_pet_without_legs_please_add_legs.png'), [((39,52,56),(120,28,24)),((56,75,81),(206,64,44)),((255,255,255),(255,214,130))])
def legs(base, xs, rows, col, frame):
    w,h=base.size; im=Image.new('RGBA',(w,h+rows),(0,0,0,0)); im.alpha_composite(base,(0,0))
    for i,x in enumerate(xs):
        off = (1 if (i+frame)%2 else 0)
        for r in range(rows): im.putpixel((x+(off if r==rows-1 else 0),h+r),col+(255,))
        im.putpixel((x+1+(off if True else 0),h+rows-1),col+(255,))
    return im
save('dragonpet',[legs(dp,[6,12],2,(120,28,24),0), legs(dp,[6,12],2,(120,28,24),1)])
# dog pet: a second frame with the legs swapped
dog=L('Dog_pet.png'); d2=dog.copy(); a=np.array(d2); last=a[-1].copy(); a[-1]=0; a[-1,5:15]=last[4:14]; save('dogpet',[dog, Image.fromarray(a,'RGBA')])
# skeleton pet (the original sits on a wall texture: redrawn)
sk={'W':(244,244,236),'g':(150,150,160),'K':(40,40,50)}
skA=from_map(["..WWWW..",".WWWWWW.",".WgWWgW.",".WWWWWW.","..WKKW..",".KWWWWK.","W.WKKW.W","..W..W..","..W..W.."],sk)
skB=from_map(["..WWWW..",".WWWWWW.",".WgWWgW.",".WWWWWW.","..WKKW..",".KWWWWK.",".WWKKWW.","..W..W..",".W....W."],sk)
save('skelpet',[skA,skB])
# frog: the head on a squat green body (sitting, and mid-hop)
fh=recolor(L('Frog_enemy_head_please_add_body.png'), [(A,(78,156,64))])
G=(78,156,64); Gd=(52,110,46); Y=(220,226,120)
def frog(hop):
    im=Image.new('RGBA',(13,13),(0,0,0,0)); im.alpha_composite(fh,(1,0 if not hop else 0))
    body=["..GGGGGGGGG..",".GGYYYYYGGGG.","GGYYYYYYYGGGG"] if not hop else ["..GGGGGGGGG..",".GGYYYYYGGGG.","..GYYYYYGGG.."]
    feet=["Gd.......dGG."," dd.....dd..."] if not hop else [".d.......d...","d.........d.."]
    for y,r in enumerate(body+feet):
        for x,ch in enumerate(r):
            c={'G':G,'Y':Y,'d':Gd}.get(ch)
            if c and 8+y<13: im.putpixel((x,8+y),c+(255,))
    return crop(im)
save('frog',[frog(False),frog(True)])
# wolf: the head, then a grey body, a tail and four running legs
wh=recolor(L('Wolf_enemy_without_leg_please_add_the_rest.png'), [(A,(112,112,124))])
Wc=(112,112,124); Wd=(76,76,88); Wl=(160,160,170)
def wolf(f):
    im=Image.new('RGBA',(20,14),(0,0,0,0)); im.alpha_composite(wh,(0,0))
    for y in range(4,9):
        for x in range(9,18): im.putpixel((x,y),(Wl if y==4 else Wc)+(255,))
    for i,(x,y) in enumerate([(18,3),(19,2),(18,4)]): im.putpixel((x,y),Wd+(255,))
    legs=[(4,6),(7,9),(11,13),(15,17)] 
    for k,(a1,a2) in enumerate(legs):
        x=a1 if (k+f)%2 else a2
        for y in range(9,13): im.putpixel((x if y<12 else x+(1 if (k+f)%2 else -1),y),Wd+(255,))
    return crop(im)
save('wolf',[wolf(0),wolf(1)])
# glider bat: purple, gliding (two wing heights)
gb=recolor(L('Glider_bat_enemy.png'), [(A,(92,62,128))])
gb2=gb.copy(); a=np.array(gb2); a[:2]=0; gb2=Image.fromarray(a,'RGBA')
save('glider',[gb,gb2])

with open(f'{OUT}/manifest.txt','w') as fo:
    for m in manifest: fo.write('%s %d %d %d %s\n'%m)
for m in manifest: print(m)

# everything faces right in the game: flip the ones drawn facing left
for name in ['frog','wolf','dogpet','glider','turtle_walk','turtle_slide','dragonpet','foxpet','mush_elite']:
    k=[m for m in manifest if m[0]==name][0][1]
    im=Image.open(f'{OUT}/{name}.png'); fw=im.width//k
    o=Image.new('RGBA',im.size,(0,0,0,0))
    for i in range(k): o.paste(im.crop((i*fw,0,(i+1)*fw,im.height)).transpose(Image.FLIP_LEFT_RIGHT),(i*fw,0))
    o.save(f'{OUT}/{name}.png')
# then copy out/<name>.png to Assets/DigDeep/Resources/DigDeepArt/new/<name>.bytes
