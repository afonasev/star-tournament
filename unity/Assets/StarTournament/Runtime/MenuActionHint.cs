using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    // Captions remain owned by their screen; this decorates only the current action.
    public sealed class MenuActionHint : MonoBehaviour
    {
        public Func<InputDevice> Device;
        // Only screens that opt in receive the colored button artwork.
        public bool ColoredLobbyGlyph;
        // Main lobby opt-in; other menus retain their confirm hints.
        public bool HideFocusedConfirm;
        public Text CaptionOverride;
        public bool CardHint;
        public Func<bool> Active,Focused,ShortcutActive;
        public MenuPadButton? Shortcut;
        public Func<float> ReservedRightInset;
        Selectable button;Text caption,glyph,shortcutGlyph;Image glyphBacking,shortcutBacking;
        static Sprite circle;
        Text adjacentCaption;float adjacentLeft;
        public string PlainCaption=>caption?caption.text:null;
        public Text GlyphLabel=>glyph;
        public void Initialize(Selectable target,Func<InputDevice> device,Func<bool> active,Func<bool> focused=null)
        {
            button=target;caption=target is InputField field?field.textComponent:target.transform.Find("label").GetComponent<Text>();Device=device;Active=active;Focused=focused??(()=>EventSystem.current&&EventSystem.current.currentSelectedGameObject==target.gameObject);
            if(!glyph)
            {
                var backing=new GameObject("gamepad-action-button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));backing.transform.SetParent(target.transform,false);
                glyphBacking=backing.GetComponent<Image>();glyphBacking.raycastTarget=false;glyphBacking.sprite=Circle();glyphBacking.enabled=false;
                var node=new GameObject("gamepad-action-hint",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));node.transform.SetParent(target.transform,false);
                glyph=node.GetComponent<Text>();glyph.raycastTarget=false;glyph.alignment=TextAnchor.MiddleRight;glyph.supportRichText=false;
                var rect=glyph.rectTransform;rect.anchorMin=new Vector2(1,0);rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(-8,0);rect.offsetMax=new Vector2(-8,0);
                var secondary=new GameObject("gamepad-shortcut-button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));secondary.transform.SetParent(target.transform,false);
                shortcutBacking=secondary.GetComponent<Image>();shortcutBacking.raycastTarget=false;shortcutBacking.sprite=Circle();shortcutBacking.enabled=false;
                var secondaryText=new GameObject("gamepad-shortcut-hint",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));secondaryText.transform.SetParent(target.transform,false);
                shortcutGlyph=secondaryText.GetComponent<Text>();shortcutGlyph.raycastTarget=false;shortcutGlyph.supportRichText=false;shortcutGlyph.alignment=TextAnchor.MiddleCenter;
                shortcutGlyph.rectTransform.anchorMin=shortcutGlyph.rectTransform.anchorMax=new Vector2(1,.5f);shortcutGlyph.enabled=false;
                glyphBacking.transform.SetAsFirstSibling();shortcutBacking.transform.SetSiblingIndex(glyphBacking.transform.GetSiblingIndex()+1);
                glyph.transform.SetAsLastSibling();
                shortcutGlyph.transform.SetAsLastSibling();
            }
            glyph.enabled=false;
        }
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!button||!caption||!glyph||!shortcutGlyph)return;
            if(CaptionOverride)caption=CaptionOverride;
            if(ColoredLobbyGlyph){RefreshAdjacent();return;}
            var device=Device?.Invoke();
            bool focused=Focused?.Invoke()??false,shortcut=Shortcut.HasValue&&(ShortcutActive?.Invoke()??true);
            bool showConfirm=focused&&!HideFocusedConfirm;
            if(HideFocusedConfirm&&focused&&Shortcut==MenuPadButton.South)shortcut=false;
            bool show=button.IsActive()&&button.IsInteractable()&&(Active?.Invoke()??false)&&(showConfirm||shortcut)&&device is Gamepad&&device.added&&device.enabled;
            if(show)
            {
                var family=MenuGamepadHints.Family(device.layout,device.description.product);
                glyph.font=caption.font;glyph.fontStyle=caption.fontStyle;glyph.fontSize=ColoredLobbyGlyph&&family!=MenuPadFamily.Generic?26:Mathf.Min(caption.fontSize,family==MenuPadFamily.Generic?18:22);glyph.color=caption.color;
                string direct=shortcut?MenuGamepadHints.Glyph(family,Shortcut.Value):null;
                string confirm=MenuGamepadHints.Glyph(family,MenuPadButton.South);
                if(ColoredLobbyGlyph&&family!=MenuPadFamily.Generic){direct=NeutralMenuGlyph(direct);confirm=NeutralMenuGlyph(confirm);}
                bool pair=ColoredLobbyGlyph&&family!=MenuPadFamily.Generic&&showConfirm&&shortcut&&Shortcut.Value!=MenuPadButton.South;
                glyph.text=showConfirm?confirm:direct;
                if(showConfirm&&shortcut&&Shortcut.Value!=MenuPadButton.South&&!pair)glyph.text+=" / "+direct;
                bool buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(glyph.text);
                bool secondaryButton=pair&&IsButtonGlyph(direct);
                float buttonWidth=buttonGlyph?GlyphWidth(glyph.text):0,secondaryWidth=secondaryButton?GlyphWidth(direct):0;
                float width=pair?buttonWidth+secondaryWidth+4:buttonGlyph?buttonWidth:glyph.preferredWidth+4;
                float inset=Mathf.Max(ColoredLobbyGlyph?12:buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);
                // Keep the value/title rectangle untouched. Omit a hint when there is
                // no free space beside the caption (for example a narrow numeric key).
                bool left=caption.alignment==TextAnchor.MiddleLeft||caption.alignment==TextAnchor.UpperLeft||caption.alignment==TextAnchor.LowerLeft;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform,caption.rectTransform);
                float captionEnd=left?bounds.min.x+caption.preferredWidth:bounds.center.x+caption.preferredWidth*.5f;
                float space=((RectTransform)button.transform).rect.xMax-inset-captionEnd-4;
                if(space<width&&pair)
                {
                    pair=false;glyph.text=ColoredLobbyGlyph&&showConfirm?confirm:direct;buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(glyph.text);secondaryButton=false;
                    buttonWidth=buttonGlyph?GlyphWidth(glyph.text):0;secondaryWidth=0;width=buttonGlyph?buttonWidth:glyph.preferredWidth+4;
                    inset=Mathf.Max(ColoredLobbyGlyph?12:buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);
                    space=((RectTransform)button.transform).rect.xMax-inset-captionEnd-4;
                }
                if(space<width&&showConfirm&&shortcut&&!pair&&!ColoredLobbyGlyph){glyph.text=direct;buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(direct);buttonWidth=buttonGlyph?GlyphWidth(direct):0;width=buttonGlyph?buttonWidth:glyph.preferredWidth+4;inset=Mathf.Max(ColoredLobbyGlyph?12:buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);}
                show=space>=width;
                MenuPadButton primaryAction=showConfirm?(ColoredLobbyGlyph?MenuPadButton.South:shortcut&&Shortcut.Value!=MenuPadButton.South&&!pair?Shortcut.Value:MenuPadButton.South):Shortcut.Value;
                float primaryWidth=pair?buttonWidth:width;
                glyph.rectTransform.offsetMin=new Vector2(-primaryWidth-inset,0);glyph.rectTransform.offsetMax=new Vector2(-inset,0);
                float y=(glyph.rectTransform.anchorMin.y+glyph.rectTransform.anchorMax.y)*.5f;
                if(buttonGlyph)
                {
                    glyph.color=Color.white;glyph.alignment=TextAnchor.MiddleCenter;glyphBacking.color=ButtonColor(primaryAction);
                    var glyphOutline=glyphBacking.GetComponent<Outline>();if(primaryAction==MenuPadButton.Start){if(!glyphOutline)glyphOutline=glyphBacking.gameObject.AddComponent<Outline>();glyphOutline.enabled=true;glyphOutline.effectColor=new Color32(141,160,173,255);glyphOutline.effectDistance=new Vector2(1,-1);}else if(glyphOutline)glyphOutline.enabled=false;
                    var backingRect=glyphBacking.rectTransform;backingRect.anchorMin=backingRect.anchorMax=new Vector2(1,y);backingRect.sizeDelta=new Vector2(buttonWidth,buttonWidth);backingRect.anchoredPosition=new Vector2(-inset-buttonWidth*.5f,0);
                }
                else {glyph.alignment=TextAnchor.MiddleRight;}
                if(pair&&secondaryButton)
                {
                    shortcutGlyph.font=caption.font;shortcutGlyph.fontStyle=caption.fontStyle;shortcutGlyph.fontSize=glyph.fontSize;shortcutGlyph.text=direct;shortcutGlyph.color=Color.white;
                    var secondaryRect=shortcutGlyph.rectTransform;secondaryRect.sizeDelta=new Vector2(secondaryWidth,secondaryWidth);secondaryRect.anchoredPosition=new Vector2(-inset-buttonWidth-4-secondaryWidth*.5f,0);
                    var secondaryBg=shortcutBacking.rectTransform;secondaryBg.anchorMin=secondaryBg.anchorMax=new Vector2(1,y);secondaryBg.sizeDelta=new Vector2(secondaryWidth,secondaryWidth);secondaryBg.anchoredPosition=secondaryRect.anchoredPosition;
                    shortcutBacking.color=ButtonColor(Shortcut.Value);
                    var shortcutOutline=shortcutBacking.GetComponent<Outline>();if(Shortcut.Value==MenuPadButton.Start){if(!shortcutOutline)shortcutOutline=shortcutBacking.gameObject.AddComponent<Outline>();shortcutOutline.enabled=true;shortcutOutline.effectColor=new Color32(141,160,173,255);shortcutOutline.effectDistance=new Vector2(1,-1);}else if(shortcutOutline)shortcutOutline.enabled=false;
                }
                shortcutGlyph.enabled=pair&&secondaryButton&&show;shortcutBacking.enabled=shortcutGlyph.enabled;
                glyphBacking.enabled=show&&buttonGlyph;
            }
            glyph.enabled=show;if(!show){glyphBacking.enabled=false;shortcutGlyph.enabled=false;shortcutBacking.enabled=false;}
        }
        void RefreshAdjacent()
        {
            var device=Device?.Invoke();bool focused=Focused?.Invoke()??false;
            bool shortcut=Shortcut.HasValue&&(ShortcutActive?.Invoke()??true);
            bool showConfirm=focused&&!HideFocusedConfirm;
            if(HideFocusedConfirm&&focused&&Shortcut==MenuPadButton.South)shortcut=false;
            bool show=button.IsActive()&&button.IsInteractable()&&(Active?.Invoke()??false)&&(showConfirm||shortcut)&&device is Gamepad&&device.added&&device.enabled;
            bool pair=false,primaryButton=false,secondaryButton=false;
            float primaryWidth=0,secondaryWidth=0;
            var parent=(RectTransform)button.transform;float inset=Mathf.Max(14,ReservedRightInset?.Invoke()??14);
            bool left=caption.alignment==TextAnchor.MiddleLeft||caption.alignment==TextAnchor.LowerLeft||caption.alignment==TextAnchor.UpperLeft;
            // Cards keep their portrait and icon columns. Other actions center the entire text+hint group.
            if(adjacentCaption!=caption){adjacentCaption=caption;adjacentLeft=parent.rect.width*caption.rectTransform.anchorMin.x+caption.rectTransform.offsetMin.x;}
            float lo=CardHint?parent.rect.xMin+parent.rect.width*.52f:left?parent.rect.xMin+adjacentLeft:parent.rect.xMin+inset;
            float hi=CardHint?parent.rect.xMin+parent.rect.width*.95f:parent.rect.xMax-inset;
            float available=hi-lo;
            if(show)
            {
                var family=MenuGamepadHints.Family(device.layout,device.description.product);
                glyph.font=caption.font;glyph.fontStyle=caption.fontStyle;glyph.fontSize=family==MenuPadFamily.Generic?18:26;
                string confirm=family==MenuPadFamily.Generic?MenuGamepadHints.Glyph(family,MenuPadButton.South):NeutralMenuGlyph(MenuGamepadHints.Glyph(family,MenuPadButton.South));
                string direct=shortcut?MenuGamepadHints.Glyph(family,Shortcut.Value):null;
                if(family!=MenuPadFamily.Generic&&direct!=null)direct=NeutralMenuGlyph(direct);
                glyph.text=showConfirm?confirm:direct;
                pair=showConfirm&&shortcut&&Shortcut.Value!=MenuPadButton.South;
                primaryButton=family!=MenuPadFamily.Generic&&IsButtonGlyph(glyph.text);
                primaryWidth=primaryButton?GlyphWidth(glyph.text):glyph.preferredWidth+4;
                shortcutGlyph.font=caption.font;shortcutGlyph.fontStyle=caption.fontStyle;shortcutGlyph.fontSize=glyph.fontSize;shortcutGlyph.text=direct;
                secondaryButton=pair&&family!=MenuPadFamily.Generic&&IsButtonGlyph(direct);
                secondaryWidth=pair?(secondaryButton?GlyphWidth(direct):shortcutGlyph.preferredWidth+4):0;
                // Caption keeps priority; suppress only a redundant shortcut when the group cannot fit.
                if(caption.preferredWidth+10+primaryWidth+secondaryWidth+6>available){pair=false;secondaryWidth=0;}
                show=primaryWidth+28<available;
            }
            float hints=show?primaryWidth+(pair?secondaryWidth+6:0)+14:0;
            float titleWidth=Mathf.Min(caption.preferredWidth+4,Mathf.Max(0,available-hints));
            {
                var rect=caption.rectTransform;float start=left?lo:(lo+hi-titleWidth-hints)*.5f;
                rect.anchorMin=new Vector2(.5f,rect.anchorMin.y);rect.anchorMax=new Vector2(.5f,rect.anchorMax.y);
                rect.offsetMin=new Vector2(start-parent.rect.center.x,rect.offsetMin.y);rect.offsetMax=new Vector2(start+titleWidth-parent.rect.center.x,rect.offsetMax.y);
                caption.resizeTextForBestFit=true;caption.resizeTextMinSize=Mathf.Min(18,caption.fontSize);caption.resizeTextMaxSize=caption.fontSize;
            }
            if(show)
            {
                float start=left?lo:(lo+hi-titleWidth-hints)*.5f;
                float x=start+titleWidth+14;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform,caption.rectTransform);float y=bounds.center.y;
                PlaceGlyph(glyph,glyphBacking,x+primaryWidth*.5f,y,primaryWidth,primaryButton,showConfirm?MenuPadButton.South:Shortcut.Value);
                if(pair)PlaceGlyph(shortcutGlyph,shortcutBacking,x+primaryWidth+6+secondaryWidth*.5f,y,secondaryWidth,secondaryButton,Shortcut.Value);
            }
            glyph.enabled=show;glyphBacking.enabled=show&&primaryButton;
            shortcutGlyph.enabled=show&&pair;shortcutBacking.enabled=show&&pair&&secondaryButton;
        }
        void PlaceGlyph(Text label,Image backing,float x,float y,float width,bool buttonGlyph,MenuPadButton action)
        {
            var rect=label.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width,38);rect.anchoredPosition=new Vector2(x,y)-((RectTransform)button.transform).rect.center;
            label.alignment=TextAnchor.MiddleCenter;label.color=buttonGlyph?Color.white:caption.color;
            var bg=backing.rectTransform;bg.anchorMin=bg.anchorMax=new Vector2(.5f,.5f);bg.sizeDelta=new Vector2(width,width);bg.anchoredPosition=rect.anchoredPosition;backing.color=ButtonColor(action);
        }
        static Sprite Circle()
        {
            if(circle)return circle;
            return circle=LobbyVisualAssets.Button;
        }
        static string NeutralMenuGlyph(string value)=>value=="Menu"||value=="Options"||value=="+"?"≡":value;
        static bool IsButtonGlyph(string value)=>value.Length==1;
        static float GlyphWidth(string value)=>value.Length==1?38:58;
        static Color ButtonColor(MenuPadButton button)
        {
            switch(button)
            {
                case MenuPadButton.South:return new Color32(36,174,91,255);
                case MenuPadButton.East:return new Color32(216,61,66,255);
                case MenuPadButton.West:return new Color32(47,133,226,255);
                case MenuPadButton.North:return new Color32(238,190,45,255);
                default:return new Color32(9,20,29,255);
            }
        }
    }
    sealed class MenuContextHint : MonoBehaviour
    {
        public Func<string> Value;Text caption;
        void Awake(){caption=GetComponent<Text>();}
        void LateUpdate(){if(!caption)return;caption.text=Value?.Invoke()??"";caption.enabled=caption.text.Length>0;}
    }
    sealed class MenuPadOnlyHint : MonoBehaviour
    {
        public Func<bool> Show;
        Text caption;
        void Awake(){caption=GetComponent<Text>();}
        void LateUpdate(){if(caption)caption.enabled=Show?.Invoke()??false;}
    }
}
