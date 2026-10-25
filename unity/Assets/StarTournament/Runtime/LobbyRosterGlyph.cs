using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Small, screen-space device pictograms for lobby participant cards.
    public sealed class LobbyRosterGlyph : MaskableGraphic
    {
        public enum DeviceKind { KeyboardMouse, Gamepad }
        [SerializeField] DeviceKind kind;
        public DeviceKind Kind { get=>kind; set { if(kind==value)return;kind=value;SetVerticesDirty(); } }
        static readonly Color32 Cyan=new Color32(111,226,231,255);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;if(r.width<=0||r.height<=0)return;
            float s=Mathf.Min(r.height/34f,r.width/84f);var center=r.center;
            Vector2 P(float x,float y)=>center+new Vector2((x-42)*s,(y-17)*s);
            if(kind==DeviceKind.KeyboardMouse)
            {
                RectOutline(mesh,Rect.MinMaxRect(P(1,7).x,P(1,7).y,P(55,27).x,P(55,27).y),2*s);
                for(int row=0;row<3;row++)for(int col=0;col<7;col++)
                {
                    float x=6+col*6.7f,y=11+row*5.8f;
                    Line(mesh,P(x,y),P(x+4.2f,y),1.2f*s,Cyan);
                }
                Line(mesh,P(7,5),P(48,5),1.2f*s,Cyan);
                Ellipse(mesh,P(70,17),5.7f*s,9.5f*s,2*s);
                Line(mesh,P(70,22),P(70,17),1.6f*s,Cyan);
            }
            else
            {
                Vector2[] outline={P(13,22),P(20,26),P(29,27),P(36,23),P(48,23),P(55,27),P(64,26),P(71,22),P(75,13),P(73,7),P(68,6),P(60,14),P(24,14),P(16,6),P(11,7),P(9,13)};
                for(int i=0;i<outline.Length;i++)Line(mesh,outline[i],outline[(i+1)%outline.Length],2*s,Cyan);
                Line(mesh,P(23,20),P(23,8),2*s,Cyan);Line(mesh,P(17,14),P(29,14),2*s,Cyan);
                Disc(mesh,P(58,18),1.5f*s,Cyan);Disc(mesh,P(62,22),1.5f*s,Cyan);Disc(mesh,P(66,18),1.5f*s,Cyan);Disc(mesh,P(62,14),1.5f*s,Cyan);
            }
        }
        static void RectOutline(VertexHelper mesh,Rect rect,float width)
        {
            Line(mesh,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),width,Cyan);
            Line(mesh,new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),width,Cyan);
            Line(mesh,new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax),width,Cyan);
            Line(mesh,new Vector2(rect.xMin,rect.yMax),new Vector2(rect.xMin,rect.yMin),width,Cyan);
        }
        static void Ellipse(VertexHelper mesh,Vector2 c,float rx,float ry,float width)
        {
            const int n=16;var prior=c+new Vector2(rx,0);
            for(int i=1;i<=n;i++){float a=i*Mathf.PI*2/n;var next=c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);Line(mesh,prior,next,width,Cyan);prior=next;}
        }
        static void Disc(VertexHelper mesh,Vector2 c,float radius,Color color)
        {
            const int n=10;int first=mesh.currentVertCount;mesh.AddVert(c,color,Vector2.zero);
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
            for(int i=0;i<n;i++)mesh.AddTriangle(first,first+1+i,first+1+(i+1)%n);
        }
        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x)*(width*.5f);int first=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
