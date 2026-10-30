using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class AmmoSupplyUpdate
    {
        // Explicit source staging; no user/author history, Player build or publication.
        public static void Stage()
        {
            ProvingGroundBuild.Prepare();
            var scene=EditorSceneManager.OpenScene("Assets/StarTournament/Scenes/ProvingGround.unity");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var baseline=ground.CaptureLabAuthoringBaseline();var previous=LabReleaseCatalog.Load();
            var gameplay=DesignLabHistory.ResolvePublishedDefault(baseline,previous).Selected.Snapshot;
            var draft=gameplay.Clone();
            draft.Set("rifle.startingAmmo",250);draft.Set("combat.startingAmmo",15);
            draft.Set("rocket.startingAmmo",10);draft.Set("cutter.energyCapacity",20);draft.Set("plasma.pickupAmmo",100);
            if(draft.Hash()==gameplay.Hash()){Debug.Log("AMMO_SUPPLY_ALREADY_STAGED");return;}
            var updated=previous.Clone();
            updated.Entries.Add(new LabReleaseEntry{Sequence=updated.Entries.Max(e=>e.Sequence)+1,
                ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=updated.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).Max(e=>e.Revision)+1,
                Date="20261031",Hash=draft.Hash(),Snapshot=draft});
            var validated=DesignLabHistory.ResolvePublishedDefault(baseline,updated);
            if(validated.Selected.Snapshot.Hash()!=draft.Hash())throw new InvalidOperationException("Staged ammo does not match startup Default");
            foreach(var old in previous.Entries)
                if(JsonUtility.ToJson(updated.Entries.Single(e=>e.Sequence==old.Sequence))!=JsonUtility.ToJson(old))
                    throw new InvalidOperationException("Historical release changed");
            const string target="Assets/StarTournament/Resources/LabReleaseCatalog.json";
            File.WriteAllText(target,JsonUtility.ToJson(updated,true));AssetDatabase.ImportAsset(target);
            Debug.Log("AMMO_SUPPLY_STAGED hash="+draft.Hash()+"; source only, author history untouched");
        }
    }
}
