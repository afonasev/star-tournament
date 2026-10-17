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
        public Func<bool> Active,Focused,ShortcutActive;
        public MenuPadButton? Shortcut;
        public Func<float> ReservedRightInset;
        Selectable button;Text caption,glyph;
        public string PlainCaption=>caption?caption.text:null;
        public Text GlyphLabel=>glyph;
        public void Initialize(Selectable target,Func<InputDevice> device,Func<bool> active,Func<bool> focused=null)
        {
            button=target;caption=target is InputField field?field.textComponent:target.transform.Find("label").GetComponent<Text>();Device=device;Active=active;Focused=focused??(()=>EventSystem.current&&EventSystem.current.currentSelectedGameObject==target.gameObject);
            if(!glyph)
            {
                var node=new GameObject("gamepad-action-hint",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));node.transform.SetParent(target.transform,false);
                glyph=node.GetComponent<Text>();glyph.raycastTarget=false;glyph.alignment=TextAnchor.MiddleRight;glyph.supportRichText=false;
                var rect=glyph.rectTransform;rect.anchorMin=new Vector2(1,0);rect.anchorMax=Vector2.one;rect.offsetMax=new Vector2(-8,0);
            }
            glyph.enabled=false;
        }
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!button||!caption||!glyph)return;
            var device=Device?.Invoke();
            bool focused=Focused?.Invoke()??false,shortcut=Shortcut.HasValue&&(ShortcutActive?.Invoke()??true);
            bool show=button.IsActive()&&button.IsInteractable()&&(Active?.Invoke()??false)&&(focused||shortcut)&&device is Gamepad&&device.added&&device.enabled;
            if(show)
            {
                var family=MenuGamepadHints.Family(device.layout,device.description.product);
                glyph.font=caption.font;glyph.fontStyle=caption.fontStyle;glyph.fontSize=Mathf.Min(caption.fontSize,family==MenuPadFamily.Generic?18:22);glyph.color=caption.color;
                string direct=shortcut?MenuGamepadHints.Glyph(family,Shortcut.Value):null;
                glyph.text=focused?MenuGamepadHints.Glyph(family,MenuPadButton.South):direct;
                if(focused&&shortcut&&Shortcut.Value!=MenuPadButton.South)glyph.text+=" / "+direct;
                float width=glyph.preferredWidth+4,inset=Mathf.Max(8,ReservedRightInset?.Invoke()??8);
                // Keep the value/title rectangle untouched. Omit a hint when there is
                // no free space beside the caption (for example a narrow numeric key).
                bool left=caption.alignment==TextAnchor.MiddleLeft||caption.alignment==TextAnchor.UpperLeft||caption.alignment==TextAnchor.LowerLeft;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform,caption.rectTransform);
                float captionEnd=left?bounds.min.x+caption.preferredWidth:bounds.center.x+caption.preferredWidth*.5f;
                float space=((RectTransform)button.transform).rect.xMax-inset-captionEnd-4;
                if(space<width&&focused&&shortcut){glyph.text=direct;width=glyph.preferredWidth+4;}
                show=space>=width;
                glyph.rectTransform.offsetMin=new Vector2(-width-inset,0);glyph.rectTransform.offsetMax=new Vector2(-inset,0);
            }
            glyph.enabled=show;
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
