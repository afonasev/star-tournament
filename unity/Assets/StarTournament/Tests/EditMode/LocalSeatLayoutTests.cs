using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class LocalSeatLayoutTests
    {
        [Test] public void ApprovedViewportsCoverScreenWithoutOverlapping()
        {
            for(int count=1;count<=4;count++)
            {
                float area=0;
                for(int i=0;i<count;i++)
                {
                    var r=LocalSeatLayout.Viewport(count,i);area+=r.width*r.height;
                    Assert.That(r.xMin,Is.GreaterThanOrEqualTo(0));Assert.That(r.xMax,Is.LessThanOrEqualTo(1));
                    Assert.That(r.yMin,Is.GreaterThanOrEqualTo(0));Assert.That(r.yMax,Is.LessThanOrEqualTo(1));
                    for(int j=0;j<i;j++) Assert.That(r.Overlaps(LocalSeatLayout.Viewport(count,j)),Is.False);
                    if(count==3) Assert.That(r.Overlaps(LocalSeatLayout.PersistentStandings(count).Value),Is.False);
                }
                Assert.That(area,Is.EqualTo(count==3?.75f:1));
                Assert.That(LocalSeatLayout.PersistentStandings(count).HasValue,Is.EqualTo(count==3));
            }
            Assert.That(LocalSeatLayout.Viewport(2,0),Is.EqualTo(new Rect(0,0,.5f,1)));
            Assert.That(LocalSeatLayout.Viewport(2,1),Is.EqualTo(new Rect(.5f,0,.5f,1)));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>LocalSeatLayout.Viewport(5,0));
        }
        [Test] public void RemovingKeyboardSeatReleasesMouseOwnership()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var seats=new SeatInputCoordinator();seats.Assign(0,pad);seats.Assign(3,keyboard);
                Assert.That(seats.HasKeyboard,Is.True);
                seats.SetActiveSeatCount(2);Assert.That(seats.HasKeyboard,Is.False);
                InputSystem.RemoveDevice(mouse);seats.SetActiveSeatCount(4);
                Assert.That(seats.IsConnected(3),Is.False);Assert.That(seats.Assign(3,keyboard),Is.False);
            }
            finally {foreach(var d in new InputDevice[]{keyboard,mouse,pad})if(d.added)InputSystem.RemoveDevice(d);}
        }
        [Test] public void ShrinkReleasesOnlyRemovedDevicesAndGrowRequiresFreshJoins()
        {
            var seats=new SeatInputCoordinator();var pads=new Gamepad[4];
            try
            {
                for(int i=0;i<4;i++){pads[i]=InputSystem.AddDevice<Gamepad>();Assert.That(seats.Assign(i,pads[i]),Is.True);}
                seats.SetActiveSeatCount(2);Assert.That(seats.Ready,Is.True);
                Assert.That(seats.Assign(2,pads[2]),Is.False);Assert.That(seats.Consume(2).Fire,Is.False);
                InputSystem.RemoveDevice(pads[2]);Assert.That(seats.Ready,Is.True);
                seats.SetActiveSeatCount(3);Assert.That(seats.Ready,Is.False);
                Assert.That(seats.Assign(2,pads[0]),Is.False);
                Assert.That(seats.Assign(2,pads[3]),Is.True);Assert.That(seats.Ready,Is.True);
                InputSystem.RemoveDevice(pads[1]);Assert.That(seats.Ready,Is.False);
                InputSystem.AddDevice(pads[1]);Assert.That(seats.Ready,Is.True);
                Assert.Throws<System.ArgumentOutOfRangeException>(()=>seats.SetActiveSeatCount(0));
                Assert.Throws<System.ArgumentOutOfRangeException>(()=>seats.SetActiveSeatCount(5));
            }
            finally {foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
        }
    }
}
