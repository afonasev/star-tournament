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
            hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo("A"));Assert.That(caption.text,Is.EqualTo("Профиль · Нижняя"));
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
        [Test]public void KeyboardHasNoGamepadArtworkAndUnknownPadHasXboxButtons()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                hint.Device=()=>keyboard;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(root.transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.False);
                hint.Device=()=>pad;hint.Refresh();Assert.That(hint.GlyphLabel.text,Is.EqualTo("A"));Assert.That(root.transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.True);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
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
            bool route=true;hint.Shortcut=MenuPadButton.East;hint.ShortcutActive=()=>route;focused=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo("B"));
            route=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);focused=true;hint.Refresh();Assert.That(hint.GlyphLabel.text,Is.EqualTo("A"));
            route=true;hint.Refresh();Assert.That(hint.GlyphLabel.text,Is.EqualTo("A"));Assert.That(root.transform.Find("gamepad-shortcut-hint").GetComponent<Text>().text,Is.EqualTo("B"));Assert.That(root.transform.Find("gamepad-shortcut-button").GetComponent<Image>().enabled,Is.True);button.interactable=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
        }
        [Test]public void ReservedDecorationSpaceProtectsInstalledBadgeOrDropdownArrow()
        {
            caption.alignment=TextAnchor.MiddleLeft;caption.text="Версия";caption.rectTransform.anchorMin=Vector2.zero;caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=caption.rectTransform.offsetMax=Vector2.zero;
            hint.ReservedRightInset=()=>250;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.rectTransform.offsetMax.x,Is.EqualTo(-250));
            hint.ReservedRightInset=()=>790;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);Assert.That(caption.text,Is.EqualTo("Версия"));
        }
        [TestCase(false)] [TestCase(true)]
        public void LobbyOptInHidesConfirmAndPreservesFamilyShortcutsInBothLayouts(bool centered)
        {
            hint.HideFocusedConfirm=true;hint.ColoredLobbyGlyph=centered;
            string[] layouts={"XInputController","DualShockGamepad","SwitchProControllerHID","Gamepad"};
            var actions=new[]{MenuPadButton.East,MenuPadButton.West,MenuPadButton.North,MenuPadButton.Start};
            var colors=new[]{new Color32(216,61,66,255),new Color32(47,133,226,255),new Color32(238,190,45,255),new Color32(9,20,29,255)};
            foreach(string layout in layouts)
            {
                var device=(Gamepad)InputSystem.AddDevice(layout);
                try
                {
                    hint.Device=()=>device;focused=true;hint.Shortcut=null;hint.Refresh();
                    Assert.That(hint.GlyphLabel.enabled,Is.False,"Focused lobby action needs no redundant confirm: "+layout);
                    Assert.That(root.transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.False);
                    for(int n=0;n<actions.Length;n++)
                    {
                        hint.Shortcut=actions[n];hint.Refresh();
                        var family=MenuGamepadHints.Family(device.layout,device.description.product);
                        string expected=MenuGamepadHints.Glyph(family,actions[n]);
                        if(family!=MenuPadFamily.Generic&&(expected=="Menu"||expected=="Options"||expected=="+"))expected="≡";
                        Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo(expected));
                        Assert.That(root.transform.Find("gamepad-shortcut-hint").GetComponent<Text>().enabled,Is.False,"Only one shortcut remains");
                        var backing=root.transform.Find("gamepad-action-button").GetComponent<Image>();
                        if(family!=MenuPadFamily.Generic){Assert.That(backing.enabled,Is.True);Assert.That((Color32)backing.color,Is.EqualTo(colors[n]));}
                        focused=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo(expected));focused=true;
                    }
                    hint.Shortcut=MenuPadButton.South;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False,"South is redundant on the focused lobby action");
                    hint.Shortcut=MenuPadButton.East;button.interactable=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);button.interactable=true;
                    hint.Shortcut=null;hint.HideFocusedConfirm=false;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True,"Other menus retain focused confirm by default");hint.HideFocusedConfirm=true;
                    Assert.That(caption.text,Is.EqualTo("Профиль · Нижняя"));
                }
                finally{InputSystem.RemoveDevice(device);}
            }
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
