using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class IndustrialTunnelsTests
    {
        [Test] public void AlbedoIsIncludedAsLoadableResource()
        {
            var asset=UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/StarTournament/Resources/IndustrialTunnels/steel-panels.png");
            Assert.That(asset,Is.InstanceOf<Texture2D>(),asset?asset.GetType().Name:"AssetDatabase missing texture");
            Assert.That(Resources.Load<Texture2D>("IndustrialTunnels/steel-panels"),Is.Not.Null);
        }
        [Test] public void OldLabHistoryRetainsHashAndOverridesAfterTunnelAddition()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tunnel-lab-"+System.Guid.NewGuid());System.IO.Directory.CreateDirectory(directory);
            try
            {
                string path=System.IO.Path.Combine(directory,"history.json");var old=EditMode.DesignLabHistoryTests.Shipped();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.TunnelsArtId||p.Id==ProvingProfile.TunnelsAuthoringId);var history=new DesignLabHistory(path,old);history.Create("My settings");
                var draft=history.Selected.Snapshot;draft.Set("audio.footstepGain",.3f);history.Save(draft);var previous=history.Selected;
                var current=old.Clone();current.Profiles.Add(ProvingProfile.CreateIndustrialTunnelsPresentation());current.Profiles.Add(IndustrialTunnelsCatalog.AuthoringProfile());
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepGain"),Is.EqualTo(.3f));
                Assert.That(upgraded.Selected.Snapshot.Get("tunnels.light.warm"),Is.EqualTo(12));Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==previous.Number).Hash,Is.EqualTo(previous.Hash));
            }
            finally {System.IO.Directory.Delete(directory,true);}
        }
        [Test] public void WayfindingUpgradePreservesOldHistoryAndCustomLight()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"wayfinding-lab-"+System.Guid.NewGuid());System.IO.Directory.CreateDirectory(directory);
            try
            {
                string path=System.IO.Path.Combine(directory,"history.json");var current=EditMode.DesignLabHistoryTests.Shipped();
                var prior=typeof(ProvingProfile).GetMethod("BeforeTunnelWayfinding",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)prior.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Custom tunnels");var draft=history.Selected.Snapshot;
                draft.Set("tunnels.light.warm",8);history.Save(draft);var previous=history.Selected;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(upgraded.Selected.Snapshot.Get("tunnels.light.warm"),Is.EqualTo(8));
                Assert.That(upgraded.Selected.Snapshot.Get("tunnels.wayfinding.numberHeight"),Is.EqualTo(1.8f));
                Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==previous.Number).Hash,Is.EqualTo(previous.Hash));
                Assert.That(upgraded.Selected.Snapshot.Validate(),Is.Empty);
            }
            finally {System.IO.Directory.Delete(directory,true);}
        }
        [Test] public void ApprovedTopologyAndPickupsValidateWithoutSpeed()
        {
            var d=IndustrialTunnelsCatalog.Build();var result=ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault());Assert.That(result.IsValid,Is.True,result.ToString());
            Assert.That(d.Spawns.Length,Is.EqualTo(4));Assert.That(d.Transitions.Length,Is.EqualTo(16));Assert.That(d.Pickups.Length,Is.EqualTo(6));
            Assert.That(d.Pickups.Count(x=>x.Kind==ArenaPickupKind.Armor),Is.EqualTo(2));Assert.That(d.DisableSpeedPickup,Is.True);
            Assert.That(d.Pickups.Single(x=>x.Kind==ArenaPickupKind.Pulse).Anchor,Is.EqualTo(new Vector3(0,0,-19)));
            Assert.That(d.Pickups.Single(x=>x.Kind==ArenaPickupKind.Shotgun).Anchor.x,Is.LessThan(0));Assert.That(d.Pickups.Single(x=>x.Kind==ArenaPickupKind.Cutter).Anchor.x,Is.GreaterThan(0));
        }
        [Test] public void AdmissionAndFreezeRemainMapSpecific()
        {
            foreach(int n in new[]{2,3,4})Assert.That(AuthoredArenaCatalog.Supports(IndustrialTunnelsCatalog.Id,n),Is.True);
            foreach(int n in new[]{1,5,8})Assert.That(AuthoredArenaCatalog.Supports(IndustrialTunnelsCatalog.Id,n),Is.False);
            Assert.That(AuthoredArenaCatalog.Supports(CombatBowlCatalog.Id,8),Is.True);
            var d=IndustrialTunnelsCatalog.Build();var frozen=ArenaFreezeSnapshot.Create(d,ProvingProfile.CreateDefault());d.Pickups[0].Anchor+=Vector3.one;
            Assert.Throws<System.ArgumentException>(()=>frozen.RequireDefinition(d));Assert.That(frozen.Definition.Pickups[0].Anchor,Is.Not.EqualTo(d.Pickups[0].Anchor));
            Assert.Throws<System.ArgumentException>(()=>AuthoredArenaCatalog.Resolve(IndustrialTunnelsCatalog.Id,"unknown"));
            Assert.That(JsonUtility.ToJson(AuthoredArenaCatalog.Resolve(CombatBowlCatalog.Id)),Is.EqualTo(JsonUtility.ToJson(CombatBowlCatalog.Build())));
        }
        [Test] public void SeparateMetadataDoesNotExposeMapGeometryAsBalance()
        {
            var bundle=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{IndustrialTunnelsCatalog.AuthoringProfile(),ProvingProfile.CreateIndustrialTunnelsPresentation()}};
            Assert.That(bundle.Validate(),Is.Empty);Assert.That(bundle.Descriptors.All(d=>d.Path.StartsWith("tunnels.")),Is.True);
            Assert.That(bundle.Descriptors.Where(d=>d.Path.StartsWith("tunnels.map.")).All(d=>!bundle.IsVisible(d.Path)),Is.True);
            Assert.That(bundle.IsEditable("tunnels.light.warm"),Is.True);
        }
    }
}
