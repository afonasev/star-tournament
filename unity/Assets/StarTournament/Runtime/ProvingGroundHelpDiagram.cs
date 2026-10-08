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
            if(pad)
            {
                var caption=Panel(parent,"help-lt-aim-caption",new Vector2(.08f,.015f),new Vector2(.92f,.105f),new Color32(12,23,35,245));
                Label(caption.transform,"help-lt-aim-text","LT: короткое нажатие — сброс к горизонту · удерживать — фиксация высоты прицела",20,
                    new Vector2(.02f,.05f),new Vector2(.98f,.95f),TextAnchor.MiddleCenter,new Color32(255,211,126,255));
            }
        }
    }
}
