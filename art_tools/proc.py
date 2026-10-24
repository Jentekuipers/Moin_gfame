import math, sys, os
import numpy as np
from PIL import Image
from collections import Counter
from grid import boundaries, fit

def downsample(im, sx=None, sy=None):
    a=np.array(im.convert('RGBA'))
    if sx is None:
        fx=fit(boundaries(a,1),0); fy=fit(boundaries(a,0),0)
        cands=[]
        sx,ox=fx[1],fx[2]; sy,oy=fy[1],fy[2]
        s=(sx+sy)/2; sx=sy=s
    else:
        fx=fit(boundaries(a,1),0); fy=fit(boundaries(a,0),0)
        ox=fx[2]%sx; oy=fy[2]%sy
    H,W=a.shape[:2]
    nx=int((W-ox)/sx); ny=int((H-oy)/sy)
    out=np.zeros((ny,nx,4),dtype=np.uint8)
    for j in range(ny):
        for i in range(nx):
            x0=int(ox+i*sx+sx*0.3); x1=int(ox+(i+1)*sx-sx*0.3)+1
            y0=int(oy+j*sy+sy*0.3); y1=int(oy+(j+1)*sy-sy*0.3)+1
            cell=a[y0:y1,x0:x1].reshape(-1,4)
            c=Counter(map(tuple,cell)).most_common(1)[0][0]
            out[j,i]=c
    return Image.fromarray(out,'RGBA'), (sx,ox,oy)

def remove_bg(im, bgs, tol=18, flood=True):
    a=np.array(im.convert('RGBA')).astype(int)
    H,W=a.shape[:2]
    def isbg(p):
        if p[3]<20: return True
        for b in bgs:
            if abs(p[0]-b[0])+abs(p[1]-b[1])+abs(p[2]-b[2])<=tol: return True
        return False
    mask=np.zeros((H,W),bool)
    if flood:
        from collections import deque
        q=deque()
        for x in range(W):
            for y in (0,H-1):
                if isbg(a[y,x]) and not mask[y,x]: mask[y,x]=True; q.append((x,y))
        for y in range(H):
            for x in (0,W-1):
                if isbg(a[y,x]) and not mask[y,x]: mask[y,x]=True; q.append((x,y))
        while q:
            x,y=q.popleft()
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                nx,ny=x+dx,y+dy
                if 0<=nx<W and 0<=ny<H and not mask[ny,nx] and isbg(a[ny,nx]): mask[ny,nx]=True; q.append((nx,ny))
    else:
        for y in range(H):
            for x in range(W):
                if isbg(a[y,x]): mask[y,x]=True
    a[mask]=[0,0,0,0]
    return Image.fromarray(a.astype(np.uint8),'RGBA')

def crop(im, pad=0):
    bb=im.getbbox()
    if not bb: return im
    x0,y0,x1,y1=bb
    return im.crop((max(0,x0-pad),max(0,y0-pad),min(im.width,x1+pad),min(im.height,y1+pad)))

def prev(im, path, k=8):
    bg=Image.new('RGBA',(im.width*k,im.height*k),(60,60,80,255))
    bg.alpha_composite(im.resize((im.width*k,im.height*k),Image.NEAREST))
    bg.save(path)
