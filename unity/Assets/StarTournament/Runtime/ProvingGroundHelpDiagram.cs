using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        void DrawHelpDiagram(Transform parent,bool pad)
        {
            foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var go=new GameObject("help-illustration",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            Layout((RectTransform)go.transform,Vector2.zero,Vector2.one);
            var image=go.GetComponent<RawImage>();image.texture=Resources.Load<Texture2D>(pad?"UI/help-gamepad":"UI/help-keyboard");image.raycastTarget=false;
        }
    }
}
