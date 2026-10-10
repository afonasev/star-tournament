using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Canvas pixels only. Loading presentation never owns simulation or estimates progress.
    public sealed class NativeLoadingScreen:MonoBehaviour
    {
        public const string Credit="© 2026 Evgeniy Afonasev · afonasev.tech · Made with Codex";
        static readonly Color32 Gold=new Color32(231,187,114,255),Ink=new Color32(16,28,41,255),Muted=new Color32(168,185,198,255);
        GameObject startup,match;
        Text mapName,modeLabel,participants,target,status;
        GameObject targetGroup;
        RectTransform sweep,track;
        LoadingEmblem emblem;
        float started;
        bool failed;
        public bool Visible=>gameObject.activeSelf;
        public float AnimationPosition=>sweep.anchoredPosition.x;
        public static NativeLoadingScreen Create(Transform parent)
        {
            var root=new GameObject("loading-screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var screen=root.AddComponent<NativeLoadingScreen>();screen.Build();return screen;
        }
        static RectTransform Area(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        static Image Surface(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {var rect=Area(parent,name,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=color;return image;}
        static Text Label(Transform parent,string name,string value,int size,Vector2 min,Vector2 max,Color color,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var rect=Area(parent,name,min,max);var label=rect.gameObject.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=value;label.fontSize=size;label.color=color;label.alignment=align;label.raycastTarget=false;label.supportRichText=true;
            return label;
        }
        void Build()
        {
            Surface(transform,"loading-ink",Vector2.zero,Vector2.one,Ink);
            var art=Area(transform,"loading-art",Vector2.zero,Vector2.one);var image=art.gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("UI/menu-arena");image.raycastTarget=false;
            var fit=art.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=image.texture?(float)image.texture.width/image.texture.height:16f/9;
            var shade=Area(transform,"loading-shade",Vector2.zero,Vector2.one).gameObject.AddComponent<LoadingShade>();shade.raycastTarget=false;
            startup=Area(transform,"loading-startup",Vector2.zero,Vector2.one).gameObject;
            var icon=Area(startup.transform,"loading-emblem",new Vector2(.4325f,.625f),new Vector2(.5675f,.865f));
            emblem=icon.gameObject.AddComponent<LoadingEmblem>();emblem.raycastTarget=false;
            var brand=Label(startup.transform,"loading-brand","STAR <color=#E7BB72>TOURNAMENT</color>",65,new Vector2(.12f,.53f),new Vector2(.88f,.64f),Color.white);brand.fontStyle=FontStyle.Bold;
            Label(startup.transform,"loading-credit",Credit,16,new Vector2(.12f,.47f),new Vector2(.88f,.52f),Muted);
            match=Area(transform,"loading-match",Vector2.zero,Vector2.one).gameObject;
            var small=Label(match.transform,"loading-match-brand","STAR <color=#E7BB72>TOURNAMENT</color>",22,new Vector2(.06f,.875f),new Vector2(.5f,.935f),Color.white,TextAnchor.MiddleLeft);small.fontStyle=FontStyle.Bold;
            var card=Area(match.transform,"loading-match-card",new Vector2(.22f,.355f),new Vector2(.78f,.75f));
            var panel=card.gameObject.AddComponent<LoadingCardGraphic>();panel.color=new Color32(16,28,41,242);panel.raycastTarget=false;card.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var illustration=Area(card,"loading-card-art",new Vector2(.002f,.002f),new Vector2(.38f,.998f));var raw=illustration.gameObject.AddComponent<RawImage>();raw.texture=image.texture;raw.uvRect=new Rect(.5f,0,.35f,1);raw.raycastTarget=false;
            Surface(illustration,"loading-card-shade",Vector2.zero,Vector2.one,new Color32(7,16,27,70));
            Label(illustration,"loading-map-badge","ВЫБРАННАЯ КАРТА",14,new Vector2(.07f,.04f),new Vector2(.93f,.16f),Gold,TextAnchor.MiddleLeft);
            Label(card,"loading-next-match","ВАШ СЛЕДУЮЩИЙ МАТЧ",17,new Vector2(.425f,.81f),new Vector2(.96f,.94f),Gold,TextAnchor.MiddleLeft).fontStyle=FontStyle.Bold;
            mapName=Label(card,"loading-map-name","",45,new Vector2(.425f,.57f),new Vector2(.96f,.81f),Color.white,TextAnchor.MiddleLeft);mapName.fontStyle=FontStyle.Bold;mapName.resizeTextForBestFit=true;mapName.resizeTextMinSize=28;mapName.resizeTextMaxSize=45;
            modeLabel=Label(card,"loading-mode","",20,new Vector2(.425f,.46f),new Vector2(.96f,.59f),Muted,TextAnchor.MiddleLeft);
            Surface(card,"loading-card-rule",new Vector2(.425f,.37f),new Vector2(.96f,.373f),new Color32(62,83,99,255));
            var count=Area(card,"loading-participants",new Vector2(.425f,.19f),new Vector2(.56f,.32f));
            Surface(count,"loading-count-rule",Vector2.zero,new Vector2(.005f,1),Muted);
            Label(count,"loading-participants-title","УЧАСТНИКИ",15,new Vector2(.1f,.5f),new Vector2(1,1),Muted,TextAnchor.MiddleLeft);
            participants=Label(count,"loading-participants-count","",18,new Vector2(.1f,0),new Vector2(1,.5f),Color.white,TextAnchor.MiddleLeft);
            targetGroup=Area(card,"loading-target",new Vector2(.57f,.19f),new Vector2(.96f,.32f)).gameObject;
            Surface(targetGroup.transform,"loading-target-rule",Vector2.zero,new Vector2(.002f,1),Muted);
            Label(targetGroup.transform,"loading-target-title","ЦЕЛЬ",15,new Vector2(.04f,.5f),Vector2.one,Muted,TextAnchor.MiddleLeft);
            target=Label(targetGroup.transform,"loading-target-value","",18,new Vector2(.04f,0),new Vector2(1,.5f),Color.white,TextAnchor.MiddleLeft);
            Label(match.transform,"loading-match-credit",Credit,14,new Vector2(.05f,.001f),new Vector2(.95f,.023f),Muted);
            Label(match.transform,"loading-auto-start","Матч начнётся автоматически",20,new Vector2(.2f,.155f),new Vector2(.8f,.2f),Muted);
            status=Label(transform,"loading-status","",20,new Vector2(.2f,.225f),new Vector2(.8f,.265f),Color.white);
            var trackImage=Surface(transform,"loading-track",new Vector2(.3f,.205f),new Vector2(.7f,.208f),new Color32(52,68,82,255));
            track=trackImage.rectTransform;track.gameObject.AddComponent<RectMask2D>();
            sweep=Surface(track.transform,"loading-sweep",new Vector2(0,0),new Vector2(.18f,1),Gold).rectTransform;
        }
        void PositionIndicator(bool initial)
        {
            track.anchorMin=new Vector2(initial?.34f:.3f,initial?.15f:.205f);track.anchorMax=new Vector2(initial?.66f:.7f,initial?.153f:.208f);
            status.rectTransform.anchorMin=new Vector2(.2f,initial?.175f:.225f);status.rectTransform.anchorMax=new Vector2(.8f,initial?.215f:.265f);
        }
        public void ShowStartup(){PositionIndicator(true);gameObject.SetActive(true);startup.SetActive(true);match.SetActive(false);status.text="Загрузка игры";started=Time.unscaledTime;failed=false;}
        public void ShowMatch(string map,NativeMatchMode mode,int count,NativeMatchConfiguration configuration)
        {
            PositionIndicator(false);gameObject.SetActive(true);startup.SetActive(false);match.SetActive(true);started=Time.unscaledTime;failed=false;
            mapName.text=map;modeLabel.text=mode==NativeMatchMode.Teams?"Командный матч":"Каждый за себя";
            participants.text=count.ToString(CultureInfo.InvariantCulture);
            targetGroup.SetActive(configuration.TargetEnabled);target.text=configuration.TargetPoints.ToString("N0",CultureInfo.GetCultureInfo("ru-RU"))+" очков";
            status.text="Подготовка арены";
        }
        public void ShowError(string message){failed=true;status.text=message;}
        public void Hide(){gameObject.SetActive(false);}
        void Update()
        {
            if(failed)return;
            float t=Time.unscaledTime-started;
            sweep.anchoredPosition=new Vector2((Mathf.PingPong(t*.4f,1)*1.18f-.18f)*((RectTransform)sweep.parent).rect.width,0);
            emblem.Angle=t*60;emblem.SetVerticesDirty();
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingShade:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();var r=rectTransform.rect;
            // Same art and strong lower shade as the approved centered concept.
            for(int y=0;y<4;y++)for(int x=0;x<2;x++)
            {
                float a=x/2f,b=(x+1)/2f,c=y/4f,d=(y+1)/4f;
                var p=new[]{new Vector2(a,c),new Vector2(a,d),new Vector2(b,d),new Vector2(b,c)};int first=m.currentVertCount;
                foreach(var v in p){byte alpha=(byte)Mathf.Lerp(245,145,v.y);m.AddVert(new Vector3(Mathf.Lerp(r.xMin,r.xMax,v.x),Mathf.Lerp(r.yMin,r.yMax,v.y)),new Color32(7,16,27,alpha),Vector2.zero);}
                m.AddTriangle(first,first+1,first+2);m.AddTriangle(first,first+2,first+3);
            }
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingCardGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();var r=rectTransform.rect;float radius=18;int first=m.currentVertCount;m.AddVert(r.center,color,Vector2.zero);
            const int steps=8;
            for(int corner=0;corner<4;corner++)for(int step=0;step<=steps;step++)
            {
                float angle=(90-corner*90-step*90f/steps)*Mathf.Deg2Rad;
                var center=new Vector2(corner<2?r.xMax-radius:r.xMin+radius,corner==0||corner==3?r.yMax-radius:r.yMin+radius);
                m.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
            }
            int n=4*(steps+1);for(int i=0;i<n;i++)m.AddTriangle(first,first+1+i,first+1+(i+1)%n);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingEmblem:MaskableGraphic
    {
        public float Angle;
        public const string Credit="© 2026 Evgeniy Afonasev · afonasev.tech · Made with Codex";
        static readonly Color32 Gold=new Color32(231,187,114,255);
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();var r=rectTransform.rect;float radius=Mathf.Min(r.width,r.height)*.5f;var center=r.center;
            for(int i=0;i<4;i++){float a=(90+i*90)*Mathf.Deg2Rad,b=a+Mathf.PI*.5f;Stroke(m,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,1,new Color32(231,187,114,95));}
            for(int i=0;i<48;i++){float a=(Angle+i*3)*Mathf.Deg2Rad,b=(Angle+(i+1)*3)*Mathf.Deg2Rad;Stroke(m,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.82f,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius*.82f,2,Gold);}
            int first=m.currentVertCount;m.AddVert(center,Gold,Vector2.zero);
            for(int i=0;i<10;i++){float a=(90+i*36)*Mathf.Deg2Rad;float length=radius*(i%2==0?.39f:.17f);m.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*length,Gold,Vector2.zero);}
            for(int i=0;i<10;i++)m.AddTriangle(first,first+1+i,first+1+(i+1)%10);
        }
        static void Stroke(VertexHelper m,Vector2 a,Vector2 b,float width,Color color)
        {var v=(b-a).normalized;var normal=new Vector2(-v.y,v.x)*width*.5f;int n=m.currentVertCount;m.AddVert(a-normal,color,Vector2.zero);m.AddVert(a+normal,color,Vector2.zero);m.AddVert(b+normal,color,Vector2.zero);m.AddVert(b-normal,color,Vector2.zero);m.AddTriangle(n,n+1,n+2);m.AddTriangle(n,n+2,n+3);}
    }
}
