using System;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    // Shared menu artwork for both the compact lobby card and the map selector.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArenaPreviewGraphic : RawImage
    {
        string map;
        public void Show(string id)
        {
            if(map==id)return;
            var artwork=Resources.Load<Texture2D>("MapMenuArt/"+id);
            if(!artwork)throw new InvalidOperationException("Missing menu artwork for "+id);
            map=id;texture=artwork;raycastTarget=false;UpdateCrop();
        }
        protected override void OnRectTransformDimensionsChange()
        {base.OnRectTransformDimensionsChange();UpdateCrop();}
        void UpdateCrop()
        {
            if(!texture)return;
            var rect=rectTransform.rect;if(rect.width<=0||rect.height<=0)return;
            float imageAspect=(float)texture.width/texture.height,viewAspect=rect.width/rect.height;
            if(imageAspect>viewAspect)
            {float width=viewAspect/imageAspect;uvRect=new Rect((1-width)*.5f,0,width,1);}
            else
            {float height=imageAspect/viewAspect;uvRect=new Rect(0,(1-height)*.5f,1,height);}
        }
    }
}
