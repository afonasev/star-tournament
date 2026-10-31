using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class GameNamesAmmoHudTests
    {
        [Test] public void CatalogUsesAgreedMapNamesWithoutChangingIds()
        {
            Assert.That(AuthoredArenaCatalog.Name(CombatBowlCatalog.Id),Is.EqualTo("Орбита"));
            Assert.That(AuthoredArenaCatalog.Name(IndustrialTunnelsCatalog.Id),Is.EqualTo("Тоннели"));
            Assert.That(AuthoredArenaCatalog.Name(LunarLaboratoryCatalog.Id),Is.EqualTo("Луна-9"));
            Assert.That(AuthoredArenaCatalog.Name(WellArenaCatalog.Id),Is.EqualTo("Станция Вега"));
            Assert.That(AuthoredArenaCatalog.Name(FiringRangeCatalog.Id),Is.EqualTo("Полигон"));
        }
        [Test] public void PresentationAliasesDoNotMutateLabMetadata()
        {
            var profile=ProvingProfile.CreateCutterDefault();var before=JsonUtility.ToJson(profile);
            Assert.That(GameDisplayNames.Text("COBALT / VECTOR / Crimson / Резак / FORGE"),Is.EqualTo("Автомат / Дробовик / Ракетница / Лазер / Плазмомёт"));
            Assert.That(GameDisplayNames.Text("Расход Резака"),Is.EqualTo("Расход Лазера"));
            Assert.That(JsonUtility.ToJson(profile),Is.EqualTo(before));
        }
        [Test] public void AmmoPreferenceDefaultsOffPersistsAndBelongsToResetAllowList()
        {
            bool had=PlayerPrefs.HasKey(NativeUserPreferences.AmmoHudKey);int previous=PlayerPrefs.GetInt(NativeUserPreferences.AmmoHudKey);
            try
            {
                PlayerPrefs.DeleteKey(NativeUserPreferences.AmmoHudKey);Assert.That(NativeUserPreferences.ShowAmmoHud,Is.False);
                NativeUserPreferences.SetAmmoHud(true);Assert.That(NativeUserPreferences.ShowAmmoHud,Is.True);
                Assert.That(PlayerPrefs.GetInt(NativeUserPreferences.AmmoHudKey),Is.EqualTo(1));
                var keys=new System.Collections.Generic.List<string>();int cap=Application.targetFrameRate;
                try{NativeUserPreferences.Reset(keys.Add,()=>{});}finally{Application.targetFrameRate=cap;}
                Assert.That(keys,Does.Contain(NativeUserPreferences.AmmoHudKey));
                NativeUserPreferences.SetAmmoHud(false);Assert.That(NativeUserPreferences.ShowAmmoHud,Is.False);
            }
            finally{if(had)PlayerPrefs.SetInt(NativeUserPreferences.AmmoHudKey,previous);else PlayerPrefs.DeleteKey(NativeUserPreferences.AmmoHudKey);PlayerPrefs.Save();}
        }
        [Test] public void PackagedDefaultAppliesAgreedAmmoAndPreservesOtherTuning()
        {
            var scene=EditorSceneManager.OpenScene("Assets/StarTournament/Scenes/ProvingGround.unity",OpenSceneMode.Additive);
            try
            {
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                var baseline=ground.CaptureLabAuthoringBaseline();var catalog=LabReleaseCatalog.Load();
                var current=DesignLabHistory.ResolveStartupDefault(baseline,catalog).Selected.Snapshot;
                var previous=catalog.Clone();previous.Entries.RemoveAt(previous.Entries.Count-1);
                var old=DesignLabHistory.ResolveStartupDefault(baseline,previous).Selected.Snapshot;
                var expected=new System.Collections.Generic.Dictionary<string,float>{{"rifle.startingAmmo",250},{"combat.startingAmmo",15},{"rocket.startingAmmo",10},{"cutter.energyCapacity",20},{"plasma.pickupAmmo",100}};
                foreach(var item in expected)Assert.That(current.Get(item.Key),Is.EqualTo(item.Value),item.Key);
                foreach(var d in old.Descriptors.Where(d=>old.IsEditable(d.Path)&&!expected.ContainsKey(d.Path)))
                    Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
                var life=new CombatLife("ammo-test",current.Profile("rifle.startingAmmo"),current.Profile("cutter.energyCapacity"),current.Profile("plasma.pickupAmmo"));
                Assert.That(life.Read().RifleAmmo,Is.EqualTo(250));
                foreach(var weapon in new[]{WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter,WeaponId.Plasma})Assert.That(life.CollectWeapon(weapon),Is.True);
                var state=life.Read();Assert.That(state.ShotgunAmmo,Is.EqualTo(15));Assert.That(state.RocketAmmo,Is.EqualTo(10));Assert.That(state.CutterEnergy,Is.EqualTo(20));Assert.That(state.PlasmaAmmo,Is.EqualTo(100));
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
