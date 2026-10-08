using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StarTournament.ProvingGround
{
    // Navigation chooses the map immediately; Submit confirms it. Pointer clicks keep the button action.
    public sealed class SetupMapChoice : MonoBehaviour, ISelectHandler, ISubmitHandler
    {
        Action select,confirm;
        public void Initialize(Action onSelect,Action onConfirm){select=onSelect;confirm=onConfirm;}
        public void OnSelect(BaseEventData eventData)=>select?.Invoke();
        public void OnSubmit(BaseEventData eventData)=>confirm?.Invoke();
    }
}
