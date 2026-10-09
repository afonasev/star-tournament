using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Quiet rounded award card with a hairline frame and tier accent.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NativeAchievementCardGraphic : Graphic
    {
        public Color Accent;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();const float radius=5;
            var outline=new Vector2[32];
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner==0||corner==3?rect.xMax-radius:rect.xMin+radius,corner<2?rect.yMax-radius:rect.yMin+radius);
                for(int step=0;step<8;step++)
                {
                    float angle=(corner*90+step*90f/7)*Mathf.Deg2Rad;
                    outline[corner*8+step]=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                }
            }
            vh.AddVert(rect.center,color,Vector2.zero);
            foreach(var point in outline)vh.AddVert(point,color,Vector2.zero);
            for(int i=0;i<outline.Length;i++)vh.AddTriangle(0,i+1,(i+1)%outline.Length+1);
            for(int i=0;i<outline.Length;i++)
            {
                Vector2 a=outline[i],b=outline[(i+1)%outline.Length],direction=(b-a).normalized,side=new Vector2(-direction.y,direction.x)*.5f;
                Color ink=new Color32(64,84,106,255);if(a.x<rect.xMin+1&&b.x<rect.xMin+1)ink=Accent;
                int n=vh.currentVertCount;vh.AddVert(a+side,ink,Vector2.zero);vh.AddVert(a-side,ink,Vector2.zero);vh.AddVert(b-side,ink,Vector2.zero);vh.AddVert(b+side,ink,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
