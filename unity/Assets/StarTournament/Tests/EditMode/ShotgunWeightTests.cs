using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ShotgunWeightTests
    {
        [Test]public void PackagedDefaultTightensShotgunWithoutChangingDamageOrPelletCount()
        {
            var scene=EditorSceneManager.OpenScene("Assets/StarTournament/Scenes/ProvingGround.unity",OpenSceneMode.Additive);
            try
            {
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                var catalog=LabReleaseCatalog.Load();
                var current=DesignLabHistory.ResolvePublishedDefault(ground.CaptureLabAuthoringBaseline(),catalog).Selected.Snapshot;
                var prior=catalog.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).OrderByDescending(e=>e.Revision).Skip(1).First().Snapshot;
                var original=catalog.Entries.Single(e=>e.ProfileId==DesignLabHistory.ReleaseId&&e.Revision==2).Snapshot;
                Assert.That(current.Get("shot.spread"),Is.EqualTo(original.Get("shot.spread")*.8f).Within(.00001f));
                Assert.That(current.Get("shot.spread"),Is.EqualTo(prior.Get("shot.spread")));
                foreach(var path in new[]{"shot.damage","shot.pellets","shot.range","combat.cooldownSeconds"})
                    Assert.That(current.Get(path),Is.EqualTo(prior.Get(path)),path);
                Assert.That(current.Get("presentation.shotTracerRed"),Is.EqualTo(.32f));
                Assert.That(current.Get("presentation.shotTracerGreen"),Is.EqualTo(.32f));
                Assert.That(current.Get("presentation.shotTracerBlue"),Is.EqualTo(.32f));
                catalog.Entries.Remove(catalog.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).OrderByDescending(e=>e.Revision).First());
                var previous=DesignLabHistory.ResolvePublishedDefault(ground.CaptureLabAuthoringBaseline(),catalog).Selected.Snapshot;
                foreach(var descriptor in previous.Descriptors.Where(d=>!d.Path.StartsWith("presentation.shotTracer")))
                    Assert.That(current.Get(descriptor.Path),Is.EqualTo(previous.Get(descriptor.Path)),descriptor.Path);
                var combat=current.Profiles.Single(p=>p.Id=="unity-native-combat-v1");
                var resolver=new ShotgunResolver(combat,1.8f);
                for(int pellet=0;pellet<resolver.PelletCount;pellet++)
                    Assert.That(Vector3.Angle(Vector3.forward,resolver.Direction(Quaternion.identity,pellet,17)),Is.LessThanOrEqualTo(4.801f));
                foreach(var cue in new[]{"shotgun","shotgun-0","shotgun-1"})
                {
                    var clip=Resources.Load<AudioClip>("Audio/"+cue);
                    Assert.That(clip,Is.Not.Null);Assert.That(clip.length,Is.EqualTo(.55f).Within(.001f));
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
