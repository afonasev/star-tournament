using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Neutral source albedo with large authored identity panels; equipment stays independent.</summary>
    public static class TrooperIdentityPresentation
    {
        public static bool IsStatus(Material material)=>material.name.StartsWith("identity",StringComparison.OrdinalIgnoreCase);
        public static bool IsBodyZone(Material material)
        {
            string name=material.name;
            // Authored material roles: large limb plates, helmet, chest, thighs and rear jet pack.
            return IsStatus(material)||name.StartsWith("vector-ceramic",StringComparison.OrdinalIgnoreCase)||
                name=="TECI_helmet"||name=="torso"||name=="jumpjet"||name=="pants";
        }
        public static void ApplyBody(GameObject root,Color color,ProvingProfile profile=null)
        {
            profile=profile??ProvingProfile.CreateParticipantPaletteDefault();
            var surfaces=root.GetComponent<TrooperIdentityMaterials>()??root.AddComponent<TrooperIdentityMaterials>();
            var tuning=new Vector4(profile.Get("participant.surface.detailGain"),profile.Get("participant.surface.maskStart"),
                profile.Get("participant.surface.maskWidth"),profile.Get("participant.surface.panelFloor"));
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // The shotgun shares ceramic/status materials with the skin but has independent boost state.
                if(renderer.name=="weapon:joined"||renderer.GetComponentInParent<WeaponModelPresentation>()!=null&&
                    (HasAncestor(renderer.transform,"automatic-rifle")||HasAncestor(renderer.transform,"pulse-launcher")||HasAncestor(renderer.transform,"cutter")))continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]&&!IsStatus(materials[i]))materials[i]=surfaces.Resolve(materials[i]);
                renderer.sharedMaterials=materials;
                for(int i=0;i<materials.Length;i++)if(materials[i])
                {
                    var mat=materials[i];
                    if(mat.HasProperty("_IdentityColor"))
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                        block.SetColor("_IdentityColor",color);block.SetVector("_IdentitySurface",tuning);
                        // Semantic authored roles, not numerical gameplay tuning: ceramic limb plates
                        // are entire panels; textured shell/cloth use source reflectance for seams.
                        float mode=!IsBodyZone(mat)?0:
                            mat.name.StartsWith("vector-ceramic",StringComparison.OrdinalIgnoreCase)?2:1;
                        // These materials are owned per actor. Persist identity in their PBR
                        // constant buffer as well as the slot block (native skinned rendering).
                        mat.SetColor("_IdentityColor",color);mat.SetVector("_IdentitySurface",tuning);mat.SetFloat("_IdentityMode",mode);
                        block.SetFloat("_IdentityMode",mode);renderer.SetPropertyBlock(block,i);
                    }
                    else if(IsBodyZone(mat))Paint(renderer,i,color,IsStatus(mat));
                }
            }
        }
        static bool HasAncestor(Transform t,string name)
        {for(;t;t=t.parent)if(t.name==name)return true;return false;}
        public static void Paint(Renderer renderer,int slot,Color color,bool emission)
        {
            var material=renderer.sharedMaterials[slot];var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,slot);
            foreach(string property in new[]{"_BaseColor","baseColorFactor"})
                if(material.HasProperty(property))block.SetColor(property,color);
            if(emission)foreach(string property in new[]{"_EmissionColor","emissiveFactor"})
                if(material.HasProperty(property))block.SetColor(property,color);
            renderer.SetPropertyBlock(block,slot);
        }
    }

    /// <summary>Per-actor material ownership. Source textures/maps remain shared and never mutate.</summary>
    public sealed class TrooperIdentityMaterials:MonoBehaviour
    {
        readonly System.Collections.Generic.Dictionary<Material,Material> materials=new System.Collections.Generic.Dictionary<Material,Material>();
        Shader shader;
        public Material Resolve(Material source)
        {
            if(source.HasProperty("_IdentityColor"))return source;
            if(materials.TryGetValue(source,out var existing))return existing;
            shader=shader?shader:Resources.Load<Shader>("TrooperIdentity");
            if(!shader)throw new InvalidOperationException("Trooper identity shader missing");
            var clone=new Material(shader);clone.CopyPropertiesFromMaterial(source);clone.name=source.name;
            materials.Add(source,clone);return clone;
        }
        void OnDestroy(){foreach(var material in materials.Values)Destroy(material);materials.Clear();}
    }
}
