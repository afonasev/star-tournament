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
        public Func<bool> Active,Focused,ShortcutActive;
        public MenuPadButton? Shortcut;
        public Func<float> ReservedRightInset;
        Selectable button;Text caption,glyph,shortcutGlyph;Image glyphBacking,shortcutBacking;
        static Sprite circle;
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
            var device=Device?.Invoke();
            bool focused=Focused?.Invoke()??false,shortcut=Shortcut.HasValue&&(ShortcutActive?.Invoke()??true);
            bool show=button.IsActive()&&button.IsInteractable()&&(Active?.Invoke()??false)&&(focused||shortcut)&&device is Gamepad&&device.added&&device.enabled;
            if(show)
            {
                var family=MenuGamepadHints.Family(device.layout,device.description.product);
                glyph.font=caption.font;glyph.fontStyle=caption.fontStyle;glyph.fontSize=Mathf.Min(caption.fontSize,family==MenuPadFamily.Generic?18:22);glyph.color=caption.color;
                string direct=shortcut?MenuGamepadHints.Glyph(family,Shortcut.Value):null;
                string confirm=MenuGamepadHints.Glyph(family,MenuPadButton.South);
                if(ColoredLobbyGlyph&&family!=MenuPadFamily.Generic){direct=NeutralMenuGlyph(direct);confirm=NeutralMenuGlyph(confirm);}
                bool pair=ColoredLobbyGlyph&&family!=MenuPadFamily.Generic&&focused&&shortcut&&Shortcut.Value!=MenuPadButton.South;
                glyph.text=focused?confirm:direct;
                if(focused&&shortcut&&Shortcut.Value!=MenuPadButton.South&&!pair)glyph.text+=" / "+direct;
                bool buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(glyph.text);
                bool secondaryButton=pair&&IsButtonGlyph(direct);
                float buttonWidth=buttonGlyph?GlyphWidth(glyph.text):0,secondaryWidth=secondaryButton?GlyphWidth(direct):0;
                float width=pair?buttonWidth+secondaryWidth+4:buttonGlyph?buttonWidth:glyph.preferredWidth+4;
                float inset=Mathf.Max(buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);
                // Keep the value/title rectangle untouched. Omit a hint when there is
                // no free space beside the caption (for example a narrow numeric key).
                bool left=caption.alignment==TextAnchor.MiddleLeft||caption.alignment==TextAnchor.UpperLeft||caption.alignment==TextAnchor.LowerLeft;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform,caption.rectTransform);
                float captionEnd=left?bounds.min.x+caption.preferredWidth:bounds.center.x+caption.preferredWidth*.5f;
                float space=((RectTransform)button.transform).rect.xMax-inset-captionEnd-4;
                if(space<width&&pair)
                {
                    pair=false;glyph.text=direct;buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(direct);secondaryButton=false;
                    buttonWidth=buttonGlyph?GlyphWidth(glyph.text):0;secondaryWidth=0;width=buttonGlyph?buttonWidth:glyph.preferredWidth+4;
                    inset=Mathf.Max(buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);
                    space=((RectTransform)button.transform).rect.xMax-inset-captionEnd-4;
                }
                if(space<width&&focused&&shortcut&&!pair){glyph.text=direct;buttonGlyph=ColoredLobbyGlyph&&IsButtonGlyph(direct);buttonWidth=buttonGlyph?GlyphWidth(direct):0;width=buttonGlyph?buttonWidth:glyph.preferredWidth+4;inset=Mathf.Max(buttonGlyph?width+8:8,ReservedRightInset?.Invoke()??8);}
                show=space>=width;
                MenuPadButton primaryAction=focused&&shortcut&&Shortcut.Value!=MenuPadButton.South&&!pair?Shortcut.Value:focused?MenuPadButton.South:Shortcut.Value;
                float primaryWidth=pair?buttonWidth:width;
                glyph.rectTransform.offsetMin=new Vector2(-primaryWidth-inset,0);glyph.rectTransform.offsetMax=new Vector2(-inset,0);
                float y=(glyph.rectTransform.anchorMin.y+glyph.rectTransform.anchorMax.y)*.5f;
                if(buttonGlyph)
                {
                    glyph.color=Color.white;glyph.alignment=TextAnchor.MiddleCenter;glyphBacking.color=ButtonColor(primaryAction);
                    var glyphOutline=glyphBacking.GetComponent<Outline>();if(primaryAction==MenuPadButton.Start){if(!glyphOutline)glyphOutline=glyphBacking.gameObject.AddComponent<Outline>();glyphOutline.enabled=true;glyphOutline.effectColor=new Color32(141,160,173,255);glyphOutline.effectDistance=new Vector2(1,-1);}else if(glyphOutline)glyphOutline.enabled=false;
                    var backingRect=glyphBacking.rectTransform;backingRect.anchorMin=backingRect.anchorMax=new Vector2(1,y);backingRect.sizeDelta=new Vector2(buttonWidth-2,26);backingRect.anchoredPosition=new Vector2(-inset-buttonWidth*.5f,0);
                }
                else {glyph.alignment=TextAnchor.MiddleRight;}
                if(pair&&secondaryButton)
                {
                    shortcutGlyph.font=caption.font;shortcutGlyph.fontStyle=caption.fontStyle;shortcutGlyph.fontSize=glyph.fontSize;shortcutGlyph.text=direct;shortcutGlyph.color=Color.white;
                    var secondaryRect=shortcutGlyph.rectTransform;secondaryRect.sizeDelta=new Vector2(secondaryWidth,26);secondaryRect.anchoredPosition=new Vector2(-inset-buttonWidth-4-secondaryWidth*.5f,0);
                    var secondaryBg=shortcutBacking.rectTransform;secondaryBg.anchorMin=secondaryBg.anchorMax=new Vector2(1,y);secondaryBg.sizeDelta=new Vector2(secondaryWidth-2,26);secondaryBg.anchoredPosition=secondaryRect.anchoredPosition;
                    shortcutBacking.color=ButtonColor(Shortcut.Value);
                    var shortcutOutline=shortcutBacking.GetComponent<Outline>();if(Shortcut.Value==MenuPadButton.Start){if(!shortcutOutline)shortcutOutline=shortcutBacking.gameObject.AddComponent<Outline>();shortcutOutline.enabled=true;shortcutOutline.effectColor=new Color32(141,160,173,255);shortcutOutline.effectDistance=new Vector2(1,-1);}else if(shortcutOutline)shortcutOutline.enabled=false;
                }
                shortcutGlyph.enabled=pair&&secondaryButton&&show;shortcutBacking.enabled=shortcutGlyph.enabled;
                glyphBacking.enabled=show&&buttonGlyph;
            }
            glyph.enabled=show;if(!show){glyphBacking.enabled=false;shortcutGlyph.enabled=false;shortcutBacking.enabled=false;}
        }
        static Sprite Circle()
        {
            if(circle)return circle;
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="lobby-gamepad-button";
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size*.5f,size*.5f));pixels[y*size+x]=Color.white*Mathf.Clamp01(size*.5f+.5f-d);}
            texture.SetPixels(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;
            circle=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);return circle;
        }
        static string NeutralMenuGlyph(string value)=>value=="Menu"||value=="Options"||value=="+"?"≡":value;
        static bool IsButtonGlyph(string value)=>value.Length==1;
        static float GlyphWidth(string value)=>value.Length==1?30:58;
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
