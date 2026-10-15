using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class WellLabDetail
    {
        // Cosmetic subparts fit the authored solid body. Props stay on the top surface;
        // they neither create colliders nor alter the physics/navigation obstruction.
        public void Furniture(ArenaSolid solid)
        {
            var forward=WellFurniturePlan.Front(solid);var rotation=Quaternion.LookRotation(forward);
            bool acrossX=Mathf.Abs(forward.x)>.5f;
            float w=acrossX?solid.Size.z:solid.Size.x,h=solid.Size.y,d=acrossX?solid.Size.x:solid.Size.z;
            var origin=solid.Position-Vector3.up*h/2;
            int department=Mathf.Clamp(Mathf.FloorToInt(origin.y/WellArenaCatalog.Storey),0,2);
            var accent=department==0?porcelain:department==1?blue:amber;
            void Part(Material m,float x,float y,float z,float sx,float sy,float sz)=>FaceBox(m,origin,rotation,new Vector3(x,y,z),new Vector3(sx,sy,sz));
            void Soft(Material m,float x,float y,float z,float sx,float sy,float sz)=>Round(m,origin,rotation,new Vector3(x,y,z),new Vector3(sx,sy,sz));
            // A closed plinth and full carcass correspond to the single authoritative AABB.
            bool cabinet=solid.Surface.EndsWith("cabinet"),shippingCase=solid.Surface.EndsWith("case");
            float capBottom=h-(cabinet?.07f:shippingCase?.13f:.09f);
            Part(trim,0,.055f,0,w,.11f,d);
            Soft(shippingCase?accent:panel,0,(capBottom+.11f)/2,0,w,capBottom-.11f,d);
            if(solid.Surface.EndsWith("cabinet"))
            {
                foreach(int side in new[]{-1,1})
                {
                    Soft(porcelain,side*w*.245f,h*.52f,d/2+.004f,w*.475f,h-.22f,.025f);
                    Part(trim,side*.07f,h*.51f,d/2+.025f,.025f,.30f,.025f);
                    Part(alloy,side*.07f,h*.51f,d/2+.043f,.025f,.18f,.025f);
                }
                Part(accent,0,h-.26f,d/2+.02f,w-.12f,.08f,.014f);
                Part(porcelain,-w*.25f,h-.44f,d/2+.023f,.32f,.10f,.018f);
                for(int i=0;i<4;i++)Part(trim,0,.20f+i*.045f,d/2+.023f,w*.65f,.018f,.014f);
                Soft(alloy,0,h-.035f,0,w,.07f,d);
            }
            else if(solid.Surface.EndsWith("case"))
            {
                Soft(alloy,0,h-.065f,0,w,.13f,d);
                foreach(int side in new[]{-1,1})
                {
                    Part(trim,side*(w/2-.09f),h/2,d/2+.015f,.10f,h-.06f,.04f);
                    Part(alloy,side*w*.3f,h-.18f,d/2+.03f,.09f,.18f,.035f);
                }
                Part(trim,0,h*.52f,d/2+.025f,.42f,.16f,.028f);
                Part(alloy,0,h*.54f,d/2+.047f,.28f,.045f,.024f);
                Part(porcelain,-w*.28f,h*.36f,d/2+.025f,.29f,.16f,.018f);
                // Small shipping-label bars rather than unreadable micro-text.
                for(int i=0;i<6;i++)Part(trim,-w*.28f-.1f+i*.035f,h*.36f,d/2+.036f,.014f,.10f,.006f);
            }
            else
            {
                Soft(trim,0,h-.045f,0,w,.09f,d);
                Soft(porcelain,0,h-.012f,0,w-.04f,.035f,d-.03f);
                // Closed drawer bases: no fake empty legroom under a solid collision box.
                for(int col=0;col<3;col++)for(int row=0;row<2;row++)
                {
                    float x=(col-1)*w/3,y=.34f+row*.43f;
                    Soft(col==2?accent:porcelain,x,y,d/2+.005f,w/3-.045f,.39f,.024f);
                    Part(alloy,x,y+.10f,d/2+.031f,w*.16f,.03f,.035f);
                }
                // A workstation, not a random pile: instrument left, task tray right, documents at the front.
                float instrument=-w*.24f;
                Soft(panel,instrument,h+.24f,-.12f,.64f,.48f,.46f);
                Soft(trim,instrument,h+.27f,.118f,.54f,.33f,.027f);
                Round(display,origin,rotation,new Vector3(instrument,h+.27f,.137f),new Vector3(.49f,.28f,.006f));
                for(int i=0;i<3;i++)Turned(alloy,origin,rotation,new Vector3(instrument-.18f+i*.18f,h+.065f,.14f),.055f,.025f,true);
                Soft(alloy,w*.24f,h+.04f,-.10f,.68f,.08f,.52f);
                if(department==0)
                {
                    for(int i=0;i<3;i++)
                    {
                        var p=new Vector3(w*.24f-.20f+i*.20f,h+.20f,-.10f);
                        Turned(porcelain,origin,rotation,p,.14f,.28f);Turned(blue,origin,rotation,p+Vector3.up*.16f,.16f,.045f);
                    }
                }
                else if(department==1)
                {
                    Turned(alloy,origin,rotation,new Vector3(w*.24f,h+.20f,-.10f),.36f,.25f);
                    Turned(trim,origin,rotation,new Vector3(w*.24f,h+.34f,-.10f),.21f,.08f);
                }
                else
                {
                    Part(amber,w*.24f,h+.10f,-.10f,.52f,.045f,.38f);
                    for(int i=0;i<3;i++)Part(trim,w*.24f-.16f+i*.16f,h+.145f,-.10f,.10f,.045f,.20f);
                }
                // Deliberately offset folders supply signs of work without floor clutter.
                var paperRotation=rotation*Quaternion.Euler(0,-12,0);
                Round(blue,origin,paperRotation,new Vector3(w*.08f,h+.025f,.37f),new Vector3(.38f,.045f,.28f));
                Round(porcelain,origin,paperRotation,new Vector3(w*.08f+.018f,h+.051f,.36f),new Vector3(.34f,.009f,.25f));
            }
        }
    }
}
