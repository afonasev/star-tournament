using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeAudioSpatialTests
    {
        [Test] public void BeamListenerUsesClosestSegmentPointIncludingDegenerateBeam()
        {
            var start=Vector3.zero;var end=new Vector3(20,0,0);var listener=new Vector3(18,0,2);
            var point=NativeAudioSpatial.ClosestPoint(start,end,listener);
            Assert.That(point,Is.EqualTo(new Vector3(18,0,0)));
            var weapon=NativeAudioSpatial.ForListener(start,listener,0,28,.4f);
            var beam=NativeAudioSpatial.ForListener(point,listener,0,28,.4f);
            Assert.That(beam.Gain,Is.GreaterThan(weapon.Gain*2));
            Assert.That(NativeAudioSpatial.ClosestPoint(start,end,new Vector3(30,0,0)),Is.EqualTo(end));
            Assert.That(NativeAudioSpatial.ClosestPoint(start,start,listener),Is.EqualTo(start));
        }
        [Test] public void DistanceAndListenerYawControlGainAndPan()
        {
            var near=NativeAudioSpatial.ForListener(Vector3.right*2,Vector3.zero,0,20,.4f);
            var far=NativeAudioSpatial.ForListener(Vector3.right*18,Vector3.zero,0,20,.4f);
            Assert.That(near.Gain,Is.EqualTo(.36f).Within(.0001));Assert.That(far.Gain,Is.EqualTo(.04f).Within(.0001));
            Assert.That(near.Pan,Is.EqualTo(1).Within(.0001));
            Assert.That(NativeAudioSpatial.ForListener(Vector3.right*2,Vector3.zero,180,20,.4f).Pan,Is.EqualTo(-1).Within(.0001));
            Assert.That(NativeAudioSpatial.ForListener(Vector3.right*21,Vector3.zero,0,20,.4f).Gain,Is.Zero);
            Assert.That(NativeAudioSpatial.ForListener(Vector3.zero,Vector3.zero,0,20,.4f).Pan,Is.Zero);
        }
    }
}
