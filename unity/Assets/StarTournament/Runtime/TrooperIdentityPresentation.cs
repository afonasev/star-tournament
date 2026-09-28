using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Material-slot identity routing for the authored trooper. Textures and neutral hardware stay shared.</summary>
    public static class TrooperIdentityPresentation
    {
        public static bool IsStatus(Material material)=>material.name.StartsWith("identity",StringComparison.OrdinalIgnoreCase);
        public static bool IsBodyZone(Material material)
        {
            string name=material.name;
            // Authored material roles, not tunable thresholds: limb plates, shoulder fabric, helmet, chest and rear jet pack.
            return IsStatus(material)||name.StartsWith("vector-ceramic",StringComparison.OrdinalIgnoreCase)||
                name.StartsWith("strata-oxblood-fabric",StringComparison.OrdinalIgnoreCase)||
                name=="TECI_helmet"||name=="torso"||name=="jumpjet";
        }
        public static void ApplyBody(GameObject root,Color color)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // The shotgun shares ceramic/status materials with the skin but has independent boost state.
                if(renderer.name=="weapon:joined"||renderer.GetComponentInParent<WeaponModelPresentation>()!=null&&
                    (HasAncestor(renderer.transform,"automatic-rifle")||HasAncestor(renderer.transform,"pulse-launcher")||HasAncestor(renderer.transform,"cutter")))continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]&&IsBodyZone(materials[i]))Paint(renderer,i,color,IsStatus(materials[i]));
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
}
