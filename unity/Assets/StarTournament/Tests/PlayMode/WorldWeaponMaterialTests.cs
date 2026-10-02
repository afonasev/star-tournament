using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WorldWeaponMaterialTests
    {
        Scene scene;
        [UnityTearDown] public IEnumerator Cleanup()
        { if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene); }

        static bool Equipment(Renderer renderer) => renderer.name=="weapon:joined" ||
            renderer.transform.GetComponentsInParent<Transform>(true).Any(t=>
                t.name=="automatic-rifle" || t.name=="pulse-launcher" || t.name=="cutter");

        [UnityTest] public IEnumerator InactiveActorIdentityPreservesEquipmentMaterialsAndBoostRestoresThem()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            var body=Object.Instantiate(ground.TrooperBodyPrefab,ground.transform);
            var equipment=body.AddComponent<WeaponModelPresentation>();
            equipment.Initialize(ground.RiflePrefab,ground.Profile,ground.PulsePrefab,ground.CutterPrefab,ground.CutterProfile);
            var sources=body.GetComponentsInChildren<Renderer>(true).Where(Equipment)
                .ToDictionary(r=>r,r=>r.sharedMaterials);
            Assert.That(sources.Count,Is.GreaterThan(3),"all mounted weapons are present");
            Assert.That(sources.Keys.Count(r=>r.name=="weapon:joined"),Is.EqualTo(1));
            foreach(string name in new[]{"automatic-rifle","pulse-launcher","cutter"})
                Assert.That(sources.Keys.Any(r=>r.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name==name)),Is.True,name);
            var identity=Color.magenta;
            // Ordinary Begin applies identity to equipment while its actor root is inactive.
            foreach(bool active in new[]{false,true,false})
            {
                body.SetActive(active);
                TrooperIdentityPresentation.ApplyBody(body,identity,ground.ParticipantPaletteProfile);
                foreach(var source in sources)
                {
                    Assert.That(source.Key.sharedMaterials,Is.EqualTo(source.Value),"authored material ownership: "+source.Key.name+" active="+active);
                    foreach(var material in source.Key.sharedMaterials)
                        Assert.That(material.shader.name,Is.Not.EqualTo("StarTournament/TrooperIdentity"),"equipment is not a body surface");
                }
            }
            equipment.Tint(identity,true);
            foreach(var source in sources)
                for(int slot=0;slot<source.Value.Length;slot++)
                {
                    var material=source.Value[slot];var block=new MaterialPropertyBlock();source.Key.GetPropertyBlock(block,slot);
                    string property=material.HasProperty("_BaseColor")?"_BaseColor":"baseColorFactor";
                    Assert.That(block.GetColor(property),Is.EqualTo(Color.red),source.Key.name+" boost");
                }
            equipment.Tint(identity,false);
            foreach(var source in sources)
            {
                Assert.That(source.Key.sharedMaterials,Is.EqualTo(source.Value));
                for(int slot=0;slot<source.Value.Length;slot++)
                {
                    var material=source.Value[slot];var block=new MaterialPropertyBlock();source.Key.GetPropertyBlock(block,slot);
                    if(TrooperIdentityPresentation.IsStatus(material))
                    {
                        string property=material.HasProperty("_BaseColor")?"_BaseColor":"baseColorFactor";
                        Assert.That(block.GetColor(property),Is.EqualTo(identity),"status returns to identity");
                    }
                    else Assert.That(block.isEmpty,Is.True,source.Key.name+" returns to authored colors");
                }
            }
        }
    }
}
