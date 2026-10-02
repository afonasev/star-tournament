using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class UnifiedDamageSessionTests
    {
        Scene scene; CharacterMotor[] motors; NativeCombatSession session; ProvingProfile move,combat;
        void Setup()
        {
            scene=SceneManager.CreateScene("unified-damage-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("unified damage");SceneManager.MoveGameObjectToScene(owner,scene);
            move=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("rifle.spread",0);combat.Set("shot.spread",0);
            var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            motors=new CharacterMotor[4];for(int i=0;i<4;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(-8+i*3,0,0));}
            var p=ProvingProfile.CreateMatchDefault();var match=new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),NativeMatchConfiguration.Default(p),p,50);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),combat,match);EquippedCombatFixture.Equip(session);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        void Step(LocalAction[] actions=null)=>session.Tick(actions??new LocalAction[4],.02f);
        void Switch(WeaponSelection weapon){var a=new LocalAction[4];a[0].SelectWeapon=weapon;Step(a);for(int i=0;i<52;i++)Step();}
        [UnityTest] public IEnumerator MultipliersPrecedeShieldAndFreezeSelectedTuning()
        {
            Setup();yield return null;var snapshot=session.Capture();snapshot.Lives[0].Armor=30;snapshot.Lives[1].Armor=30;snapshot.Lives[2].Armor=30;session.Restore(snapshot);
            combat.Set("damage.friendlyMultiplier",1);combat.Set("damage.selfMultiplier",1);
            var ally=session.ApplyDamage(1,1,80,0,1);Assert.That(ally.ArmorLost,Is.EqualTo(30));Assert.That(ally.HealthLost,Is.EqualTo(10));Assert.That(ally.Applied,Is.EqualTo(40));
            var own=session.ApplyDamage(0,1,80,0,1);Assert.That(own.Applied,Is.EqualTo(40));
            var enemy=session.ApplyDamage(2,1,80,0,1);Assert.That(enemy.Applied,Is.EqualTo(80));
            var row=session.Match.Read().Standings.Single(r=>r.Seat==0);Assert.That(row.DamageDealt,Is.EqualTo(80));Assert.That(row.AllyDamageDealt,Is.EqualTo(40));Assert.That(row.DamageReceived,Is.EqualTo(40));
            Assert.That(session.ApplyDamage(1,0,80,0,1).Applied,Is.Zero);
        }
        [UnityTest] public IEnumerator RifleAndShotgunStopAtAllyAndCutterPassesBothParticipants()
        {
            Setup();yield return null;motors[0].Initialize(move,new Vector3(-8,0,0));motors[1].Initialize(move,new Vector3(-8,0,3));motors[2].Initialize(move,new Vector3(-8,0,6));Physics.SyncTransforms();
            var fire=new LocalAction[4];fire[0].Fire=true;Step(fire);Step();Step();
            Assert.That(session.Life(1).Health,Is.EqualTo(95));Assert.That(session.Life(2).Health,Is.EqualTo(100));
            Switch(WeaponSelection.Shotgun);Step(fire);Assert.That(session.Life(1).Health,Is.EqualTo(52.5f).Within(.001));Assert.That(session.Life(2).Health,Is.EqualTo(100));
            Switch(WeaponSelection.Cutter);fire[0].FireHeld=true;Step(fire);fire[0].Fire=false;for(int i=0;i<40;i++)Step(fire);
            Assert.That(session.Life(1).Health,Is.LessThan(52.5f));Assert.That(session.Life(2).Health,Is.LessThan(100));
            Assert.That(session.Life(0).Health,Is.EqualTo(100),"Beam does not add self contact");
        }
    }
}
