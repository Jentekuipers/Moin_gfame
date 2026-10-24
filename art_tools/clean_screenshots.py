from proc import *
G=(191,191,191)
items={
 'Dog_pet.png':[G], 'Dragon_pet_without_legs_please_add_legs.png':[G], 'Frog_enemy_head_please_add_body.png':[G],
 'Glider_bat_enemy.png':[G], 'Groundpound_event_dragon_sprite.png':[G], 'Mushroom_elite_enemy.png':[G],
 'Randomizer_altar_press_E_to_randomize.png':[G], 'Void_Ice_mage_add_icy_colors.png':[G,(119,191,163)],
 'Void_electric_mage.png':[G], 'Void_elite_mage.png':[G], 'Void_fire_mage_enemy.png':[G], 'Void_key_full.png':[G],
 'Void_spike.png':[G], 'Wolf_enemy_without_leg_please_add_the_rest.png':[G], 'legendary_sword_weapon.png':[G],
 'starter_bow_weapon.png':[G,(236,236,236)], 'uncommon_bow_weapon.png':[G], 'Coin.png':[(255,255,255)],
 'Rock_mage_enemy_Remove_whitebackground.png':[(254,254,254),(255,255,255)], 'Shopkeeper.png':[],
 'death_altar_event.png':[], 'Gold_event_bucket_but_color_me_gold.png':[], 'rare_bow_weapon.png':[G],
}
import os
os.makedirs('clean',exist_ok=True)
for f,bgs in items.items():
    im=Image.open('src/'+f).convert('RGBA')
    ds,info=downsample(im)
    ds=remove_bg(ds,bgs, tol=20)
    ds=crop(ds)
    ds.save('clean/'+f)
    print(f, ds.size, 'scale %.2f'%info[0])
