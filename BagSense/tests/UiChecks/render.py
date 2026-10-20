import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image

for path in Path(sys.argv[1]).glob('bagsense-*.json'):
    data = json.loads(path.read_text())
    width, height = data['Width'], data['Height']
    texture = np.fromfile(path.parent / f"font-{data['Font']}.rgba", dtype=np.uint8).reshape(data['TextureHeight'], data['TextureWidth'], 4).astype(float)/255
    textures = {1: texture}
    for info in data.get('Textures', []):
        textures[info['Id']] = np.fromfile(path.parent / info['File'], dtype=np.uint8).reshape(info['Height'], info['Width'], 4).astype(float)/255
    target = np.zeros((height, width, 3),dtype=float)
    for draw in data['Lists']:
        vertices = np.array(draw['Vertices'])
        indices = np.array(draw['Indices'])
        packed = vertices[:,4].astype(np.uint32)
        colors = np.stack([(packed >> shift)&255 for shift in (0,8,16,24)],axis=1)/255.
        for command in draw['Commands']:
            texture = textures[command.get('Texture', 1)]
            x0,y0,x1,y1=command['Clip']
            for at in range(command['Offset'],command['Offset']+command['Count'],3):
                ids=indices[at:at+3]+command['VertexOffset']
                tri=vertices[ids,:4]; rgb=colors[ids]
                left=max(0,int(x0),int(np.floor(tri[:,0].min()))); right=min(width,int(x1),int(np.ceil(tri[:,0].max())))
                top=max(0,int(y0),int(np.floor(tri[:,1].min()))); bottom=min(height,int(y1),int(np.ceil(tri[:,1].max())))
                if left>=right or top>=bottom: continue
                a,b,c=tri
                denominator=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                if abs(denominator)<1e-8: continue
                yy,xx=np.mgrid[top:bottom,left:right];xx=xx+.5;yy=yy+.5
                u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/denominator
                v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/denominator
                w=1-u-v; mask=(u>=-1e-7)&(v>=-1e-7)&(w>=-1e-7)
                if not mask.any(): continue
                bary=np.stack([u[mask],v[mask],w[mask]],axis=1)
                uv=bary @ tri[:,2:4]
                tx=np.clip((uv[:,0]*texture.shape[1]).astype(int),0,texture.shape[1]-1)
                ty=np.clip((uv[:,1]*texture.shape[0]).astype(int),0,texture.shape[0]-1)
                source=(bary@rgb)*texture[ty,tx]
                dest=target[top:bottom,left:right]
                alpha=source[:,3:4]
                dest[mask]=source[:,:3]*alpha+dest[mask]*(1-alpha)
    Image.fromarray(np.clip(target*255,0,255).astype(np.uint8)).save(path.with_suffix('.png'))
    print(path.with_suffix('.png'))
