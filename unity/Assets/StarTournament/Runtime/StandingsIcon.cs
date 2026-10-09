using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    /// <summary>Authored vector glyphs, independent of installed font/emoji coverage.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StandingsIcon : MaskableGraphic
    {
        public enum Symbol { Crosshair, Handshake, Skull, Outgoing, Incoming, Star, Pulse, Crown, Human, Bot, Shield, Fixture, Percent, Helmet }
        Symbol symbol;
        public Symbol Kind { get=>symbol; set{symbol=value;SetVerticesDirty();} }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Glyph contours/stroke are technical vector artwork, not layout or gameplay tuning.
            switch(symbol)
            {
                case Symbol.Crosshair:
                    Ring(mesh,.5f,.5f,.28f);Line(mesh,.5f,.05f,.5f,.32f);Line(mesh,.5f,.68f,.5f,.95f);Line(mesh,.05f,.5f,.32f,.5f);Line(mesh,.68f,.5f,.95f,.5f);break;
                case Symbol.Handshake:
                    Path(mesh,new[]{V(.05f,.59f),V(.21f,.8f),V(.43f,.67f),V(.55f,.74f),V(.78f,.67f),V(.92f,.43f),V(.76f,.2f),V(.62f,.17f),V(.27f,.42f),V(.18f,.3f),V(.05f,.59f)});
                    Path(mesh,new[]{V(.43f,.67f),V(.32f,.51f),V(.42f,.43f),V(.58f,.55f),V(.81f,.35f)});
                    Line(mesh,.27f,.42f,.58f,.17f);Line(mesh,.36f,.35f,.28f,.25f);Line(mesh,.45f,.29f,.37f,.18f);Line(mesh,.55f,.23f,.47f,.12f);Line(mesh,.12f,.49f,.28f,.73f);Line(mesh,.78f,.67f,.66f,.54f);break;
                case Symbol.Skull:
                    Path(mesh,new[]{V(.32f,.12f),V(.32f,.36f),V(.2f,.45f),V(.17f,.67f),V(.26f,.84f),V(.5f,.9f),V(.74f,.84f),V(.83f,.67f),V(.8f,.45f),V(.68f,.36f),V(.68f,.12f),V(.32f,.12f)});
                    Ring(mesh,.35f,.59f,.065f);Ring(mesh,.65f,.59f,.065f);Line(mesh,.5f,.43f,.5f,.36f);Line(mesh,.45f,.12f,.45f,.27f);Line(mesh,.56f,.12f,.56f,.27f);break;
                case Symbol.Outgoing: Path(mesh,new[]{V(.1f,.24f),V(.4f,.52f),V(.56f,.39f),V(.88f,.77f)});Path(mesh,new[]{V(.63f,.77f),V(.88f,.77f),V(.88f,.52f)});break;
                case Symbol.Incoming: Path(mesh,new[]{V(.1f,.77f),V(.4f,.48f),V(.56f,.61f),V(.88f,.23f)});Path(mesh,new[]{V(.63f,.23f),V(.88f,.23f),V(.88f,.48f)});break;
                case Symbol.Star:
                    var points=new Vector2[11];for(int i=0;i<=10;i++){float angle=(90+i*36)*Mathf.Deg2Rad;float radius=i%2==0?.44f:.21f;points[i]=V(.5f+Mathf.Cos(angle)*radius,.5f+Mathf.Sin(angle)*radius);}Path(mesh,points);break;
                case Symbol.Pulse: Path(mesh,new[]{V(.05f,.5f),V(.3f,.5f),V(.42f,.82f),V(.57f,.19f),V(.68f,.5f),V(.95f,.5f)});break;
                case Symbol.Crown: Path(mesh,new[]{V(.18f,.25f),V(.09f,.74f),V(.32f,.56f),V(.5f,.9f),V(.68f,.56f),V(.91f,.74f),V(.82f,.25f),V(.18f,.25f)});Line(mesh,.19f,.12f,.81f,.12f);break;
                case Symbol.Human: Path(mesh,new[]{V(.08f,.26f),V(.17f,.7f),V(.38f,.75f),V(.44f,.65f),V(.56f,.65f),V(.62f,.75f),V(.83f,.7f),V(.92f,.26f),V(.72f,.24f),V(.61f,.43f),V(.39f,.43f),V(.28f,.24f),V(.08f,.26f)});Line(mesh,.23f,.56f,.39f,.56f);Line(mesh,.31f,.48f,.31f,.64f);Ring(mesh,.72f,.57f,.025f);break;
                case Symbol.Bot: Path(mesh,new[]{V(.17f,.22f),V(.17f,.71f),V(.83f,.71f),V(.83f,.22f),V(.17f,.22f)});Line(mesh,.5f,.71f,.5f,.88f);Line(mesh,.07f,.35f,.07f,.59f);Line(mesh,.93f,.35f,.93f,.59f);Ring(mesh,.34f,.51f,.045f);Ring(mesh,.66f,.51f,.045f);Line(mesh,.36f,.32f,.64f,.32f);break;
                case Symbol.Shield: Path(mesh,new[]{V(.5f,.08f),V(.16f,.32f),V(.16f,.77f),V(.5f,.93f),V(.84f,.77f),V(.84f,.32f),V(.5f,.08f)});break;
                case Symbol.Helmet:
                    Path(mesh,new[]{V(.15f,.48f),V(.15f,.62f),V(.22f,.82f),V(.42f,.9f),V(.67f,.88f),V(.82f,.72f),V(.85f,.46f),V(.67f,.3f),V(.67f,.13f),V(.36f,.13f),V(.36f,.4f),V(.15f,.4f),V(.15f,.48f)});
                    Path(mesh,new[]{V(.15f,.58f),V(.73f,.58f),V(.73f,.4f),V(.36f,.4f)});Line(mesh,.51f,.25f,.67f,.25f);break;
                case Symbol.Percent: Ring(mesh,.25f,.75f,.13f);Ring(mesh,.75f,.25f,.13f);Line(mesh,.2f,.15f,.8f,.85f);break;
                case Symbol.Fixture: Line(mesh,.18f,.18f,.82f,.82f);Line(mesh,.18f,.82f,.82f,.18f);break;
            }
            // Center the visible vector contour, including stroke, rather than its nominal glyph box.
            float left=float.MaxValue,right=float.MinValue;var vertex=UIVertex.simpleVert;
            for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);left=Mathf.Min(left,vertex.position.x);right=Mathf.Max(right,vertex.position.x);}
            if(mesh.currentVertCount>0)
            {
                float shift=GetPixelAdjustedRect().center.x-(left+right)*.5f;
                for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);vertex.position.x+=shift;mesh.SetUIVertex(vertex,i);}
            }
        }
        static Vector2 V(float x,float y)=>new Vector2(x,y);
        void Path(VertexHelper mesh,Vector2[] points){for(int i=1;i<points.Length;i++)Line(mesh,points[i-1].x,points[i-1].y,points[i].x,points[i].y);}
        void Ring(VertexHelper mesh,float x,float y,float radius){var points=new Vector2[25];for(int i=0;i<=24;i++){float a=i*Mathf.PI/12;points[i]=V(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);}Path(mesh,points);}
        void Line(VertexHelper mesh,float ax,float ay,float bx,float by)
        {
            var rect=GetPixelAdjustedRect();var a=new Vector2(rect.xMin+ax*rect.width,rect.yMin+ay*rect.height);var b=new Vector2(rect.xMin+bx*rect.width,rect.yMin+by*rect.height);
            Vector2 n=(b-a).normalized; n=new Vector2(-n.y,n.x)*Mathf.Min(rect.width,rect.height)*.035f;
            int first=mesh.currentVertCount;foreach(var p in new[]{a-n,a+n,b+n,b-n}){var v=UIVertex.simpleVert;v.position=p;v.color=color;mesh.AddVert(v);}mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
