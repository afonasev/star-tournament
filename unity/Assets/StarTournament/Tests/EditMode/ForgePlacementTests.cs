using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ForgePlacementTests
    {
        [Test] public void SixApprovedAnchorsAndUnchangedGeometry()
        {
            var bowl=CombatBowlCatalog.Build();var old=AuthoredArenaCatalog.Resolve(CombatBowlCatalog.Id,"19");
            Assert.That(bowl.Pickups.Count(p=>p.Kind==ArenaPickupKind.Plasma),Is.EqualTo(1));
            Assert.That(bowl.Pickups.Single(p=>p.Kind==ArenaPickupKind.Plasma).Anchor,Is.EqualTo(new Vector3(12,4,21)));
            Assert.That(bowl.Pickups.Single(p=>p.Kind==ArenaPickupKind.Pulse).Id,Is.EqualTo("pulse-south"));
            Assert.That(IndustrialTunnelsCatalog.Build().Pickups.Single(p=>p.Kind==ArenaPickupKind.Plasma).Anchor,Is.EqualTo(new Vector3(0,0,19)));
            foreach(var pair in new[]{(bowl,old),(LunarLaboratoryCatalog.Build(),AuthoredArenaCatalog.Resolve(LunarLaboratoryCatalog.Id,"8")),(WellArenaCatalog.Build(),AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"6"))})
            {
                Assert.That(pair.Item1.Identity,Is.Not.EqualTo(pair.Item2.Identity));
                Assert.That(pair.Item1.Solids.Select(s=>JsonUtility.ToJson(s)),Is.EqualTo(pair.Item2.Solids.Select(s=>JsonUtility.ToJson(s))));
                Assert.That(pair.Item1.Transitions.Select(s=>JsonUtility.ToJson(s)),Is.EqualTo(pair.Item2.Transitions.Select(s=>JsonUtility.ToJson(s))));
                Assert.That(pair.Item1.Spawns,Is.EqualTo(pair.Item2.Spawns));
                foreach(var p in pair.Item2.Pickups.Where(p=>p.Kind!=ArenaPickupKind.Plasma&&p.Id!="pulse-north"))
                    Assert.That(JsonUtility.ToJson(pair.Item1.Pickups.Single(x=>x.Id==p.Id)),Is.EqualTo(JsonUtility.ToJson(p)));
            }
            var lunar=LunarLaboratoryCatalog.Build();Assert.That(lunar.Pickups.Count(p=>p.Kind==ArenaPickupKind.Plasma),Is.EqualTo(2));
            foreach(var p in lunar.Pickups.Where(p=>p.Kind==ArenaPickupKind.Plasma))
            {Assert.That(Mathf.Abs(p.Anchor.x),Is.EqualTo(14));Assert.That(Mathf.Abs(p.Anchor.z),Is.EqualTo(9));Assert.That(p.Anchor.y,Is.Zero);}
            var well=WellArenaCatalog.Build();Assert.That(well.Pickups.Count(p=>p.Kind==ArenaPickupKind.Plasma),Is.EqualTo(2));
            foreach(int side in new[]{-1,1})
            {
                var p=well.Pickups.Single(p=>p.Id=="forge-stair-"+side);var deck=well.Solids.Single(s=>s.Id=="stair-"+side+"-1landing");
                Assert.That(p.Support,Is.EqualTo(deck.Support));Assert.That(p.Anchor.y,Is.EqualTo(10.8f).Within(.0001f));
                Assert.That(p.Anchor.x,Is.EqualTo(deck.Position.x-side*.1f).Within(.0001f));Assert.That(p.Anchor.z,Is.EqualTo(deck.Position.z));
                var settings=ProvingProfile.CreateWellDefault();float radius=settings.Get("well.pickupRingRadius")+settings.Get("well.pickupRingWidth")/2;
                Assert.That(radius,Is.LessThan(deck.Size.x/2));Assert.That(radius,Is.LessThan(deck.Size.z/2));
            }
        }
        [Test] public void PlacementUpgradePreservesF1SavedRegistriesAndHashes()
        {
            var profiles=new[]{CombatBowlCatalog.AuthoringProfile(),LunarLaboratoryCatalog.AuthoringProfile(),WellArenaCatalog.AuthoringProfile()};
            var method=typeof(ProvingProfile).GetMethod("BeforeForgePlacement",BindingFlags.Instance|BindingFlags.NonPublic);
            var old=new LabBundle{Profiles=profiles.Select(p=>(ProvingProfile)method.Invoke(p,new object[]{0})).ToList()};
            Assert.That(old.Profiles.Select(p=>p.Version),Is.EqualTo(new[]{13,3,6}));
            string path=Path.Combine(Path.GetTempPath(),"forge-placement-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,old);first.Create("F1 map layout");string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles=profiles.ToList()});Assert.That(next.StorageError,Is.Null);
                Assert.That(JsonUtility.ToJson(next.Profiles.Single(p=>p.Id==first.SelectedProfileId).Revisions.Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
                Assert.That(next.Selected.Snapshot.Get("forge-north.anchor.x"),Is.EqualTo(12));
                Assert.That(next.Selected.Snapshot.Get("lunar.map.forge-yard-east.anchor.x"),Is.EqualTo(14));
                Assert.That(next.Selected.Snapshot.Get("well.map.forge-stair-1.anchor.y"),Is.EqualTo(10.8f).Within(.0001f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
