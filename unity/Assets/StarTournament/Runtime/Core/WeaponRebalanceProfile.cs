using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        void RebalanceDefault(string path,float value)
        {Set(path,value);FindDescriptor(path).DefaultValue=value;}
        // Exact pre-rebalance registry: immutable hashes and metadata must remain trusted.
        internal ProvingProfile BeforeWeaponRebalance()
        {
            if((id!="unity-native-combat-v1"||version!=7)&&(id!="unity-combat-state-v1"||version!=7)&&(id!="cutter-beam-v1"||version!=4))return this;
            var p=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            if(id=="unity-native-combat-v1")
            {p.version=6;p.RebalanceDefault("rifle.damage",20);p.RebalanceDefault("rifle.spread",1.2f);p.RebalanceDefault("shot.damage",70);p.RebalanceDefault("shot.pellets",12);p.RebalanceDefault("shot.spread",7);p.RebalanceDefault("rocket.maximumDamage",100);p.FindDescriptor("rocket.maximumDamage").Description="Единый предел прямого и радиального урона, включая усиление.";}
            else if(id=="unity-combat-state-v1")
            {p.version=6;p.RebalanceDefault("rifle.startingAmmo",100);p.RebalanceDefault("rifle.cooldownSeconds",.12f);p.RebalanceDefault("weapon.switchSeconds",1);p.RebalanceDefault("damageBoost.multiplier",2);}
            else p.version=3;
            return p;
        }
    }
}
