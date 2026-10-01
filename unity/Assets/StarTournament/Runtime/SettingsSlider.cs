using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed class SettingsSlider : Slider
    {
        public float Step;
        public override void OnMove(AxisEventData data)
        {
            if(!IsActive()||!IsInteractable())return;
            if(data.moveDir==MoveDirection.Left||data.moveDir==MoveDirection.Right)
            { value+=data.moveDir==MoveDirection.Left?-Step:Step;data.Use(); }
            else base.OnMove(data);
        }
    }
}
