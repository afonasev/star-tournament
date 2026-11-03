using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabSnapshotCopyTests
    {
        static ProvingProfile Profile(List<NumericDescriptor> descriptors,List<ProvingProfileValue> values)
        {
            var profile=new ProvingProfile();
            typeof(ProvingProfile).GetField("descriptors",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(profile,descriptors);
            typeof(ProvingProfile).GetField("values",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(profile,values);
            return profile;
        }
        static NumericDescriptor Descriptor(string path,string unit="ratio")=>new NumericDescriptor
        {Path=path,Group="test",Label="Test",Description="Test",Unit=unit,Minimum=0,Maximum=10,Step=.5f,DefaultValue=1};

        [Test] public void CanonicalHashMatchesRoundTripTextForSignedZeroAndSpecialValues()
        {
            var profile=Profile(new List<NumericDescriptor>{Descriptor("number")},new List<ProvingProfileValue>{new ProvingProfileValue{Path="number",Value=0}});
            var bundle=new LabBundle{Profiles={profile}};
            var negativeZero=System.BitConverter.ToSingle(System.BitConverter.GetBytes(int.MinValue),0);
            var otherNan=System.BitConverter.ToSingle(System.BitConverter.GetBytes(unchecked((int)0x7fc00001)),0);
            var samples=new[]{0f,negativeZero,1f/3f,float.Epsilon,float.MaxValue,float.MinValue,float.NaN,otherNan,float.PositiveInfinity,float.NegativeInfinity,1e-30f};
            for(int pass=0;pass<2;pass++)foreach(var value in samples)
            {
                bundle.Set("number",value);
                string canonical=profile.Id+"@"+profile.Version+"\nnumber[Gameplay]="+value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                using(var sha=System.Security.Cryptography.SHA256.Create())
                {
                    var expected=System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))).Replace("-","").ToLowerInvariant();
                    Assert.That(bundle.Hash(),Is.EqualTo(expected));
                }
            }
        }
        [Test] public void ShippedBundleCopyMatchesJsonRoundTripAndOwnsItsMetadataAndValues()
        {
            var source=DesignLabHistoryTests.Shipped();source.Set("rifle.damage",31);
            source.Profile("rifle.damage").Descriptor("rifle.damage").Label="Unicode · кириллица\nlabel";
            source.Hash(); // Populate the source's owner and value indexes before copying.
            var expected=JsonUtility.FromJson<LabBundle>(JsonUtility.ToJson(source));
            var copy=source.Clone();
            Assert.That(JsonUtility.ToJson(copy),Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(copy.Hash(),Is.EqualTo(expected.Hash()));
            var detached=copy.Profile("rifle.damage");
            detached.Set("rifle.damage",99);detached.Descriptor("rifle.damage").Label="Changed";
            detached.Descriptor("rifle.damage").Maximum=200;
            copy.Profiles.RemoveAt(copy.Profiles.Count-1);
            Assert.That(source.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(source.Profile("rifle.damage").Descriptor("rifle.damage").Label,Does.StartWith("Unicode"));
            Assert.That(JsonUtility.ToJson(source),Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void RevisionCopyPreservesJsonNormalizationAndDetachesTheSnapshot()
        {
            var revision=new LabRevision{Number=7,Date=null,Hash=null,Snapshot=new LabBundle{Profiles={ProvingProfile.CreateDefault()}}};
            revision.Snapshot.Profile("audio.music.gain").Descriptor("audio.music.gain").Description=null;
            var expected=JsonUtility.FromJson<LabRevision>(JsonUtility.ToJson(revision));
            var copy=revision.Clone();
            Assert.That(copy.Date,Is.EqualTo(expected.Date));Assert.That(copy.Hash,Is.EqualTo(expected.Hash));
            Assert.That(copy.Snapshot.Profile("audio.music.gain").Descriptor("audio.music.gain").Description,
                Is.EqualTo(expected.Snapshot.Profile("audio.music.gain").Descriptor("audio.music.gain").Description));
            Assert.That(JsonUtility.ToJson(copy),Is.EqualTo(JsonUtility.ToJson(expected)));
            copy.Snapshot.Set("audio.music.gain",.1f);
            Assert.That(revision.Snapshot.Get("audio.music.gain"),Is.EqualTo(.55f));
        }

        [Test] public void IncompleteProfileCopyKeepsTheExistingJsonFallback()
        {
            var malformed=Profile(new List<NumericDescriptor>{null,Descriptor("test")},new List<ProvingProfileValue>{null,new ProvingProfileValue{Path="test",Value=2}});
            var source=new LabBundle{Profiles={malformed}};
            var expected=JsonUtility.FromJson<LabBundle>(JsonUtility.ToJson(source));
            Assert.That(JsonUtility.ToJson(source.Clone()),Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void ValidationKeepsTheFirstDuplicateDescriptorAndDiagnosticOrder()
        {
            var profile=Profile(new List<NumericDescriptor>{Descriptor("test","count"),Descriptor("test")},
                new List<ProvingProfileValue>{new ProvingProfileValue{Path="test",Value=1.5f}});
            Assert.That(profile.Validate().Select(i=>i.Message),Is.EqualTo(new[]{"Descriptor path must be unique.","Count must be integral."}));
        }

        [Test] public void ValidationDoesNotReuseAnIndexAfterMetadataChanges()
        {
            var descriptor=Descriptor("test");var value=new ProvingProfileValue{Path="test",Value=2};
            var profile=Profile(new List<NumericDescriptor>{descriptor},new List<ProvingProfileValue>{value});
            Assert.That(profile.Validate(),Is.Empty);
            descriptor.Path="changed";
            Assert.That(profile.Validate().Select(i=>i.Message),Is.EqualTo(new[]{"Value has no descriptor.","Descriptor has no profile value."}));
            value.Path="changed";Assert.That(profile.Validate(),Is.Empty);
            descriptor.Maximum=1;
            Assert.That(profile.Validate().Last().Message,Does.Contain("outside"));
        }

        [Test] public void ValidationRetainsNullEntryDiagnostics()
        {
            var profile=Profile(new List<NumericDescriptor>{null,Descriptor("test")},
                new List<ProvingProfileValue>{null,new ProvingProfileValue{Path="test",Value=1}});
            Assert.That(profile.Validate().Select(i=>i.Message),Is.EqualTo(new[]{"Descriptor is required.","Profile value is required."}));
        }
    }
}
