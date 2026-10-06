using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeScoreboard()
        {
            if(id!="unity-native-match-v1" && id!=DefaultId)return this;
            var p=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            p.descriptors.RemoveAll(d=>d.Path=="score.friendlyOrSelfKillPenalty"||d.Path.StartsWith("ui.standings"));
            p.values.RemoveAll(v=>v.Path=="score.friendlyOrSelfKillPenalty"||v.Path.StartsWith("ui.standings"));
            p.valueIndex=null;if(p.id=="unity-native-match-v1")p.version=1;
            return p;
        }
    }
}
