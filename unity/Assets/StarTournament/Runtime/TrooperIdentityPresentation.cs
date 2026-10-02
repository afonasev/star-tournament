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
                name=="TECI_helmet"||name=="torso"||name=="jumpjet";
        }
        public static void ApplyBody(GameObject root,Color color,ProvingProfile profile=null)
        {
            profile=profile??ProvingProfile.CreateParticipantPaletteDefault();
            var surfaces=root.GetComponent<TrooperIdentityMaterials>()??root.AddComponent<TrooperIdentityMaterials>();
            var tuning=new Vector4(profile.Get("participant.surface.detailGain"),profile.Get("participant.surface.maskStart"),
                profile.Get("participant.surface.maskWidth"),profile.Get("participant.surface.panelFloor"));
            var panels=new Vector4(profile.Get("participant.surface.shellStart"),profile.Get("participant.surface.shellWidth"),
                profile.Get("participant.surface.neutralPlateGain"),profile.Get("participant.surface.helmetStripeWidth"));
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // The shotgun shares ceramic/status materials with the skin but has independent boost state.
                // Begin applies identity before activating world actors; equipment ownership
                // must remain visible while their ancestors are inactive.
                if(renderer.name=="weapon:joined"||renderer.GetComponentInParent<WeaponModelPresentation>(true)!=null&&
                    (HasAncestor(renderer.transform,"automatic-rifle")||HasAncestor(renderer.transform,"pulse-launcher")||HasAncestor(renderer.transform,"cutter")))continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]&&!IsStatus(materials[i]))materials[i]=surfaces.Resolve(materials[i],renderer);
                renderer.sharedMaterials=materials;
                for(int i=0;i<materials.Length;i++)if(materials[i])
                {
                    var mat=materials[i];
                    if(mat.HasProperty("_IdentityColor"))
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                        block.SetColor("_IdentityColor",color);block.SetVector("_IdentitySurface",tuning);block.SetVector("_IdentityPanels",panels);
                        // Authored mesh roles define plate coverage; source reflectance separates
                        // helmet/chest shell panels from dark seams. The combined first-person
                        // mesh has a ceramic forearm slot; glove/fabric slots stay neutral.
                        float mode=!IsBodyZone(mat)?0:mat.name=="TECI_helmet"?3:
                            mat.name.StartsWith("vector-ceramic",StringComparison.OrdinalIgnoreCase)?
                                (renderer.name.Contains("UpperArm")||renderer.name.Contains("UpperLeg")||renderer.name=="vector-armored-hands"?2:-1):1;
                        // These materials are owned per actor. Persist identity in their PBR
                        // constant buffer as well as the slot block (native skinned rendering).
                        mat.SetColor("_IdentityColor",color);mat.SetVector("_IdentitySurface",tuning);mat.SetFloat("_IdentityMode",mode);mat.SetVector("_IdentityPanels",panels);
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
        readonly System.Collections.Generic.Dictionary<Tuple<Renderer,Material>,Material> materials=new System.Collections.Generic.Dictionary<Tuple<Renderer,Material>,Material>();
        Shader shader;
        public Material Resolve(Material source,Renderer renderer)
        {
            foreach(var owned in materials)
                if(owned.Key.Item1==renderer&&owned.Value==source)return source;
            var key=Tuple.Create(renderer,source);
            if(materials.TryGetValue(key,out var existing))return existing;
            shader=shader?shader:Resources.Load<Shader>("TrooperIdentity");
            if(!shader)throw new InvalidOperationException("Trooper identity shader missing");
            var clone=new Material(shader);
            // CopyPropertiesFromMaterial replaces the native Player property sheet with
            // the source shader's sheet and makes added identity uniforms unavailable.
            // Copy only shared authored properties, retaining the new shader's schema.
            for(int i=0;i<source.shader.GetPropertyCount();i++)
            {
                string property=source.shader.GetPropertyName(i);if(!clone.HasProperty(property))continue;
                switch(source.shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:clone.SetColor(property,source.GetColor(property));break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:clone.SetVector(property,source.GetVector(property));break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        clone.SetTexture(property,source.GetTexture(property));clone.SetTextureOffset(property,source.GetTextureOffset(property));clone.SetTextureScale(property,source.GetTextureScale(property));break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:clone.SetInteger(property,source.GetInteger(property));break;
                    default:clone.SetFloat(property,source.GetFloat(property));break;
                }
            }
            clone.shaderKeywords=source.shaderKeywords;clone.renderQueue=source.renderQueue;
            clone.enableInstancing=source.enableInstancing;clone.doubleSidedGI=source.doubleSidedGI;clone.globalIlluminationFlags=source.globalIlluminationFlags;
            clone.name=source.name;
            if(!clone.HasProperty("_IdentityColor"))throw new InvalidOperationException("Identity material schema missing after authored property copy");
            materials.Add(key,clone);return clone;
        }
        void OnDestroy(){foreach(var material in materials.Values)Destroy(material);materials.Clear();}
    }
}
