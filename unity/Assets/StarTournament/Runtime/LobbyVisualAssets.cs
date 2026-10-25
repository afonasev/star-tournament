using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // Deterministic high-resolution UI artwork. No game state, assets or camera tuning.
    public static class LobbyVisualAssets
    {
        static Sprite tintedPanel, panel, rim, scrim, button, cell, filled, emptySlot;
        public static Sprite Panel => panel ? panel : panel=Make("lobby-brushed-panel",256,128,(x,y)=>
        {
            float edge=Mathf.Clamp01(Rounded(x,y,256,128,10)+.5f);
            float light=.13f+.05f*y/128+.05f*Mathf.Exp(-Mathf.Pow((x-75)/120,2));
            float grain=.0025f*Mathf.Sin(y*2.7f+x*.17f);
            return new Color(light+grain,light*1.32f+grain,light*1.65f+grain,edge*.94f);
        });
        public static Sprite TintedPanel=>tintedPanel?tintedPanel:tintedPanel=Make("lobby-player-metal",256,128,(x,y)=>
        {
            float edge=Mathf.Clamp01(Rounded(x,y,256,128,10)+.5f);
            float light=.27f+.13f*y/128+.09f*Mathf.Exp(-Mathf.Pow((x-75)/120,2))+.006f*Mathf.Sin(y*2.7f+x*.17f);
            return new Color(light,light,light,edge*.97f);
        });
        public static Sprite Rim => rim ? rim : rim=Make("lobby-panel-rim",64,64,(x,y)=>
        {float d=Rounded(x,y,64,64,10);return new Color(1,1,1,Mathf.Clamp01(d+.5f)*(1-Mathf.Clamp01(d-1)));},new Vector4(12,12,12,12));
        public static Sprite Scrim => scrim ? scrim : scrim=Make("lobby-map-title-scrim",2,128,(x,y)=>new Color(.015f,.03f,.05f,.92f*Mathf.Pow(1-y/128,1.8f)));
        public static Sprite Button => button ? button : button=Make("lobby-button-bevel",128,128,(x,y)=>
        {
            float d=Vector2.Distance(new Vector2(x,y),new Vector2(64,64));
            float rimLight=d>58 ? .85f+.15f*(y/128) : d>54 ? .32f : .58f+.36f*y/128;
            float highlight=.18f*Mathf.Exp(-Mathf.Pow((x-48)/37,2)-Mathf.Pow((y-94)/12,2));
            return new Color(rimLight+highlight,rimLight+highlight,rimLight+highlight,Mathf.Clamp01(64-d));
        });
        public static Sprite EmptySlot => emptySlot ? emptySlot : emptySlot=Make("lobby-empty-slot-ring",192,192,(x,y)=>
        {float d=Vector2.Distance(new Vector2(x,y),new Vector2(96,96));return new Color(1,1,1,Mathf.Clamp01(94-d)*Mathf.Clamp01(d-91.5f));});
        public static Sprite ScreenCell(bool selected)
        {
            if(selected)return filled?filled:filled=Make("lobby-screen-filled",64,64,(x,y)=>new Color(1,1,1,Mathf.Clamp01(Rounded(x,y,64,64,4))));
            return cell?cell:cell=Make("lobby-screen-outline",64,64,(x,y)=>{float d=Rounded(x,y,64,64,4);return new Color(1,1,1,Mathf.Clamp01(d)*(1-Mathf.Clamp01(d-5)));});
        }
        static float Rounded(float x,float y,float w,float h,float radius)
        {var q=new Vector2(Mathf.Abs(x-w/2)-(w/2-radius),Mathf.Abs(y-h/2)-(h/2-radius));return radius-new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude-Mathf.Min(Mathf.Max(q.x,q.y),0);}
        public static Sprite Make(string name,int width,int height,Func<float,float,Color> sample,Vector4 border=default)
        {
            var tex=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y*width+x]=sample(x+.5f,y+.5f);
            tex.SetPixels(pixels);tex.Apply();return Sprite.Create(tex,new Rect(0,0,width,height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
        }
    }
}
