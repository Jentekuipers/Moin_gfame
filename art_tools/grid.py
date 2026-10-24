import sys, math
from PIL import Image
import numpy as np
def boundaries(a, axis):
    # positions where a pixel differs (strongly) from its predecessor along axis
    d=np.abs(np.diff(a.astype(int),axis=axis)).sum(axis=2)
    pos=np.where(d>40)
    return pos[axis]+1
def fit(b, n):
    best=None
    for s in np.arange(4.0,12.0,0.01):
        ph=(b % s)/s
        # circular mean of phase
        ang=ph*2*np.pi
        c=np.cos(ang).mean(); si=np.sin(ang).mean()
        R=math.hypot(c,si)
        score=R - 0.0*s
        if best is None or score>best[0]: best=(score,s,(math.atan2(si,c)/(2*np.pi))%1*s)
    return best
def est(path):
    im=Image.open(path).convert('RGBA'); a=np.array(im)
    bx=boundaries(a,1); by=boundaries(a,0)
    fx=fit(bx,a.shape[1]); fy=fit(by,a.shape[0])
    return fx,fy
if __name__=='__main__':
    for p in sys.argv[1:]:
        print(p, est(p))
