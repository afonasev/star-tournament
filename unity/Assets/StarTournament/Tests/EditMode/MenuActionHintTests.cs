using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class MenuActionHintTests
    {
        GameObject root;Gamepad pad;Button button;Text caption;MenuActionHint hint;bool active,focused;
        [SetUp]public void Setup()
        {
            root=new GameObject("hint fixture",typeof(RectTransform),typeof(Image),typeof(Button));button=root.GetComponent<Button>();((RectTransform)root.transform).sizeDelta=new Vector2(800,80);
            var label=new GameObject("label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(root.transform,false);caption=label.GetComponent<Text>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=24;caption.alignment=TextAnchor.MiddleCenter;caption.text="Профиль · Нижняя";
            pad=InputSystem.AddDevice<Gamepad>();active=focused=true;hint=root.AddComponent<MenuActionHint>();hint.Initialize(button,()=>pad,()=>active,()=>focused);
        }
        [TearDown]public void Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(root)Object.DestroyImmediate(root);}
        [Test]public void HintNeverRewritesDynamicCaptionOrMistakesItsContentsForASuffix()
        {
            hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo("Нижняя"));Assert.That(caption.text,Is.EqualTo("Профиль · Нижняя"));
            caption.text="Новая версия · A";for(int i=0;i<4;i++)hint.Refresh();Assert.That(caption.text,Is.EqualTo("Новая версия · A"));
            active=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(caption.text,Is.EqualTo("Новая версия · A"));
        }
        [Test]public void OnlyAvailableFocusedActiveConnectedActionAdvertisesConfirm()
        {
            hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);focused=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
            focused=true;button.interactable=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);button.interactable=true;
            InputSystem.DisableDevice(pad);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);InputSystem.EnableDevice(pad);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);
            InputSystem.RemoveDevice(pad);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
        }
        [Test]public void NarrowActionKeepsItsCaptionAndOmitsAnOverlappingHint()
        {
            caption.text="+";((RectTransform)root.transform).sizeDelta=new Vector2(35,40);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(caption.text,Is.EqualTo("+"));
            ((RectTransform)root.transform).sizeDelta=new Vector2(800,80);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);
        }
        [Test]public void InsetCaptionAnchorsAreIncludedWhenCheckingAvailableSpace()
        {
            ((RectTransform)root.transform).sizeDelta=new Vector2(240,80);caption.text="1234";caption.alignment=TextAnchor.MiddleLeft;
            caption.rectTransform.anchorMin=new Vector2(.7f,0);caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=caption.rectTransform.offsetMax=Vector2.zero;
            hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False,"A value beginning inside the action consumes its real right-side space");
            caption.rectTransform.anchorMin=Vector2.zero;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(caption.text,Is.EqualTo("1234"));
        }
        [Test]public void ExplicitShortcutAppearsOnlyForTheActualAvailableRoute()
        {
            bool route=true;hint.Shortcut=MenuPadButton.East;hint.ShortcutActive=()=>route;focused=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo("Правая"));
            route=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);focused=true;hint.Refresh();Assert.That(hint.GlyphLabel.text,Is.EqualTo("Нижняя"));
            route=true;hint.Refresh();Assert.That(hint.GlyphLabel.text,Is.EqualTo("Нижняя / Правая"));button.interactable=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
        }
        [Test]public void ReservedDecorationSpaceProtectsInstalledBadgeOrDropdownArrow()
        {
            caption.alignment=TextAnchor.MiddleLeft;caption.text="Версия";caption.rectTransform.anchorMin=Vector2.zero;caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=caption.rectTransform.offsetMax=Vector2.zero;
            hint.ReservedRightInset=()=>250;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.rectTransform.offsetMax.x,Is.EqualTo(-250));
            hint.ReservedRightInset=()=>790;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(caption.text,Is.EqualTo("Версия"));
        }
        [Test]public void ContextSpecificFocusDoesNotDependOnASingleGlobalSelection()
        {
            var second=Object.Instantiate(root);try
            {
                var secondButton=second.GetComponent<Button>();var secondHint=second.GetComponent<MenuActionHint>();bool secondFocus=true;
                secondHint.Initialize(secondButton,()=>pad,()=>active,()=>secondFocus);
                hint.Refresh();secondHint.Refresh();Assert.That(hint.GlyphLabel.enabled&&secondHint.GlyphLabel.enabled,Is.True);
                focused=false;hint.Refresh();secondHint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(secondHint.GlyphLabel.enabled,Is.True);
                secondFocus=false;secondHint.Refresh();Assert.That(secondHint.GlyphLabel.enabled,Is.False);
            }
            finally{Object.DestroyImmediate(second);}
        }
    }
}
