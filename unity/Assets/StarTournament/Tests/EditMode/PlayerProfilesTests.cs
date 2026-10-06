using System;
using System.IO;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class PlayerProfilesTests
    {
        [Test]
        public void ProfilePersistsWhileGuestAndDevicePreferenceRemainInSession()
        {
            string directory=Path.Combine(Path.GetTempPath(),"star-tournament-profiles-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"profiles.json");
            try
            {
                var first=new PlayerProfileCatalog(path);
                var alex=first.Create("Алексей",.18f,true);
                first.Rename(alex.Id,"Александр");first.SetPersonal(alex.Id,.22f,false);
                var session=new LocalIdentitySession();
                Assert.That(session.ChooseProfile(0,101,alex.Id,first),Is.True);
                Assert.That(session.ChooseProfile(1,102,alex.Id,first),Is.False,"One profile cannot occupy two seats");
                var guest=session.ChooseGuest(1,102,.18f,true);guest.MouseDegreesPerPixel=.27f;
                session.ClearSeat(1);Assert.That(session.ChooseGuest(1,102,.18f,true),Is.SameAs(guest));
                session.ClearSeat(0);Assert.That(session.TryRestore(0,101,first),Is.True);
                var reopened=new PlayerProfileCatalog(path);
                Assert.That(reopened.Find(alex.Id).Name,Is.EqualTo("Александр"));
                Assert.That(reopened.Find(alex.Id).MouseDegreesPerPixel,Is.EqualTo(.22f).Within(.001f));
                Assert.That(reopened.Find(alex.Id).ShowFps,Is.False);
                var nextSession=new LocalIdentitySession();
                Assert.That(nextSession.TryRestore(0,101,reopened),Is.False,"Device preference is not durable");
                Assert.That(nextSession.GuestAt(1),Is.Null,"Guest data is not durable");
                reopened.Delete(alex.Id);Assert.That(new PlayerProfileCatalog(path).Find(alex.Id),Is.Null);
            }
            finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        }
    }
}
