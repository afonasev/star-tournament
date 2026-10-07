using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace StarTournament.ProvingGround
{
    public sealed class MenuScrollFocus:MonoBehaviour,ISelectHandler
    {
        public ScrollRect Scroll;
        public void OnSelect(BaseEventData e)=>Reveal();
        public void Reveal(){if(!Scroll)return;Canvas.ForceUpdateCanvases();var item=(RectTransform)transform;float height=Scroll.content.rect.height-Scroll.viewport.rect.height;if(height<=0)return;float bottom=-item.anchoredPosition.y+item.rect.height;float top=-item.anchoredPosition.y;float offset=Scroll.content.anchoredPosition.y;if(top<offset)offset=top;else if(bottom>offset+Scroll.viewport.rect.height)offset=bottom-Scroll.viewport.rect.height;Scroll.verticalNormalizedPosition=1-Mathf.Clamp01(offset/height);}
    }
}
