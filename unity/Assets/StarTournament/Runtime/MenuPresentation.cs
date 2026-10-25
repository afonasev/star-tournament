using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Presentation-only metrics for the approved menu composition, in Canvas reference pixels.
    // These never feed simulation, camera or in-game readability/balance profiles.
    public sealed class MenuPresentation : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float ButtonHeight = 46;
        public bool Compact = true;
        public bool FilledFocus;
        // Opt-in for the match-lobby visual slice; other menu families keep their current palette.
        public bool LobbySurface;
        public Color? SurfaceTint;
        Text optionCaption;
        public float Height = ButtonHeight;
        Outline border;
        Image lobbyRim;
        Button button;
        GameObject focusFrame;
        public bool FocusVisible => focusFrame && focusFrame.activeSelf;
        bool selected, hover, resizing;
        static readonly Color Gold = new Color32(231,187,114,255);
        static readonly Color Line = new Color32(55,75,94,255);
        void Awake()
        {
            button=GetComponent<Button>();
            border=gameObject.AddComponent<Outline>();border.effectDistance=new Vector2(1,-1);border.useGraphicAlpha=true;
            focusFrame=new GameObject("gold-focus",typeof(RectTransform),typeof(CanvasRenderer),typeof(MenuFocusGraphic));
            focusFrame.transform.SetParent(transform,false);
            focusFrame.transform.SetAsFirstSibling();
            var frameRect=(RectTransform)focusFrame.transform;
            frameRect.anchorMin=Vector2.zero;frameRect.anchorMax=Vector2.one;
            frameRect.offsetMin=frameRect.offsetMax=Vector2.zero;
            focusFrame.GetComponent<MenuFocusGraphic>().raycastTarget=false;
            Paint();
        }
        void LateUpdate()
        {
            // Hierarchy changes cannot run from OnEnable/OnDisable during parent activation.
            if(LobbySurface&&button)
            {
                if(!lobbyRim)
                {
                    var node=new GameObject("lobby-surface-rim",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));node.transform.SetParent(transform,false);
                    lobbyRim=node.GetComponent<Image>();lobbyRim.sprite=LobbyVisualAssets.Rim;lobbyRim.type=Image.Type.Sliced;lobbyRim.raycastTarget=false;
                    var rect=lobbyRim.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                    lobbyRim.transform.SetAsLastSibling();focusFrame.transform.SetAsLastSibling();
                }
            }
            focusFrame.GetComponent<MenuFocusGraphic>().LobbyStyle=LobbySurface;
            Paint();
        }
        void Paint()
        {
            if(!border)return;
            bool enabledButton=button && button.interactable;
            bool focus=enabledButton && selected;
            if(FilledFocus && button)
            {
                bool highlighted=enabledButton&&(selected||hover);
                button.GetComponent<Image>().color=highlighted?Gold:new Color32(34,55,74,255);
                if(!optionCaption)optionCaption=GetComponentInChildren<Text>();
                if(optionCaption)optionCaption.color=highlighted?new Color32(20,35,49,255):new Color32(242,244,246,255);
            }
            else if(LobbySurface&&button)
            {
                var surface=button.GetComponent<Image>();surface.sprite=SurfaceTint.HasValue?LobbyVisualAssets.TintedPanel:LobbyVisualAssets.Panel;surface.type=Image.Type.Simple;
                surface.color=(SurfaceTint??Color.white)*(enabledButton?(hover?new Color(1.16f,1.16f,1.16f,1):Color.white):new Color(.68f,.68f,.68f,.85f));
                // Outline duplicates the entire translucent quad four times. A real hollow
                // border preserves the authored material and avoids timing-dependent opacity.
                border.enabled=false;
                if(lobbyRim)lobbyRim.color=hover?Gold:new Color32(105,139,157,205);
            }
            if(focusFrame && focusFrame.activeSelf!=focus)focusFrame.SetActive(focus);
            border.effectColor=enabledButton&&hover?Gold:LobbySurface?new Color32(84,111,132,210):Line;
            border.effectDistance=enabledButton&&hover?new Vector2(2,-2):new Vector2(1,-1);
        }
        public void SetFocused(bool value){selected=value;Paint();}
        public void OnSelect(BaseEventData e){selected=true;Paint();}
        public void OnDeselect(BaseEventData e){selected=false;Paint();}
        public void OnPointerEnter(PointerEventData e){hover=true;Paint();}
        public void OnPointerExit(PointerEventData e){hover=false;Paint();}
        void OnDisable(){selected=hover=false;Paint();}
        void OnEnable(){Resize();Paint();}
        void OnRectTransformDimensionsChange(){Resize();}
        void Resize()
        {
            if(!Compact||resizing)return;
            var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;
            if(!parent||rect.anchorMin.y==rect.anchorMax.y)return;
            float inset=Mathf.Max(0,((rect.anchorMax.y-rect.anchorMin.y)*parent.rect.height-Height)*.5f);
            if(Mathf.Abs(rect.offsetMin.y-inset)<.1f && Mathf.Abs(rect.offsetMax.y+inset)<.1f)return;
            resizing=true;
            rect.offsetMin=new Vector2(rect.offsetMin.x,inset);rect.offsetMax=new Vector2(rect.offsetMax.x,-inset);
            resizing=false;
        }
    }
    // Inset strokes keep compact buttons and scroll rows from overlapping.
    // The gold rim and dark gap scale together in split-screen Canvas space.
    public sealed class MenuFocusGraphic : MaskableGraphic
    {
        bool lobbyStyle;
        public bool LobbyStyle {get=>lobbyStyle;set{if(lobbyStyle==value)return;lobbyStyle=value;SetVerticesDirty();}}
        public static float StrokeWidth(float height)=>Mathf.Min(7,height*.08f);
        public static float GapWidth(float height)=>Mathf.Min(6,height*.06f);
        static readonly Color32 Gold=new Color32(231,187,114,255);
        static readonly Color32 Ink=new Color32(9,21,34,255);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var outer=rectTransform.rect;
            if(outer.width<=0||outer.height<=0)return;
            float stroke=StrokeWidth(outer.height),gap=GapWidth(outer.height);
            var middle=Inset(outer,stroke);var inner=Inset(middle,gap);
            if(LobbyStyle)
            {
                Ring(mesh,outer,Inset(outer,3.5f),10,6.5f,new Color32(231,187,114,40));
                Ring(mesh,outer,Inset(outer,2),10,8,Gold);
            }
            else
            {
                Ring(mesh,outer,middle,10,Mathf.Max(1,10-stroke),Gold);
                Ring(mesh,middle,inner,Mathf.Max(1,10-stroke),Mathf.Max(1,10-stroke-gap),Ink);
            }
            float cy=outer.center.y,h=Mathf.Min(15,outer.height*.18f);
            int v=mesh.currentVertCount;
            mesh.AddVert(new Vector3(outer.xMin-16,cy-h,0),Gold,Vector2.zero);
            mesh.AddVert(new Vector3(outer.xMin-16,cy+h,0),Gold,Vector2.zero);
            mesh.AddVert(new Vector3(outer.xMin-6,cy,0),Gold,Vector2.zero);
            mesh.AddTriangle(v,v+1,v+2);
        }
        static Rect Inset(Rect r,float amount)=>new Rect(r.x+amount,r.y+amount,Mathf.Max(0,r.width-2*amount),Mathf.Max(0,r.height-2*amount));
        static Vector2[] Contour(Rect r,float radius)
        {
            const int steps=6;var points=new Vector2[4*(steps+1)];
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner<2?r.xMax-radius:r.xMin+radius,corner==0||corner==3?r.yMax-radius:r.yMin+radius);
                for(int step=0;step<=steps;step++)
                {
                    float a=(90-corner*90-step*90f/steps)*Mathf.Deg2Rad;
                    points[corner*(steps+1)+step]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                }
            }
            return points;
        }
        static void Ring(VertexHelper mesh,Rect outer,Rect inner,float outerRadius,float innerRadius,Color32 color)
        {
            var a=Contour(outer,outerRadius);var b=Contour(inner,innerRadius);int first=mesh.currentVertCount;
            for(int i=0;i<a.Length;i++){mesh.AddVert(a[i],color,Vector2.zero);mesh.AddVert(b[i],color,Vector2.zero);}
            for(int i=0;i<a.Length;i++)
            {
                int next=(i+1)%a.Length;int v=first+i*2,n=first+next*2;
                mesh.AddTriangle(v,n,v+1);mesh.AddTriangle(n,n+1,v+1);
            }
        }
    }
}
