using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public sealed partial class OrbitalLeaguePresentation
    {
        Material northRoute,southRoute,sideRoute;
        bool HasWayfinding=>p.Id=="orbital-league-ring-v1";
        void InitializeWayfinding()
        {
            if(!HasWayfinding)return;
            northRoute=Flat("North / red navigation",Palette("north"));
            southRoute=Flat("South / yellow navigation",Palette("south"));
            sideRoute=Flat("Outer sides / violet navigation",Palette("sides"));
        }
        string Route(Vector3 at)
        {
            if(!HasWayfinding)return "central";
            float x=Mathf.Abs(at.x),z=Mathf.Abs(at.z);
            if(x<p.Get("wayfinding.centralHalfWidth") && z<p.Get("wayfinding.centralHalfLength"))return "central";
            if(z>=p.Get("wayfinding.outerCorner") || (x<p.Get("wayfinding.sideStart") && z>=p.Get("wayfinding.centralHalfLength")))
                return at.z>0?"north":"south";
            return "sides";
        }
        Material RouteMaterial(Vector3 at)
        {
            switch(Route(at)){case "north":return northRoute;case "south":return southRoute;case "sides":return sideRoute;default:return cyan;}
        }
        Color RouteColor(Vector3 at)=>Palette(Route(at)=="central"?"ink":Route(at));
        void AccentBox(Vector3 position,Vector3 size,Quaternion rotation)
        {
            if(!HasWayfinding){Box(cyan,position,size,rotation);return;}
            // Split long markings exactly at authored region boundaries, including rotated walls.
            int axis=size.x>=size.z?0:2;float length=size[axis];Vector3 direction=rotation*(axis==0?Vector3.right:Vector3.forward);
            var cuts=new List<float>{-length/2,length/2};
            void Cut(int worldAxis,float boundary)
            {
                if(Mathf.Abs(direction[worldAxis])<.0001f)return; // Parallel-plane intersection tolerance.
                float t=(boundary-position[worldAxis])/direction[worldAxis];if(t>-length/2 && t<length/2)cuts.Add(t);
            }
            foreach(int signum in new[]{-1,1})
            {
                Cut(0,signum*p.Get("wayfinding.centralHalfWidth"));Cut(0,signum*p.Get("wayfinding.sideStart"));
                Cut(2,signum*p.Get("wayfinding.centralHalfLength"));Cut(2,signum*p.Get("wayfinding.outerCorner"));
            }
            cuts.Sort();
            for(int i=1;i<cuts.Count;i++)
            {
                float span=cuts[i]-cuts[i-1];if(span<Epsilon)continue;
                var center=position+direction*((cuts[i]+cuts[i-1])/2);var segment=size;segment[axis]=span;
                Box(RouteMaterial(center),center,segment,rotation);
            }
        }
        void WayfindingSigns()
        {
            if(!HasWayfinding)return;
            for(int i=0;i<16;i++)
            {
                string key="wayfinding.sign-"+i;var position=Position(key);var rotation=Quaternion.Euler(0,p.Get(key+".yaw"),0);
                string region=Route(position),title=region=="north"?"NORTH  /  02":region=="south"?"SOUTH  /  04":position.x<0?"WEST  /  03":"EAST  /  01";
                float width=p.Get("wayfinding.panelWidth"),height=p.Get("wayfinding.panelHeight");
                // Rotation convention matches Label: local +Z points out from the supporting wall.
                Vector3 outward=rotation*Vector3.forward;
                Box(trim,position,new Vector3(width,height,Epsilon),rotation);
                AccentBox(position+outward*Epsilon*2+Vector3.down*(height/2-p.Get("wayfinding.panelStripe")/2),new Vector3(width,p.Get("wayfinding.panelStripe"),Epsilon),rotation);
                Label(title,position+outward*Epsilon*4,rotation,p.Get("wayfinding.labelScale"));
            }
        }
    }
    public sealed partial class ProvingProfile
    {
        static void WayfindingDescriptors(ProvingProfile p)
        {
            void V(string key,string label,string effect,string unit,float min,float max,float step,float value)
                =>p.Add("wayfinding."+key,"wayfinding",label,effect,unit,min,max,step,value);
            V("centralHalfWidth","Полуширина центра","Граница синей центральной зоны на плане.","m",8,20,.5f,16);
            V("centralHalfLength","Полудлина центра","Граница северной и южной цветовых зон у центра.","m",8,18,.5f,12);
            V("sideStart","Внешняя боковая зона","Граница фиолетовой внешней галереи по X.","m",24,32,.5f,28);
            V("outerCorner","Поворот внешнего кольца","Переход фиолетовых боков в север/юг по Z.","m",24,32,.5f,28);
            V("lowerBandY","Нижний настенный пояс","Высота полос на нижнем маршруте; цвет зависит от стороны карты.","m",.6f,2.4f,.05f,1.25f);
            V("upperBandY","Верхний настенный пояс","Высота полос на верхнем маршруте; цвет зависит от стороны карты.","m",4.6f,6.4f,.05f,5.25f);
            V("bandWidth","Ширина настенной полосы","Толщина цветового ориентира на непрозрачных стенах.","m",.05f,.4f,.01f,.18f);
            V("panelWidth","Ширина указателя","Ширина настенной панели с названием сектора.","m",2,6,.1f,4.4f);
            V("panelHeight","Высота указателя","Высота тёмной панели под цветной надписью.","m",.4f,1.2f,.05f,.8f);
            V("panelStripe","Полоса указателя","Высота цветной кромки панели.","m",.03f,.15f,.01f,.07f);
            V("labelScale","Размер названия зоны","Масштаб надписей относительно общего размера маркировки.","ratio",.5f,1.5f,.05f,1f);
            void ColorValue(string key,Color value)
            {for(int i=0;i<3;i++)p.Add("color."+key+"."+new[]{"r","g","b"}[i],"wayfinding",key+" / "+new[]{"R","G","B"}[i],"Цвет линии, панели и названия стороны карты.","ratio",0,1,.005f,value[i]);}
            ColorValue("north",new Color(.95f,.10f,.13f));ColorValue("south",new Color(1,.78f,.035f));ColorValue("sides",new Color(.65f,.26f,.95f));
            var positions=new[]{
                new Vector3(0,2.1f,23.78f),new Vector3(0,2.1f,24.22f),new Vector3(0,2.1f,-23.78f),new Vector3(0,2.1f,-24.22f),
                new Vector3(-27.78f,2.1f,9),new Vector3(-27.78f,2.1f,-9),new Vector3(27.78f,2.1f,9),new Vector3(27.78f,2.1f,-9),
                new Vector3(-28.22f,6.1f,15),new Vector3(-28.22f,6.1f,-15),new Vector3(28.22f,6.1f,15),new Vector3(28.22f,6.1f,-15),
                new Vector3(-28.22f,3,7),new Vector3(-28.22f,3,-7),new Vector3(28.22f,3,7),new Vector3(28.22f,3,-7)};
            var yaw=new[]{180f,0,0,180,90,90,-90,-90,-90,-90,90,90,-90,-90,90,90};
            for(int i=0;i<positions.Length;i++)
            {
                for(int axis=0;axis<3;axis++)V("sign-"+i+"."+new[]{"x","y","z"}[axis],"Указатель "+i,"Положение панели на непрозрачной стене; не меняет collision.","m",-40,40,.01f,positions[i][axis]);
                V("sign-"+i+".yaw","Поворот указателя "+i,"Направление читаемой стороны панели.","degrees",-180,180,1,yaw[i]);
            }
        }
    }
}
