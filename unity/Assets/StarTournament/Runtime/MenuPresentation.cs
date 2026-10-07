using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Presentation-only metrics for the approved menu composition, in Canvas reference pixels.
    // These never feed simulation, camera or in-game readability/balance profiles.
    public sealed class MenuPresentation : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float ButtonHeight = 46;
        public bool Compact = true;
        public float Height = ButtonHeight;
        Outline border;
        Button button;
        bool selected, hover, resizing;
        static readonly Color Gold = new Color32(231,187,114,255);
        static readonly Color Line = new Color32(55,75,94,255);
        void Awake()
        {
            button=GetComponent<Button>();
            border=gameObject.AddComponent<Outline>();border.effectDistance=new Vector2(1,-1);border.useGraphicAlpha=true;
            Paint();
        }
        void Paint()
        {
            if(!border)return;
            bool focus=button && button.interactable && (selected||hover);
            border.effectColor=focus?Gold:Line;border.effectDistance=focus?new Vector2(2,-2):new Vector2(1,-1);
        }
        public void SetFocused(bool value){selected=value;Paint();}
        public void OnSelect(BaseEventData e){selected=true;Paint();}
        public void OnDeselect(BaseEventData e){selected=false;Paint();}
        public void OnPointerEnter(PointerEventData e){hover=true;Paint();}
        public void OnPointerExit(PointerEventData e){hover=false;Paint();}
        void OnDisable(){selected=hover=false;Paint();}
        void OnEnable(){Resize();Paint();}
        void OnRectTransformDimensionsChange(){Resize();}
        void Resize()
        {
            if(!Compact||resizing)return;
            var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;
            if(!parent||rect.anchorMin.y==rect.anchorMax.y)return;
            float inset=Mathf.Max(0,((rect.anchorMax.y-rect.anchorMin.y)*parent.rect.height-Height)*.5f);
            if(Mathf.Abs(rect.offsetMin.y-inset)<.1f && Mathf.Abs(rect.offsetMax.y+inset)<.1f)return;
            resizing=true;
            rect.offsetMin=new Vector2(rect.offsetMin.x,inset);rect.offsetMax=new Vector2(rect.offsetMax.x,-inset);
            resizing=false;
        }
    }
}
