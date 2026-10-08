using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAchievementSessionTests
    {
        [UnityTest] public IEnumerator ActualMotorFireSwitchAndRestoreCountersExcludeInputAttemptsAndTeleport()
        {
            var scene=SceneManager.CreateScene("achievement-facts-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("achievement-session");SceneManager.MoveGameObjectToScene(owner,scene);
            try
            {
                var move=ProvingProfile.CreateDefault();var life=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
                var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
                var motors=new CharacterMotor[2];for(int p=0;p<2;p++){var go=new GameObject("p"+p);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[p]=go.AddComponent<CharacterMotor>();motors[p].Initialize(move,new Vector3(p*6,0,0));}
                var profile=ProvingProfile.CreateMatchDefault();var match=new NativeMatchState(2,NativeMatchConfiguration.Default(profile),profile,50);
                match.ConfigureAchievementRecipients(new[]{true,false});
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);EquippedCombatFixture.Equip(session);
                NativeStanding Row()=>match.Read().Standings.Single(x=>x.Seat==0);
                var actions=new LocalAction[2];
                for(int t=0;t<20;t++)session.Tick(actions,.02f);
                actions[0].Move=Vector2.up;for(int t=0;t<20;t++)session.Tick(actions,.02f);
                Assert.That(Row().DistanceTravelled,Is.GreaterThan(0));
                actions[0].Move=Vector2.zero;actions[0].Jump=true;session.Tick(actions,.02f);
                Assert.That(Row().Jumps,Is.EqualTo(1));
                session.Tick(actions,.02f);Assert.That(Row().Jumps,Is.EqualTo(1),"Airborne input is not another jump");
                actions[0]=new LocalAction{Fire=true};session.Tick(actions,.02f);
                Assert.That(Row().Shots,Is.EqualTo(1));
                session.Tick(actions,.02f);Assert.That(Row().Shots,Is.EqualTo(1),"Cooldown prevents a second actual shot");
                actions[0]=new LocalAction{SelectWeapon=WeaponSelection.Shotgun};session.Tick(actions,.02f);
                Assert.That(Row().WeaponSwitches,Is.Zero,"Request alone does not complete a switch");
                actions[0]=default;for(int t=0;t<60;t++)session.Tick(actions,.02f);
                Assert.That(Row().WeaponSwitches,Is.EqualTo(1));
                var before=session.Capture();double distance=Row().DistanceTravelled;
                before.Poses[0].Position+=Vector3.right*2;session.Restore(before);
                Assert.That(Row().DistanceTravelled,Is.EqualTo(distance),"Restored position does not count as travelled");
                actions[0]=new LocalAction{SelectWeapon=WeaponSelection.Cutter};session.Tick(actions,.02f);
                actions[0]=default;for(int t=0;t<60;t++)session.Tick(actions,.02f);
                int shots=Row().Shots;actions[0]=new LocalAction{FireHeld=true};
                for(int t=0;t<10;t++)session.Tick(actions,.02f);
                Assert.That(Row().Shots,Is.EqualTo(shots+1),"Continuous cutter is one burst, not ten shots");
                actions[0]=default;session.Tick(actions,.02f);actions[0]=new LocalAction{FireHeld=true};session.Tick(actions,.02f);
                Assert.That(Row().Shots,Is.EqualTo(shots+2),"Release and new emission begin a new burst");
                yield return null;
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);SceneManager.UnloadSceneAsync(scene);}
        }
        [UnityTest] public IEnumerator PhysicalArmorCollectCountsOneBonusAndNoRepeatWhileUnavailable()
        {
            var scene=SceneManager.CreateScene("achievement-pickup-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("achievement-pickup");SceneManager.MoveGameObjectToScene(owner,scene);
            try
            {
                var move=ProvingProfile.CreateDefault();var arena=owner.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(move),move);
                var motors=new CharacterMotor[2];for(int p=0;p<2;p++){var go=new GameObject("p"+p);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[p]=go.AddComponent<CharacterMotor>();motors[p].Initialize(move,new Vector3(0,-1.2f,p*2));}
                var profile=ProvingProfile.CreateMatchDefault();var match=new NativeMatchState(2,NativeMatchConfiguration.Default(profile),profile,50);
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),match);
                motors[0].Initialize(move,session.ArmorPickups[0].Anchor);session.Tick(new LocalAction[2],.02f);
                var row=match.Read().Standings.Single(x=>x.Seat==0);Assert.That(row.ArmorPickups,Is.EqualTo(1));Assert.That(row.BonusPickups,Is.EqualTo(1));
                session.Tick(new LocalAction[2],.02f);row=match.Read().Standings.Single(x=>x.Seat==0);
                Assert.That(row.ArmorPickups,Is.EqualTo(1),"Unavailable pickup is not collected twice");yield return null;
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
