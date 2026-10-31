// Frozen texture routine from main50fa41f3; correctness oracle, never runtime.
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    static class LegacyWellTextureReference
    {
        const int TextureSize=256;
        static float Grain(int x,int y)
        {uint h=unchecked((uint)x*73856093u^(uint)y*19349663u);h^=h>>13;h*=1274126177u;return (h&1023)/1023f-.5f;}
        static float Noise(int x,int y,int cell)
        {
            int count=TextureSize/cell,gx=x/cell,gy=y/cell;float tx=(x%cell)/(float)cell,ty=(y%cell)/(float)cell;
            tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
            return Mathf.Lerp(Mathf.Lerp(Grain(gx,gy),Grain((gx+1)%count,gy),tx),Mathf.Lerp(Grain(gx,(gy+1)%count),Grain((gx+1)%count,(gy+1)%count),tx),ty);
        }
        static float Height(string kind,int x,int y)
        {
            x=(x%TextureSize+TextureSize)%TextureSize;y=(y%TextureSize+TextureSize)%TextureSize;
            float u=x/(float)TextureSize,v=y/(float)TextureSize;
            float cloud=Mathf.Sin(u*Mathf.PI*4+Mathf.Sin(v*Mathf.PI*6))*Mathf.Cos(v*Mathf.PI*4);
            // Authored material motifs in one repeat: floor aggregate, large wall panels, perforated ceiling cassettes.
            // Their scale and contrast remain controlled by the existing Well art profile.
            if(kind=="floor-bio")return Noise(x,y,32)*.04f+Noise(x,y,2)*.25f+(Grain(x/3,y/3)>.28f?.45f:0);
            if(kind=="floor-physics")return Noise(x,y,32)*.19f+Noise(x,y,8)*.10f+Grain(x,y)*.03f;
            if(kind=="floor-electronics")
            {
                int px=x%32,py=y%32;float r=new Vector2(px-16,py-16).magnitude;
                return r<7?.11f:r<9?-.05f:Grain(x,y)*.025f;
            }
            if(kind=="floor")return Noise(x,y,32)*.06f+Noise(x,y,8)*.14f+Noise(x,y,2)*.26f+Grain(x,y)*.08f;
            if(kind=="ceiling")
            {
                int px=x%(TextureSize/2),py=y%(TextureSize/4);
                int seam=Mathf.Min(px,TextureSize/2-1-px,py,TextureSize/4-1-py);
                bool hole=seam>8&&x%12<3&&y%12<3;
                return seam<2?-.15f:hole?-.09f:Grain(x,y)*.008f;
            }
            if(kind=="soil")return Noise(x,y,32)*.4f+Noise(x,y,8)*.4f+Noise(x,y,2)*.25f+Grain(x,y)*.18f;
            if(kind=="metal")return Mathf.Sin(y*2.3f)*.05f+Grain(x,y)*.04f;
            int edge=Mathf.Min(x,y,TextureSize-1-x,TextureSize-1-y);
            return (edge<1?-.18f:edge<3?-.05f:0)+cloud*.015f+Grain(x,y)*(kind=="floor"?.04f:.008f);
        }
        public static Texture2D Texture(string kind,ProvingProfile profile,bool normal=false,TextureFormat format=TextureFormat.RGBA32)
        {
            var texture=new Texture2D(TextureSize,TextureSize,format,true,normal){name="Well "+kind+(normal?" relief":" texture"),wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color[TextureSize*TextureSize];
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++)
            {
                float n=Grain(x,y),grain=profile.Get("well.surfaceGrain"),u=x/(float)(TextureSize-1),v=y/(float)(TextureSize-1);
                if(normal)
                {
                    float dx=Height(kind,x-1,y)-Height(kind,x+1,y),dy=Height(kind,x,y-1)-Height(kind,x,y+1);
                    float relief=kind=="floor-bio"?3:kind.StartsWith("floor")?18:10;
                    var direction=new Vector3(dx*grain*relief,dy*grain*relief,1).normalized;
                    // Uncompressed desktop normals use RGB with alpha 1 (URP RG-or-AG multiplies R by A).
                    pixels[y*TextureSize+x]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);continue;
                }
                if(kind=="leaf")
                {
                    float mid=Mathf.Abs(u-.5f),vein=Mathf.Exp(-mid*130);
                    float branch=Mathf.Pow(Mathf.Max(0,Mathf.Cos((v-mid*.6f)*Mathf.PI*28)),18)*Mathf.Clamp01(mid*20);
                    var colour=Color.Lerp(new Color(.065f,.19f,.035f),new Color(.29f,.46f,.10f),Mathf.Clamp01(v*.6f+(1-mid*2)*.35f));
                    pixels[y*TextureSize+x]=colour*(1+vein*.28f+branch*.14f+n*.06f);continue;
                }
                float value=1+n*grain*.4f;
                if(kind=="ceramic")
                {
                    // Full-height enamel sheets: vertical joints dominate, with a narrow horizontal closure.
                    int vertical=Mathf.Min(x,TextureSize-1-x),horizontal=Mathf.Min(y,TextureSize-1-y);
                    if(vertical<2)value-=profile.Get("well.jointContrast");
                    else if(vertical<4)value+=.035f;
                    if(horizontal<1)value-=profile.Get("well.jointContrast")*.35f;
                    value+=Height(kind,x,y)*grain;
                }
                if(kind.StartsWith("floor"))
                {
                    int cell=TextureSize/2,px=x%cell,py=y%cell;
                    int edge=Mathf.Min(px,py,cell-1-px,cell-1-py);
                    if(edge<1)value-=profile.Get("well.jointContrast");else if(edge<2)value+=.035f;
                    value+=Height(kind,x,y)*grain*(kind=="floor-bio"?8:kind=="floor-electronics"?3:6);
                    // Subtle aggregate flecks and panel-to-panel value changes, never gameplay randomness.
                    value+=Grain(x/cell,y/cell)*grain*.5f;
                }
                if(kind=="ceiling")
                {
                    int px=x%(TextureSize/2),py=y%(TextureSize/4);
                    int seam=Mathf.Min(px,TextureSize/2-1-px,py,TextureSize/4-1-py);
                    if(seam<2)value-=profile.Get("well.jointContrast")*1.5f;
                    else if(seam<4)value+=.025f;
                    else if(seam>8&&x%12<3&&y%12<3)value-=profile.Get("well.jointContrast")*.8f;
                    value+=Grain(x,y)*grain*.15f;
                }
                if(kind=="metal")value-=.09f*(.5f+.5f*Mathf.Sin(y*2.3f));
                if(kind=="soil")value=.83f+Height(kind,x,y)*.45f;
                pixels[y*TextureSize+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
    }
}
