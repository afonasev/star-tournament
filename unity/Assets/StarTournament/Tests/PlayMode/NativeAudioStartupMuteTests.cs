using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAudioStartupMuteTests
    {
        [UnityTest]
        public IEnumerator NoAudioLaunchKeepsListenerMutedAcrossReviewOverrides()
        {
            Assert.That(NativeAudioStartupMute.ShouldMute(
                System.Environment.GetCommandLineArgs(), Application.isEditor, Application.isBatchMode), Is.True,
                "The focused PlayMode wrapper must pass -noaudio -muted before Unity starts.");
            CollectionAssert.Contains(System.Environment.GetCommandLineArgs(), "-noaudio");
            CollectionAssert.Contains(System.Environment.GetCommandLineArgs(), "-muted");
            Assert.That(NativeAudioStartupMute.ShouldMute(new[] { "-noaudio", "-humanAudio", "-audioReview" }, false, false), Is.True);
            Assert.That(NativeAudioStartupMute.ShouldMute(new[] { "-muted", "-humanAudio" }, false, false), Is.True);
            Assert.That(NativeAudioStartupMute.ShouldMute(new[] { "-musicReview", "-audioReview" }, false, false), Is.True);
            Assert.That(NativeAudioStartupMute.ShouldMute(new[] { "-humanAudio", "-runTests" }, false, false), Is.True);
            Assert.That(NativeAudioStartupMute.ShouldMute(new[] { "-humanAudio" }, false, true), Is.True);
            Assert.That(NativeAudioStartupMute.SceneCallbackRan, Is.True,
                "The mute policy must run in BeforeSceneLoad, before game behaviours can play audio.");
            Assert.That(NativeAudioStartupMute.IsMuted, Is.True);
            Assert.That(AudioListener.volume, Is.Zero);

            NativeAudioStartupMute.SetListenerVolume(1);
            yield return null;

            Assert.That(NativeAudioStartupMute.IsMuted, Is.True);
            Assert.That(AudioListener.volume, Is.Zero);
        }
    }
}
