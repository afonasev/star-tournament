using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace StarTournament.ProvingGround
{
    public sealed class LabFocusHint:MonoBehaviour,IPointerEnterHandler,ISelectHandler
    {
        public Action Show,Focus;
        public void OnPointerEnter(PointerEventData e)=>Show?.Invoke();
        public void OnSelect(BaseEventData e){Show?.Invoke();Focus?.Invoke();}
    }
}
