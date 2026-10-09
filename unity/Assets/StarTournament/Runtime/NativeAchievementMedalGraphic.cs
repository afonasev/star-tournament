using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Small procedural medal with a restrained, award-themed center mark.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NativeAchievementMedalGraphic : Graphic
    {
        public enum ThemeKind { Combat, Movement, Heal, Bonus, Aim, Relations, Distance, Survival, Ammo }
        public ThemeKind Theme;
        const int CircleSteps=28;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float u=Mathf.Min(r.width,r.height);Vector2 c=r.center+new Vector2(0,-u*.04f);
            Color ribbon=new Color(color.r,color.g,color.b,.65f);
            float stroke=u*.022f;
            foreach(float direction in new[]{-1f,1f})
            {
                Vector2 a=c+new Vector2(direction*u*.06f,-u*.20f),b=c+new Vector2(direction*u*.20f,-u*.48f),d=c+new Vector2(direction*u*.01f,-u*.39f);
                Line(vh,a,b,stroke,ribbon);Line(vh,b,d,stroke,ribbon);Line(vh,d,c+new Vector2(direction*u*.01f,-u*.25f),stroke,ribbon);
            }
            Disc(vh,c,u*.32f,new Color(color.r,color.g,color.b,.10f));
            Ring(vh,c,u*.32f,stroke,color);Ring(vh,c,u*.265f,stroke*.65f,ribbon);
            Color mark=color;
            switch(Theme)
            {
                case ThemeKind.Ammo:
                    foreach(float x in new[]{-.065f,.065f})
                    {
                        Vector2 bottom=c+new Vector2(x*u,-u*.12f),tip=c+new Vector2(x*u,u*.13f);
                        Vector2 left=bottom+new Vector2(-u*.035f,0),right=bottom+new Vector2(u*.035f,0);
                        Line(vh,left,right,stroke,mark);Line(vh,left,tip+new Vector2(-u*.035f,-u*.05f),stroke,mark);
                        Line(vh,right,tip+new Vector2(u*.035f,-u*.05f),stroke,mark);
                        Line(vh,tip+new Vector2(-u*.035f,-u*.05f),tip,stroke,mark);Line(vh,tip,tip+new Vector2(u*.035f,-u*.05f),stroke,mark);
                        Line(vh,left+Vector2.up*u*.065f,right+Vector2.up*u*.065f,stroke,mark);
                    }
                    break;
                case ThemeKind.Movement:
                    Line(vh,c+new Vector2(-u*.12f,-u*.08f),c+new Vector2(u*.12f,-u*.08f),u*.022f,mark);
                    Line(vh,c+new Vector2(-u*.10f,-u*.08f),c+new Vector2(0,u*.11f),u*.022f,mark);
                    Line(vh,c+new Vector2(0,u*.11f),c+new Vector2(u*.12f,-u*.08f),u*.022f,mark);break;
                case ThemeKind.Heal:
                    Quad(vh,c+new Vector2(-u*.045f,-u*.15f),c+new Vector2(u*.045f,u*.15f),mark);
                    Quad(vh,c+new Vector2(-u*.15f,-u*.045f),c+new Vector2(u*.15f,u*.045f),mark);break;
                case ThemeKind.Bonus:
                    Ring(vh,c,u*.13f,stroke,mark);Ring(vh,c,u*.065f,stroke,mark);break;
                case ThemeKind.Aim:
                    Ring(vh,c,u*.15f,stroke,mark);Ring(vh,c,u*.08f,stroke,mark);Disc(vh,c,u*.022f,mark);break;
                case ThemeKind.Relations:
                    Ring(vh,c+new Vector2(-u*.07f,0),u*.085f,stroke,mark);Ring(vh,c+new Vector2(u*.07f,0),u*.085f,stroke,mark);break;
                case ThemeKind.Distance:
                    Line(vh,c+new Vector2(-u*.14f,-u*.08f),c+new Vector2(u*.14f,-u*.08f),u*.022f,mark);
                    Line(vh,c+new Vector2(-u*.14f,-u*.08f),c+new Vector2(u*.11f,u*.10f),u*.022f,mark);
                    Line(vh,c+new Vector2(u*.11f,u*.10f),c+new Vector2(u*.11f,-u*.08f),u*.022f,mark);break;
                case ThemeKind.Survival:
                    Line(vh,c+new Vector2(-u*.12f,0),c+new Vector2(-u*.04f,0),u*.022f,mark);
                    Line(vh,c+new Vector2(-u*.04f,0),c+new Vector2(0,u*.11f),u*.022f,mark);
                    Line(vh,c+new Vector2(0,u*.11f),c+new Vector2(u*.07f,-u*.12f),u*.022f,mark);
                    Line(vh,c+new Vector2(u*.07f,-u*.12f),c+new Vector2(u*.13f,-u*.12f),u*.022f,mark);break;
                default:
                    Line(vh,c+new Vector2(-u*.12f,-u*.12f),c+new Vector2(u*.12f,u*.12f),u*.022f,mark);
                    Line(vh,c+new Vector2(-u*.12f,u*.12f),c+new Vector2(u*.12f,-u*.12f),u*.022f,mark);break;
            }
        }
        static void Ring(VertexHelper vh,Vector2 center,float radius,float width,Color ink)
        {
            for(int i=0;i<CircleSteps;i++)
            {
                float a=2*Mathf.PI*i/CircleSteps,b=2*Mathf.PI*(i+1)/CircleSteps;
                Line(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,ink);
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
