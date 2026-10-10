using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class BotBanterVoiceCatalogTests
    {
        [Test] public void ExactCatalogCoversEveryExistingPhraseOnce()
        {
            var phrases=Enum.GetValues(typeof(BotBanterReason)).Cast<BotBanterReason>().SelectMany(BotBanter.Lines).ToArray();
            Assert.That(phrases.Length,Is.EqualTo(20));Assert.That(phrases.Distinct().Count(),Is.EqualTo(20));
            Assert.That(NativeBotBanterVoice.Clips.Keys,Is.EquivalentTo(phrases));
            Assert.That(NativeBotBanterVoice.Clips.Values.Distinct().Count(),Is.EqualTo(20));
            Assert.That(NativeBotBanterVoice.Clips.ContainsKey("Слабак"),Is.False);
        }
        [Test] public void EveryImportedRecordingIsNonemptyMonoWithBoundedDuration()
        {
            foreach(var entry in NativeBotBanterVoice.Clips)
            {
                var clip=Resources.Load<AudioClip>(entry.Value);
                Assert.That(clip,Is.Not.Null,entry.Key);Assert.That(clip.channels,Is.EqualTo(1),entry.Key);
                Assert.That(clip.samples,Is.GreaterThan(0),entry.Key);
                Assert.That(clip.length,Is.InRange(.5f,5f),entry.Key);
            }
        }
    }
}
