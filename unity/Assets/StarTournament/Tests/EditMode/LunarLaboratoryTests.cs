using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public class LunarLaboratoryTests
    {
        [Test] public void LunarRegistryUpgradePreservesPreviousLabHash()
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lunar-lab-"+System.Guid.NewGuid()+".json");
            try
            {
                var old=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();old.Profiles.RemoveAll(p=>p.Id.StartsWith("lunar-laboratory-"));var history=new DesignLabHistory(path,old);history.Create("Before lunar map");var prior=history.Selected;
                var next=old.Clone();next.Profiles.Add(ProvingProfile.CreateLunarPresentation());next.Profiles.Add(LunarLaboratoryCatalog.AuthoringProfile());
                var upgraded=new DesignLabHistory(path,next);Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
                Assert.That(upgraded.Selected.Snapshot.IsAuthoring("lunar.map.yard.position.x"),Is.True);
            }
            finally {if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }
        [TestCase("BeforeLunarFlush")]
        [TestCase("BeforeLunarInterior")]
        [TestCase("BeforeLunarOpenCentre")]
        [TestCase("BeforeLunarRework")]
        [TestCase("BeforeLunarOffice")] public void ReworkRegistryPreservesExistingLunarLabHistory(string transform)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lunar-rework-"+System.Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod(transform,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=new LabBundle{Profiles=current.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};
                var prior=new DesignLabHistory(path,old);prior.Create("Previous lunar map");string hash=prior.Selected.Hash;int number=prior.Selected.Number;
                var next=new DesignLabHistory(path,current);Assert.That(next.StorageError,Is.Null);
                Assert.That(next.SelectedProfile.Revisions.Single(x=>x.Number==number).Hash,Is.EqualTo(hash));
            }
            finally {if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }
        [Test] public void LandscapeContactMatchesRenderedTrianglesAndUnderlay()
        {
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var sample=typeof(LunarLaboratoryPresentation).GetMethod("LandscapeHeight",flags);
            var vertex=typeof(LunarLaboratoryPresentation).GetMethod("LandscapeVertex",flags);
            float Height(Vector3 p)=>(float)sample.Invoke(null,new object[]{p,22f});
            Vector3 Vertex(int ring,int segment)=>(Vector3)vertex.Invoke(null,new object[]{ring,segment,22f});
            Assert.That(Height(new Vector3(34,0,20)),Is.EqualTo(-.35f).Within(.0001f),"near-fence underlay");
            // Interior triangle points exercise the actual planar surface, including the polar seam.
            for(int ring=0;ring<9;ring++)foreach(int segment in new[]{0,23,78,120,159})
            {
                var a=Vertex(ring,segment);var b=Vertex(ring+1,segment);var c=Vertex(ring,segment+1);var d=Vertex(ring+1,segment+1);
                foreach(var p in new[]{a*.2f+b*.3f+c*.5f,c*.2f+b*.3f+d*.5f})
                {
                    float expected=Mathf.Abs(p.x)<=45&&Mathf.Abs(p.z)<=45?Mathf.Max(-.35f,p.y):p.y;
                    Assert.That(Height(p),Is.EqualTo(expected).Within(.0001f),"rendered triangle "+ring+" / "+segment);
                }
            }
        }
        [Test] public void ApprovedV4Contract()
        {
            var d=LunarLaboratoryCatalog.Build();var result=ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault());Assert.That(result.IsValid,Is.True,result.ToString());
            Assert.That(d.Spawns.Length,Is.EqualTo(8));Assert.That(d.Pickups.Length,Is.EqualTo(11));Assert.That(d.Pickups.Count(x=>x.Kind==ArenaPickupKind.Shotgun||x.Kind==ArenaPickupKind.Pulse||x.Kind==ArenaPickupKind.Cutter),Is.EqualTo(6));
            Assert.That(d.Transitions.Length,Is.EqualTo(6));Assert.That(d.Pickups.Single(x=>x.Id=="H").Anchor.z,Is.GreaterThan(0));Assert.That(d.Pickups.Single(x=>x.Id=="V").Anchor.z,Is.LessThan(0));
            Assert.That(d.Pickups.Single(x=>x.Id=="D").Anchor.y,Is.EqualTo(LunarLaboratoryCatalog.Upper));
            Assert.That(AuthoredArenaCatalog.Next(IndustrialTunnelsCatalog.Id),Is.EqualTo(d.MapId));Assert.That(AuthoredArenaCatalog.Maximum(d.MapId),Is.EqualTo(8));
            Assert.That(LunarLaboratoryCatalog.AuthoringProfile().Validate(),Is.Empty);Assert.That(ProvingProfile.CreateLunarPresentation().Validate(),Is.Empty);
        }
    }
}
