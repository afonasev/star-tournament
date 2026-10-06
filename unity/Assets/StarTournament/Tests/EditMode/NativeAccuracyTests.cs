using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeAccuracyTests
    {
        static NativeMatchState Make(int minutes=1)
        {
            var profile=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(profile);config.DurationMinutes=minutes;
            return new NativeMatchState(4,config,profile,1);
        }
        static NativeStanding Row(NativeMatchState state,int seat)=>state.Read().Standings.Single(x=>x.Seat==seat);

        [Test] public void AggregatesMeanOfUsedWeaponFractionsWithoutIntermediateRounding()
        {
            var state=Make();state.RecordAccuracy(0,WeaponId.Shotgun,2,1);state.RecordAccuracy(0,WeaponId.Rifle,4,1);
            var row=Row(state,0);
            Assert.That(row.AccuracyPercent,Is.EqualTo(37.5).Within(1e-12));
            Assert.That(Row(state,1).AccuracyPercent,Is.Zero);
        }

        [Test] public void DelayedResultAndInvalidOrOverflowingUpdatesPreserveAggregateInvariant()
        {
            var state=Make();state.RecordAccuracy(0,WeaponId.Rifle,1,0);state.RecordAccuracy(0,WeaponId.Rifle,0,1);
            Assert.That(Row(state,0).RifleAccuracy.Successful,Is.EqualTo(1));
            var before=Row(state,0);
            Assert.Throws<ArgumentException>(()=>state.RecordAccuracy(0,WeaponId.Rifle,0,1));
            Assert.Throws<ArgumentException>(()=>state.RecordAccuracy(0,WeaponId.Shotgun,1,double.NaN));
            Assert.That(Row(state,0).RifleAccuracy,Is.EqualTo(before.RifleAccuracy));
            state.RecordAccuracy(0,WeaponId.Shotgun,double.MaxValue,0);
            var maxed=Row(state,0).ShotgunAccuracy;
            Assert.Throws<ArgumentException>(()=>state.RecordAccuracy(0,WeaponId.Shotgun,double.MaxValue,0));
            Assert.That(Row(state,0).ShotgunAccuracy,Is.EqualTo(maxed));
        }

        [Test] public void SnapshotRestorePreservesAccuracyOldZeroFieldsAndFinishedStateFreezesIt()
        {
            var state=Make();state.RecordAccuracy(0,WeaponId.RocketLauncher,2,1);
            var snapshot=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(state.Read()));var restored=Make();restored.Restore(snapshot);
            Assert.That(Row(restored,0).RocketAccuracy,Is.EqualTo(Row(state,0).RocketAccuracy));
            // Accuracy fields absent from historical snapshots deserialize as zero-initialized values.
            snapshot.Standings[Array.FindIndex(snapshot.Standings,r=>r.Seat==0)].RocketAccuracy=default;
            restored=Make();restored.Restore(snapshot);Assert.That(Row(restored,0).AccuracyPercent,Is.Zero);

            var finished=Make();finished.BeginTick();finished.RecordDamage(1,0,new DamageResult(100,true));finished.EndTick();
            for(int i=0;i<59;i++){finished.BeginTick();finished.EndTick();}
            Assert.That(finished.Phase,Is.EqualTo(NativeMatchPhase.Finished));
            var frozen=Row(finished,0);finished.RecordAccuracy(0,WeaponId.Rifle,1,1);
            Assert.That(Row(finished,0).RifleAccuracy,Is.EqualTo(frozen.RifleAccuracy));
            Assert.That(Make().Read().Standings.All(r=>r.AccuracyPercent==0),Is.True);
        }

        [Test] public void CutterReportsExactEnergyLimitedEmissionAndContactDurationsForUnion()
        {
            var profile=ProvingProfile.CreateCutterDefault();var beam=new CutterBeam(profile,2);
            double rate=profile.Get("cutter.energyPerSecond"),emitted;double appliedContact=0;
            double spent=beam.Advance(.4,rate*.05,Vector3.zero,Vector3.forward,20,new[]{0f,0f},new[]{1,1},1,
                (target,amount,contact)=>appliedContact=Math.Max(appliedContact,contact),out emitted);
            Assert.That(emitted,Is.EqualTo(.05).Within(1e-12));
            Assert.That(spent,Is.EqualTo(rate*.05).Within(1e-12));
            Assert.That(appliedContact,Is.EqualTo(emitted).Within(1e-12),"Simultaneous targets contribute their time union once");

            beam.Stop();double noContact=0;
            beam.Advance(.05,rate*.05,Vector3.zero,Vector3.forward,20,new[]{20f,20f},new[]{1,1},1,
                (target,amount,contact)=>noContact=Math.Max(noContact,contact),out emitted);
            Assert.That(emitted,Is.EqualTo(.05).Within(1e-12));Assert.That(noContact,Is.Zero);
        }

        [Test] public void CutterGrowthAccumulatesOnlyTheDeliveredEnemyContactInterval()
        {
            var beam=new CutterBeam(ProvingProfile.CreateCutterDefault(),1);double totalEmitted=0,totalContact=0;
            Action<int,float,double> contact=(target,amount,seconds)=>totalContact+=seconds;
            beam.Advance(.075,30,Vector3.zero,Vector3.forward,20,new[]{15f},new[]{1},1,contact,out double first);
            beam.Advance(.075,30,Vector3.zero,Vector3.forward,20,new[]{15f},new[]{1},1,contact,out double second);
            totalEmitted=first+second;
            Assert.That(first,Is.EqualTo(.075).Within(1e-12));Assert.That(second,Is.EqualTo(.075).Within(1e-12));
            Assert.That(totalEmitted,Is.EqualTo(.15).Within(1e-12));
            Assert.That(totalContact,Is.EqualTo(.0375).Within(1e-12),"Distance growth delays contact until 0.1125 seconds");
        }
    }
}
