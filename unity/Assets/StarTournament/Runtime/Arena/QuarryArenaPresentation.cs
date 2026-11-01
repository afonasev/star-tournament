using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // Prototype colors only: the visible shell is the exact physics shell, without decorative blockers.
    public sealed class QuarryArenaPresentation : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();Mesh markings;
        Color oldAmbient;AmbientMode oldMode;bool ownsAmbient;
        Material Make(ProvingProfile p,int i)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Quarry shared "+i,color=new Color(p.Get("quarry.art.color-"+i+".0"),p.Get("quarry.art.color-"+i+".1"),p.Get("quarry.art.color-"+i+".2"))};
            m.SetFloat("_Smoothness",p.Get("quarry.art.smoothness"));materials.Add(m);return m;
        }
        public void Build(ArenaDefinition definition)
        {
            var profile=ProvingProfile.CreateQuarryAuthoring(definition);oldAmbient=RenderSettings.ambientLight;oldMode=RenderSettings.ambientMode;ownsAmbient=true;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white*profile.Get("quarry.art.ambient");
            var palette=new Material[5];for(int i=0;i<palette.Length;i++)palette[i]=Make(profile,i);
            var lookup=new ArenaPresentationLookup(transform.parent);
            foreach(var solid in definition.Solids)
            {
                var child=lookup.Find(solid.Id);var renderer=child.GetComponent<MeshRenderer>();
                int index=solid.Material=="quarry-crate"?3:solid.Material=="floor"?(solid.Id=="quarry-ground"?0:2):1;
                renderer.sharedMaterial=palette[index];
            }
            markings=ArenaPickupMarkings.Build(transform,definition,palette[4],profile.Get("quarry.art.pickupRingRadius"),profile.Get("quarry.art.pickupRingWidth"),_=>profile.Get("quarry.art.pickupRingHeight"));
        }
        public static void ConfigureDaylight(Light sun)
        {
            var p=QuarryArenaCatalog.AuthoringProfile();sun.intensity=p.Get("quarry.art.sun");

        }
        void OnDestroy(){if(ownsAmbient){RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;}if(markings)DestroyImmediate(markings);foreach(var material in materials)if(material)DestroyImmediate(material);}
    }
}
