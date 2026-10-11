using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public enum VisualMaterialRole { Wall, Floor, Metal }
    public sealed class NativeVisualMaterials : MonoBehaviour
    {
        public readonly Dictionary<Material,VisualMaterialRole> Roles=new Dictionary<Material,VisualMaterialRole>();
        public static Material Register(Component owner,Material material,VisualMaterialRole role)
        {var registry=owner.GetComponent<NativeVisualMaterials>()??owner.gameObject.AddComponent<NativeVisualMaterials>();registry.Roles[material]=role;return material;}
    }
}
