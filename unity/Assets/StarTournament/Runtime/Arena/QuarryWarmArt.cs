using UnityEngine;
using System.Linq;
namespace StarTournament.ProvingGround
{
    // Presentation-only revision. Map solids and historical authoring snapshots are not rewritten.
    public static class QuarryWarmArt
    {
        public const string Identity="quarry-warm-limestone-a2@2";
        public static bool Applies(ArenaDefinition d)=>d.MapId==QuarryArenaCatalog.Id&&d.Revision=="6";
        public static void ConfigureMaterials(Material[] palette,ProvingProfile p,ArenaDefinition definition)
        {
            var shader=Shader.Find("StarTournament/QuarryWarmSurface");
            var stone=Resources.Load<Texture2D>("QuarryWarmArt/limestone");
            var gravel=Resources.Load<Texture2D>("QuarryWarmArt/gravel");
            void Surface(int i,int mode,float smooth,float metal,Texture2D texture=null)
            {
                var m=palette[i];m.shader=shader;m.name="Quarry warm A / "+i;
                m.SetColor("_BaseColor",new Color(p.Get("quarry.art.color-"+i+".0"),p.Get("quarry.art.color-"+i+".1"),p.Get("quarry.art.color-"+i+".2")));m.SetFloat("_Mode",mode);
                m.SetFloat("_Scale",p.Get(mode==1?"quarry.warm.gravelTile":"quarry.warm.stoneTile"));
                m.SetFloat("_Relief",p.Get("quarry.warm.relief"));m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
                if(texture)m.SetTexture("_BaseMap",texture);
                m.SetTexture("_StoneMap",stone);m.SetTexture("_GravelMap",gravel);
                m.SetFloat("_StoneScale",p.Get("quarry.warm.stoneTile"));m.SetFloat("_GravelScale",p.Get("quarry.warm.gravelTile"));
                m.SetFloat("_PatchScale",p.Get("quarry.warm.patchScale"));m.SetFloat("_BlendWidth",p.Get("quarry.warm.blendWidth"));
                m.SetFloat("_DustReach",p.Get("quarry.warm.dustReach"));
                // Only the existing upper floor receives the terrain-to-steel fringe; machine panels do not.
                m.SetFloat("_Deck",i==5?1:0);
                var upper=definition.Solids.Single(s=>s.Id=="upper-excavation");
                m.SetFloat("_DeckEdge",upper.Position.z-upper.Size.z*.5f);
            }
            float matte=p.Get("quarry.art.smoothness"),metalSmooth=p.Get("quarry.warm.metalSmoothness");
            float metal=p.Get("quarry.warm.metallic");
            Surface(0,1,matte,0,gravel);Surface(1,0,matte,0,stone);Surface(2,0,matte,0,stone);
            Surface(3,3,matte,0);Surface(5,2,metalSmooth,metal);Surface(7,2,metalSmooth,metal);
            Surface(8,4,metalSmooth,p.Get("quarry.warm.paintMetallic"));Surface(6,4,metalSmooth,p.Get("quarry.warm.paintMetallic"));
            palette[10].SetFloat("_Smoothness",p.Get("quarry.warm.glassSmoothness"));palette[10].SetFloat("_Metallic",metal);

        }
        public static void ConfigureDaylight(Light sun,ProvingProfile p)
        {
            sun.intensity=p.Get("quarry.warm.sun");sun.color=new Color(p.Get("quarry.warm.sunRed"),p.Get("quarry.warm.sunGreen"),p.Get("quarry.warm.sunBlue"));
            sun.transform.rotation=Quaternion.Euler(p.Get("quarry.warm.sunElevation"),p.Get("quarry.warm.sunAzimuth"),0);
            sun.shadowBias=p.Get("quarry.warm.shadowBias");sun.shadowNormalBias=p.Get("quarry.warm.shadowNormalBias");
            sun.shadowStrength=p.Get("quarry.warm.shadowStrength");sun.shadows=LightShadows.Soft;
        }
    }
}
