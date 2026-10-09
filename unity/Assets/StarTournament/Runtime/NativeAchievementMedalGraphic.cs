using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Small procedural medal with a restrained, award-themed center mark.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NativeAchievementMedalGraphic : Graphic
    {
        public enum ThemeKind { Combat, Movement, Heal, Bonus, Aim, Relations, Distance, Survival }
        public ThemeKind Theme;
        const int CircleSteps=28;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float u=Mathf.Min(r.width,r.height);Vector2 c=r.center+new Vector2(0,-u*.04f);
            Color ribbon=new Color(color.r*.48f,color.g*.48f,color.b*.48f,1);
            Quad(vh,new Vector2(c.x-u*.17f,c.y-u*.49f),new Vector2(c.x-u*.01f,c.y-u*.10f),ribbon);
            Quad(vh,new Vector2(c.x+u*.01f,c.y-u*.49f),new Vector2(c.x+u*.17f,c.y-u*.10f),ribbon);
            Disc(vh,c,u*.36f,color);Disc(vh,c,u*.285f,ribbon);Disc(vh,c,u*.235f,color);
            Color mark=new Color(.12f,.17f,.23f,1);
            switch(Theme)
            {
                case ThemeKind.Movement:
                    Line(vh,c+new Vector2(-u*.12f,-u*.08f),c+new Vector2(u*.12f,-u*.08f),u*.035f,mark);
                    Line(vh,c+new Vector2(-u*.10f,-u*.08f),c+new Vector2(0,u*.11f),u*.035f,mark);
                    Line(vh,c+new Vector2(0,u*.11f),c+new Vector2(u*.12f,-u*.08f),u*.035f,mark);break;
                case ThemeKind.Heal:
                    Quad(vh,c+new Vector2(-u*.045f,-u*.15f),c+new Vector2(u*.045f,u*.15f),mark);
                    Quad(vh,c+new Vector2(-u*.15f,-u*.045f),c+new Vector2(u*.15f,u*.045f),mark);break;
                case ThemeKind.Bonus:
                    Disc(vh,c,u*.13f,mark);Disc(vh,c,u*.065f,color);break;
                case ThemeKind.Aim:
                    Disc(vh,c,u*.15f,mark);Disc(vh,c,u*.095f,color);Disc(vh,c,u*.045f,mark);break;
                case ThemeKind.Relations:
                    Disc(vh,c+new Vector2(-u*.07f,0),u*.085f,mark);Disc(vh,c+new Vector2(u*.07f,0),u*.085f,mark);break;
                case ThemeKind.Distance:
                    Line(vh,c+new Vector2(-u*.14f,-u*.08f),c+new Vector2(u*.14f,-u*.08f),u*.035f,mark);
                    Line(vh,c+new Vector2(-u*.14f,-u*.08f),c+new Vector2(u*.11f,u*.10f),u*.035f,mark);
                    Line(vh,c+new Vector2(u*.11f,u*.10f),c+new Vector2(u*.11f,-u*.08f),u*.035f,mark);break;
                case ThemeKind.Survival:
                    Line(vh,c+new Vector2(-u*.12f,0),c+new Vector2(-u*.04f,0),u*.04f,mark);
                    Line(vh,c+new Vector2(-u*.04f,0),c+new Vector2(0,u*.11f),u*.04f,mark);
                    Line(vh,c+new Vector2(0,u*.11f),c+new Vector2(u*.07f,-u*.12f),u*.04f,mark);
                    Line(vh,c+new Vector2(u*.07f,-u*.12f),c+new Vector2(u*.13f,-u*.12f),u*.04f,mark);break;
                default:
                    Line(vh,c+new Vector2(-u*.12f,-u*.12f),c+new Vector2(u*.12f,u*.12f),u*.045f,mark);
                    Line(vh,c+new Vector2(-u*.12f,u*.12f),c+new Vector2(u*.12f,-u*.12f),u*.045f,mark);break;
            }
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Color color)
        {
            int n=vh.currentVertCount;vh.AddVert(new Vector3(a.x,a.y),color,Vector2.zero);vh.AddVert(new Vector3(b.x,a.y),color,Vector2.zero);vh.AddVert(new Vector3(b.x,b.y),color,Vector2.zero);vh.AddVert(new Vector3(a.x,b.y),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Disc(VertexHelper vh,Vector2 center,float radius,Color color)
        {
            int n=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
            for(int i=0;i<CircleSteps;i++){float a=2*Mathf.PI*i/CircleSteps;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
            for(int i=0;i<CircleSteps;i++)vh.AddTriangle(n,n+1+i,n+1+(i+1)%CircleSteps);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 n=(b-a).normalized;Vector2 side=new Vector2(-n.y,n.x)*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a+side,color,Vector2.zero);vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);vh.AddVert(b+side,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
