using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeWeaponPickups()
        {
            var p=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            if(p.Id=="unity-combat-state-v1"&&p.Version>=6)
            {p.version=5;p.descriptors.RemoveAll(d=>d.Path.StartsWith("weaponPickup."));p.values.RemoveAll(v=>v.Path.StartsWith("weaponPickup."));}
            if(p.Id=="combat-bowl-ring-r7-authoring-v1"&&p.Version>=10)
            {
                p.version=9;
                bool Added(string path)=>new[]{"shotgun-west.","shotgun-east.","pulse-north.","pulse-south.","cutter-north.","cutter-south."}.Any(path.StartsWith);
                p.descriptors.RemoveAll(d=>Added(d.Path));p.values.RemoveAll(v=>Added(v.Path));
            }
            return p;
        }
    }
}
