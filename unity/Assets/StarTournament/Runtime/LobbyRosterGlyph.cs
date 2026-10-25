using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Keyboard/mouse keeps its procedural strokes; gamepad uses the approved SVG raster.
    public sealed class LobbyRosterGlyph : MaskableGraphic
    {
        public enum DeviceKind { KeyboardMouse, Gamepad }
        [SerializeField] DeviceKind kind;
        static readonly Texture2D[] textures=new Texture2D[2];
        public DeviceKind Kind {get=>kind;set{if(kind==value)return;kind=value;SetMaterialDirty();SetVerticesDirty();}}
        public override Texture mainTexture => Texture(kind);
        static Texture2D Texture(DeviceKind value)
        {
            int index=(int)value;if(textures[index])return textures[index];
            if(value==DeviceKind.Gamepad)
                return textures[index]=Resources.Load<Texture2D>("LobbyIcons/GamepadSilhouette");
            var lines=new List<Vector4>();void L(float x,float y,float a,float b)=>lines.Add(new Vector4(x,y,a,b));
            void Path(params Vector2[] p){for(int i=1;i<p.Length;i++)L(p[i-1].x,p[i-1].y,p[i].x,p[i].y);}
            void Ellipse(float x,float y,float rx,float ry){for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;L(x+rx*Mathf.Cos(a),y+ry*Mathf.Sin(a),x+rx*Mathf.Cos(b),y+ry*Mathf.Sin(b));}}
            void Box(float x,float y,float w,float h){Path(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),new Vector2(x,y));}
            if(value==DeviceKind.KeyboardMouse)
            {
                Box(2,3,52,28);L(3,24,53,24);Box(18,26,20,3);
                for(int row=0;row<3;row++)for(int col=0;col<8;col++)L(7+col*5.7f,19-row*5,9.5f+col*5.7f,19-row*5);
                L(16,5.5f,42,5.5f);Ellipse(70,15,7,12);L(70,27,70,16);L(63,18,77,18);L(70,29,70,33);
            }
            var sprite=LobbyVisualAssets.Make("lobby-device-"+value,336,144,(x,y)=>
            {
                var point=new Vector2(x/4,y/4);float distance=100;
                foreach(var s in lines){var a=new Vector2(s.x,s.y);var b=new Vector2(s.z,s.w);var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/ab.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(point,a+ab*t));}
                return new Color(111/255f,226/255f,231/255f,Mathf.Clamp01((.7f-distance)*4+.5f));
            });return textures[index]=sprite.texture;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();var nominal=kind==DeviceKind.Gamepad?new Vector2(96,64):new Vector2(84,36);float scale=Mathf.Min(r.width/nominal.x,r.height/nominal.y);var size=nominal*scale;
            var a=r.center-size/2;var b=r.center+size/2;
            mesh.AddVert(new Vector3(a.x,a.y),color,new Vector2(0,0));mesh.AddVert(new Vector3(a.x,b.y),color,new Vector2(0,1));mesh.AddVert(new Vector3(b.x,b.y),color,new Vector2(1,1));mesh.AddVert(new Vector3(b.x,a.y),color,new Vector2(1,0));mesh.AddTriangle(0,1,2);mesh.AddTriangle(2,3,0);
        }
    }
}
