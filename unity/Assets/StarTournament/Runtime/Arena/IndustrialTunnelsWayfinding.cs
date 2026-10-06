using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class IndustrialTunnelsPresentation
    {
        Material[] sectors;
        Material lettering;
        // Fixed sector positions, equipment parts and glyph strokes are authored asset geometry,
        // tied to industrial-tunnels-v1@1. Readability controls live in the presentation profile.
        void CreateSectorMaterials()
        {
            var colors=new[]{new Color(.22f,.36f,.46f),new Color(.55f,.39f,.17f),new Color(.31f,.40f,.33f),new Color(.48f,.24f,.19f),new Color(.69f,.65f,.52f)};
            sectors=new Material[colors.Length];
            for(int i=0;i<colors.Length;i++)sectors[i]=Material("Sector "+(i+1)+" muted paint",colors[i]*P("wayfinding.paint"));
            lettering=Material("Sector ivory lettering",new Color(.9f,.87f,.73f));
        }
        int SectorAt(Vector3 v)
        {
            float x=Mathf.Abs(v.x),z=Mathf.Abs(v.z),reach=P("wayfinding.approach");
            if(x<=8.25f&&z<=6.25f)return 4;
            if((x>=14-reach&&z>=10&&z<=22.25f)||(x>=21&&x<=26.25f&&z>=10-reach))
                return (v.z>0?0:2)+(v.x>0?1:0);
            return -1;
        }
        void SectorWall(Vector3 face,Vector3 along,Vector3 normal,float length)
        {
            // Split long canonical shells so paint stops at the room/approach boundary.
            int segments=Mathf.CeilToInt(length*2);float step=length/segments;
            for(int i=0;i<segments;i++)
            {
                var c=face+along*((i+.5f)*step-length*.5f);int sector=SectorAt(c);if(sector<0)continue;
                var rot=Quaternion.LookRotation(normal);
                Box(sectors[sector],new Vector3(c.x,1.7f,c.z)+normal*.03f,new Vector3(step+.005f,P("wayfinding.bandHeight"),.04f),rot);
                var pipe=new Vector3(c.x,2.98f,c.z)+normal*.24f;
                Pipe(sectors[sector],pipe-along*step*.5f,pipe+along*step*.5f,.075f);
            }
        }
        void Wayfinding()
        {
            for(int i=0;i<4;i++)
            {
                int sx=i%2==0?-1:1,sz=i<2?1:-1;
                // Two inward-facing boards cover the horizontal and vertical tunnel approaches.
                Board(new Vector3(sx*25.76f,1.7f,sz*19),new Vector3(-sx,0,0),i,P("wayfinding.numberHeight"),false);
                Board(new Vector3(sx*20,1.7f,sz*21.76f),new Vector3(0,0,-sz),i,P("wayfinding.numberHeight"),false);
                Portal(new Vector3(sx*14,3.65f,sz*19),new Vector3(sx,0,0),i,i^1);
                Portal(new Vector3(sx*14,3.65f,sz*13),new Vector3(sx,0,0),i,4);
                Portal(new Vector3(sx*23,3.65f,sz*10),new Vector3(0,0,sz),i,i^2);
                Portal(new Vector3(sx*5,3.65f,sz*6),new Vector3(0,0,-sz),4,i);
                var wall=new Vector3(sx*25.55f,3.15f,sz*14.6f);var rotation=Quaternion.LookRotation(new Vector3(-sx,0,0));
                if(i==0)Pump(wall,rotation,sectors[i]);
                if(i==1)Switchgear(wall,rotation,sectors[i]);
                if(i==2)Fan(wall,rotation,sectors[i]);
                if(i==3)HeatExchanger(wall,rotation,sectors[i]);
            }
            float radius=P("wayfinding.ringRadius");
            // Paint is a flat decal-like mesh, not a physical rim around the pickup.
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
                var start=new Vector3(Mathf.Sin(a)*radius,-.592f,Mathf.Cos(a)*radius);
                var end=new Vector3(Mathf.Sin(b)*radius,-.592f,Mathf.Cos(b)*radius);
                Box(sectors[4],(start+end)*.5f,new Vector3(.12f,.006f,Vector3.Distance(start,end)+.005f),Quaternion.LookRotation(end-start));
            }
            Ring(trim,new Vector3(0,3.95f,0),Vector3.right,Vector3.forward,radius,.12f);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                var c=radial*radius+Vector3.up*3.94f;
                Box(warm,c,new Vector3(.48f,.08f,.18f),Quaternion.Euler(0,a*Mathf.Rad2Deg,0));
                if(i%3==0)Pipe(trim,c,c+Vector3.up*.8f,.035f);
            }
        }
        void Portal(Vector3 c,Vector3 inward,int current,int next)
        {
            // Facing the room: destination. Facing the tunnel: the room being entered.
            float h=P("wayfinding.signHeight");
            Board(c+inward*.09f,inward,next,h,true);
            Board(c-inward*.09f,-inward,current,h,true);
            var across=Vector3.Cross(Vector3.up,inward);var rotation=Quaternion.LookRotation(inward);
            for(int side=-1;side<=1;side+=2)
                Box(sectors[current],c+across*(side*1.95f)-Vector3.up*.9f,new Vector3(.16f,1.25f,.16f),rotation);
        }
        void Board(Vector3 c,Vector3 normal,int sector,float height,bool arrow)
        {
            var rot=Quaternion.LookRotation(-normal);float width=height*(arrow?2.85f:1.7f);
            Box(trim,c,new Vector3(width+.22f,height+.24f,.08f),rot);
            Box(sectors[sector],c+normal*.05f,new Vector3(width,height,.025f),rot);
            var origin=c+normal*.073f;
            if(sector==4)Ring(lettering,origin,rot*Vector3.right,Vector3.up,height*.32f,height*.075f);
            else
            {
                Glyph(origin+rot*Vector3.left*(height*.36f),rot,0,height*.78f);
                Glyph(origin+rot*Vector3.right*(height*.30f),rot,sector+1,height*.78f);
            }
            if(arrow)
            {
                var a=origin+rot*Vector3.left*(height*1.02f);
                Stroke(a+Vector3.down*height*.25f,a+Vector3.up*height*.25f,height*.065f);
                Stroke(a+Vector3.up*height*.25f,a+rot*Vector3.left*height*.18f,height*.065f);
                Stroke(a+Vector3.up*height*.25f,a+rot*Vector3.right*height*.18f,height*.065f);
            }
        }
        void Stroke(Vector3 a,Vector3 b,float width)=>Pipe(lettering,a,b,width*.5f);
        void Glyph(Vector3 c,Quaternion q,int digit,float height)
        {
            // Seven-segment industrial stencil, dimensions proportional to profile height.
            string segments=new[]{"abcedf","bc","abged","abgcd","fgbc"}[digit];
            foreach(char s in segments)
            {
                Vector2 a,b;
                switch(s){case 'a':a=new Vector2(-.22f,.5f);b=new Vector2(.22f,.5f);break;case 'g':a=new Vector2(-.22f,0);b=new Vector2(.22f,0);break;case 'd':a=new Vector2(-.22f,-.5f);b=new Vector2(.22f,-.5f);break;case 'f':a=new Vector2(-.25f,.46f);b=new Vector2(-.25f,.04f);break;case 'e':a=new Vector2(-.25f,-.04f);b=new Vector2(-.25f,-.46f);break;case 'b':a=new Vector2(.25f,.46f);b=new Vector2(.25f,.04f);break;default:a=new Vector2(.25f,-.04f);b=new Vector2(.25f,-.46f);break;}
                Stroke(c+q*new Vector3(a.x,a.y,0)*height,c+q*new Vector3(b.x,b.y,0)*height,height*.09f);
            }
        }
        void Ring(Material material,Vector3 c,Vector3 u,Vector3 v,float radius,float width)
        {
            const int sides=32;
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Pipe(material,c+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius,c+(u*Mathf.Cos(b)+v*Mathf.Sin(b))*radius,width*.5f);}
        }
        void EquipmentBox(Material m,Vector3 c,Quaternion q,Vector3 local,Vector3 size)=>Box(m,c+q*local,size,q);
        void Pump(Vector3 c,Quaternion q,Material paint)
        {
            EquipmentBox(trim,c,q,Vector3.zero,new Vector3(3.8f,2.6f,.18f));
            for(int i=-1;i<=1;i+=2)
            {
                var a=c+q*new Vector3(i*.95f,-.9f,.35f);var b=a+Vector3.up*1.9f;
                Pipe(paint,a,b,.32f);Pipe(steel,a+Vector3.up*.4f,a+Vector3.up*.7f,.42f);
                var valve=a+Vector3.up*1.15f+q*Vector3.forward*.42f;
                Ring(paint,valve,q*Vector3.right,Vector3.up,.46f,.11f);
                Beam(steel,valve-q*Vector3.right*.42f,valve+q*Vector3.right*.42f,.06f,.06f);
                Beam(steel,valve-Vector3.up*.42f,valve+Vector3.up*.42f,.06f,.06f);
                Pipe(paint,b,b+q*Vector3.right*(i*.6f),.25f);
            }
        }
        void Switchgear(Vector3 c,Quaternion q,Material paint)
        {
            for(int i=-1;i<=1;i++)
            {
                var p=new Vector3(i*1.05f,0,.16f);EquipmentBox(paint,c,q,p,new Vector3(.95f,1.6f,.32f));
                EquipmentBox(trim,c,q,p+new Vector3(0,.35f,.18f),new Vector3(.58f,.4f,.03f));
                EquipmentBox(lettering,c,q,p+new Vector3(.28f,-.3f,.19f),new Vector3(.05f,.3f,.04f));
                for(int j=0;j<5;j++)
                {
                    var a=c+q*(p+new Vector3((j-2)*.13f,.8f,0));var b=a+Vector3.up*(.3f+j*.08f);
                    Pipe(j==2?paint:trim,a,b,.045f);Pipe(j==2?paint:trim,b,b+q*Vector3.right*(1.8f-i),.045f);
                }
            }
        }
        void Fan(Vector3 c,Quaternion q,Material paint)
        {
            EquipmentBox(trim,c,q,Vector3.zero,new Vector3(3.7f,3.1f,.16f));
            var hub=c+q*Vector3.forward*.16f;Ring(paint,hub,q*Vector3.right,Vector3.up,1.3f,.2f);
            for(int i=0;i<6;i++)
            {
                var rotation=q*Quaternion.Euler(0,0,i*60);var radial=rotation*Vector3.up;
                Box(steel,hub+radial*.68f,new Vector3(.45f,1.05f,.06f),rotation*Quaternion.Euler(0,0,-22));
            }
            Pipe(paint,hub,hub+q*Vector3.forward*.25f,.25f);
            for(int i=-7;i<=7;i++)EquipmentBox(paint,c,q,new Vector3(i*.23f,0,.46f),new Vector3(.045f,2.85f,.045f));
            for(int i=-1;i<=1;i++)EquipmentBox(paint,c,q,new Vector3(0,i*1.4f,.47f),new Vector3(3.5f,.1f,.05f));
        }
        void HeatExchanger(Vector3 c,Quaternion q,Material paint)
        {
            EquipmentBox(trim,c,q,Vector3.zero,new Vector3(3.7f,2.7f,.18f));
            for(int i=-4;i<=4;i++)
            {
                var a=c+q*new Vector3(i*.35f,-1.1f,.35f);Pipe(paint,a,a+Vector3.up*2.2f,.12f);
            }
            for(int i=-1;i<=1;i+=2)
            {
                var a=c+q*new Vector3(-1.65f,i*1.05f,.35f);Pipe(steel,a,a+q*Vector3.right*3.3f,.2f);
            }
            for(int i=-3;i<=3;i++)EquipmentBox(paint,c,q,new Vector3(0,i*.27f,.25f),new Vector3(3.35f,.07f,.55f));
        }
    }
}
