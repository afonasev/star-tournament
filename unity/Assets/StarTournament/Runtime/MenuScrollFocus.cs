using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace StarTournament.ProvingGround
{
    public sealed class MenuScrollFocus:MonoBehaviour,ISelectHandler
    {
        public ScrollRect Scroll;
        public RectTransform Row;
        public void OnSelect(BaseEventData e)=>Reveal();
        public void Reveal(){if(!Scroll)return;Canvas.ForceUpdateCanvases();var item=Row?Row:(RectTransform)transform;float height=Scroll.content.rect.height-Scroll.viewport.rect.height;if(height<=0)return;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(Scroll.content,item);float top=Scroll.content.rect.yMax-bounds.max.y;float bottom=Scroll.content.rect.yMax-bounds.min.y;Scroll.StopMovement();const float padding=2;float offset=Scroll.content.anchoredPosition.y;if(top-padding<offset)offset=top-padding;else if(bottom+padding>offset+Scroll.viewport.rect.height)offset=bottom+padding-Scroll.viewport.rect.height;Scroll.verticalNormalizedPosition=1-Mathf.Clamp01(offset/height);}
    }
}
