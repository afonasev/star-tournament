using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        void DrawHelpDiagram(Transform parent,bool pad)
        {
            foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var area=new GameObject("help-image-area",typeof(RectTransform));area.transform.SetParent(parent,false);
            Layout((RectTransform)area.transform,pad?new Vector2(0,.23f):Vector2.zero,Vector2.one);
            var go=new GameObject("help-illustration",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(area.transform,false);
            Layout((RectTransform)go.transform,Vector2.zero,Vector2.one);
            var image=go.GetComponent<RawImage>();image.texture=Resources.Load<Texture2D>(pad?"UI/help-gamepad":"UI/help-keyboard");image.raycastTarget=false;
            var fit=go.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
            if(image.texture)fit.aspectRatio=(float)image.texture.width/image.texture.height;
            else Label(area.transform,"help-image-missing","Иллюстрация управления недоступна. Вернитесь в настройки и повторите открытие справки.",24,new Vector2(.05f,.25f),new Vector2(.95f,.75f),TextAnchor.MiddleCenter,new Color32(255,218,184,255));
            if(pad)
            {
                var caption=Panel(parent,"help-lt-aim-caption",new Vector2(.025f,.015f),new Vector2(.975f,.22f),new Color32(12,23,35,245));
                Label(caption.transform,"help-lt-aim-text","LT / L2 / ZL: короткое нажатие — сброс к горизонту (плавно); удерживать — фиксация высоты прицела",20,
                    new Vector2(.02f,.62f),new Vector2(.98f,.98f),TextAnchor.MiddleCenter,new Color32(255,211,126,255));
                var equivalents=Label(caption.transform,"help-device-equivalents","Xbox → PlayStation → Switch: A → × → B · RT → R2 → ZR · LT → L2 → ZL\nLB / RB → L1 / R1 → L / R · View → Share → − · Menu → Options → +",20,
                    new Vector2(.02f,.02f),new Vector2(.98f,.60f),TextAnchor.MiddleCenter,Color.white);
                equivalents.resizeTextForBestFit=true;equivalents.resizeTextMinSize=14;equivalents.resizeTextMaxSize=20;
            }
        }
    }
}
