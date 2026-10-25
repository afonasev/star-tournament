using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Rasterized at 4x nominal size; bilinear sampling gives aligned antialiased strokes.
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
            else
            {
                // Smooth controller silhouette with rounded shoulders and handles.
                var p=new List<Vector2>();Vector2[] anchors={new Vector2(12,5),new Vector2(8,9),new Vector2(12,25),new Vector2(20,30),new Vector2(30,29),new Vector2(35,25),new Vector2(49,25),new Vector2(54,29),new Vector2(64,30),new Vector2(72,25),new Vector2(76,9),new Vector2(72,5),new Vector2(66,7),new Vector2(59,16),new Vector2(25,16),new Vector2(18,7)};
                for(int i=0;i<anchors.Length;i++)for(int j=0;j<8;j++){float t=j/8f;var a=anchors[(i+anchors.Length-1)%anchors.Length];var b=anchors[i];var c=anchors[(i+1)%anchors.Length];var d=anchors[(i+2)%anchors.Length];p.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}p.Add(p[0]);Path(p.ToArray());
                Path(new Vector2(22,18),new Vector2(22,22),new Vector2(18,22),new Vector2(18,25),new Vector2(22,25),new Vector2(22,29),new Vector2(25,29),new Vector2(25,25),new Vector2(29,25),new Vector2(29,22),new Vector2(25,22),new Vector2(25,18),new Vector2(22,18));
                Ellipse(61,20,2,2);Ellipse(66,25,2,2);Ellipse(61,30,2,2);Ellipse(56,25,2,2);
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
            mesh.Clear();var r=GetPixelAdjustedRect();float scale=Mathf.Min(r.width/84,r.height/36);var size=new Vector2(84,36)*scale;
            var a=r.center-size/2;var b=r.center+size/2;
            mesh.AddVert(new Vector3(a.x,a.y),color,new Vector2(0,0));mesh.AddVert(new Vector3(a.x,b.y),color,new Vector2(0,1));mesh.AddVert(new Vector3(b.x,b.y),color,new Vector2(1,1));mesh.AddVert(new Vector3(b.x,a.y),color,new Vector2(1,0));mesh.AddTriangle(0,1,2);mesh.AddTriangle(2,3,0);
        }
    }
}
