using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StarTournament.ProvingGround
{
    /// <summary>Selecting a row changes only the transient results perspective.</summary>
    public sealed class NativeStandingsSelection : MonoBehaviour, ISelectHandler
    {
        public Action Selected;
        public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
    }
}
