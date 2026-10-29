using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class WellArenaPresentation
    {
        public const string WayfindingProfile="well-wayfinding-plaques-v1";
        Material wayfindingFontMaterial,wayfindingEnamel;
        // Millimetre mounting/print offsets are a rendering construction invariant, not navigation tuning.
        const float PlateDepth=.012f,PlateCentre=.008f,PrintOffset=.02f,AccentWidth=.08f;
        WellWayfindingPlate Plaque(string role,string destination,string wall,int floor,Vector3 face,Quaternion rotation,Vector2 size,Vector3 read)
        {
            var go=new GameObject("Well plaque: "+role+" "+floor+" "+wall);go.transform.SetParent(transform,false);go.transform.localPosition=face;go.transform.localRotation=rotation;
            var marker=go.AddComponent<WellWayfindingPlate>();marker.Role=role;marker.Destination=destination;marker.Floor=floor;marker.Size=size;marker.WallId=wall;marker.ReadPoint=read;
            Vector3 normal=rotation*Vector3.back;
            void Part(Material material,float x,float width)
            {
                var centre=face+rotation*new Vector3(x,0,-PlateCentre);
                Box(material,centre,Mathf.Abs(normal.x)>.5f?new Vector3(PlateDepth,size.y,width):new Vector3(width,size.y,PlateDepth));
            }
            // Adjacent pieces form one face: no coplanar accent overlay or duplicate wall skin.
            Part(colours[floor],-size.x/2+AccentWidth/2,AccentWidth);Part(wayfindingEnamel,AccentWidth/2,size.x-AccentWidth);
            return marker;
        }
        void Print(WellWayfindingPlate plaque,string value,Vector2 centre,Vector2 available,float height,bool bold=false,bool left=false)
        {
            var go=new GameObject("Well sign: "+value);go.transform.SetParent(plaque.transform,false);var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=96;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.alignment=left?TextAlignment.Left:TextAlignment.Center;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            if(!signFont){signFont=text.font;Font.textureRebuilt+=RefreshFont;}
            if(!wayfindingFontMaterial)
            {
                // Shared lit ink uses the existing font alpha atlas, and receives the same world light as its plate.
                wayfindingFontMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Well wayfinding lit ink",color=new Color(.08f,.13f,.16f)};
                wayfindingFontMaterial.SetTexture("_BaseMap",signFont.material.mainTexture);wayfindingFontMaterial.SetFloat("_Smoothness",0);
                wayfindingFontMaterial.SetFloat("_AlphaClip",1);wayfindingFontMaterial.SetFloat("_Cutoff",.35f);wayfindingFontMaterial.EnableKeyword("_ALPHATEST_ON");owned.Add(wayfindingFontMaterial);
            }
            var renderer=text.GetComponent<MeshRenderer>();renderer.sharedMaterial=wayfindingFontMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var bounds=renderer.localBounds;float scale=Mathf.Min(height*profile.Get("well.signScale")/bounds.size.y,available.x/bounds.size.x,available.y/bounds.size.y);
            go.transform.localScale=Vector3.one*scale;
            go.transform.localPosition=left?new Vector3(centre.x-available.x/2,centre.y,-PrintOffset)-new Vector3(bounds.min.x,bounds.center.y,bounds.center.z)*scale:new Vector3(centre.x,centre.y,-PrintOffset)-bounds.center*scale;
        }
        void FloorPlaque(WellWayfindingPlate plaque,string department,bool atrium)
        {
            float width=plaque.Size.x,baseHeight=profile.Get("well.signTextHeight");
            Print(plaque,"0"+(plaque.Floor+1),new Vector2(-width/2+.77f,0),new Vector2(1.15f,.79f),baseHeight*(atrium?2.9f:2.5f),true);
            Print(plaque,department,new Vector2(.64f,0),new Vector2(width-1.95f,.73f),baseHeight*(atrium?1.85f:1.4f),true,true);
        }
        void BuildWayfinding(ArenaDefinition definition)
        {
            wayfindingEnamel=Mat("Well wayfinding enamel",new Color(.92f,.92f,.86f));wayfindingEnamel.SetFloat("_Smoothness",.18f);
            for(int floor=0;floor<3;floor++)
            {
                float y=floor*WellArenaCatalog.Storey,reader=profile.Get("well.signReaderHeight"),header=profile.Get("well.signHeaderHeight"),baseHeight=profile.Get("well.signTextHeight");
                string department=new[]{"БИОРАЗРАБОТКИ","ФИЗИКА","ЭЛЕКТРОНИКА"}[floor];
                foreach(int side in new[]{-1,1})
                {
                    var rotation=Quaternion.Euler(0,side>0?0:180,0);var normal=rotation*Vector3.back;
                    var spans=new List<Vector2>();
                    foreach(var cap in definition.Solids.Where(s=>s.Id.StartsWith("f"+floor+"-cap-1"+side)).OrderBy(s=>s.Position.x))
                    {
                        float lo=cap.Position.x-cap.Size.x/2,hi=cap.Position.x+cap.Size.x/2;
                        if(spans.Count>0&&lo<=spans[spans.Count-1].y+.001f){var last=spans[spans.Count-1];spans[spans.Count-1]=new Vector2(last.x,hi);}else spans.Add(new Vector2(lo,hi));
                    }
                    var span=spans.OrderByDescending(s=>s.y-s.x).ThenBy(s=>Mathf.Abs(s.x+s.y)).First();
                    var atrium=new Vector3((span.x+span.y)/2,y+profile.Get("well.signAtriumHeight"),side*(WellArenaCatalog.Pit-.12f));
                    var high=Plaque("atrium-floor","floor","atrium-merged-cap-"+side,floor,atrium,rotation,new Vector2(Mathf.Min(6,span.y-span.x-.6f),1.05f),new Vector3(atrium.x,y+1.7f,-side*13));FloorPlaque(high,department,true);
                    float wallZ=side*(WellArenaCatalog.Edge-.15f);
                    var face=new Vector3(-side*6.5f,y+reader,wallZ);
                    var info=Plaque("lift-floor","floor","f"+floor+"-outer-z"+side,floor,face,rotation,new Vector2(3.9f,1.05f),new Vector3(face.x,y+1.7f,face.z)+normal*5);FloorPlaque(info,department,false);
                    face.x=side*6.5f;
                    var directions=Plaque("lift-directions","stairs-both","f"+floor+"-outer-z"+side,floor,face,rotation,new Vector2(3.9f,.85f),new Vector3(face.x,y+1.7f,face.z)+normal*5);
                    Print(directions,"← ЛЕСТНИЦЫ →\n← ЛАБОРАТОРИИ →",Vector2.zero,new Vector2(3.55f,.57f),baseHeight*2,true);
                    var lift=definition.Solids.First(s=>s.Id=="f"+floor+"-outer-z"+side+"-header");
                    face=new Vector3(lift.Position.x,y+header,wallZ);
                    var liftLabel=Plaque("lift-header","floors-1-2-3",lift.Id,floor,face,rotation,new Vector2(3.6f,.82f),new Vector3(face.x,y+1.7f,face.z)+normal*5);
                    Print(liftLabel,"ЛИФТ\n01 · 02 · 03",Vector2.zero,new Vector2(3.28f,.56f),baseHeight*2,true);
                    var stair=definition.Solids.First(s=>s.Id=="f"+floor+"-outer-x"+side+"-header");var stairRotation=Quaternion.Euler(0,side>0?90:-90,0);var stairNormal=stairRotation*Vector3.back;
                    face=new Vector3(side*(WellArenaCatalog.Edge-.15f),y+header,stair.Position.z);
                    var stairLabel=Plaque("stair-header","floors-above-below",stair.Id,floor,face,stairRotation,new Vector2(3.6f,.82f),new Vector3(face.x,y+1.7f,face.z)+stairNormal*5);
                    Print(stairLabel,"ЛЕСТНИЦА\n"+(floor==0?"↑ 02 · 03":floor==1?"↓ 01     ↑ 03":"↓ 01 · 02"),Vector2.zero,new Vector2(3.28f,.56f),baseHeight*2,true);
                    foreach(int end in new[]{-1,1})
                    {
                        string id=floor==1?"f1-side-"+side+"-"+end+"-front-header":"f"+floor+"-cap-0"+side+(end<0?1:4);
                        var wall=definition.Solids.First(s=>s.Id==id);var outward=Vector3.right*side;var r=Quaternion.LookRotation(-outward);
                        face=new Vector3(wall.Position.x+side*wall.Size.x/2,y+header,wall.Position.z);
                        var route=Plaque("gallery-direction",end<0?"lift-south":"lift-north",wall.Id,floor,face,r,new Vector2(2.8f,.64f),new Vector3(face.x,y+1.7f,face.z)+outward*4);
                        bool right=end*side>0;string destination=end<0?"ЛИФТ ЮГ":"ЛИФТ СЕВЕР";
                        Print(route,right?destination+" →":"← "+destination,Vector2.zero,new Vector2(2.5f,.38f),baseHeight,true);
                    }
                }
            }
        }
    }
}
