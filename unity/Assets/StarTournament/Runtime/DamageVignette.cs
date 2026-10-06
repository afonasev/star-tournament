using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Presentation impulse only; its sole clock is the combat session clock.</summary>
    public sealed class DamageVignettePulse
    {
        double began;
        float start,attack,fade,maximum;
        bool active;
        public bool HealthHit { get; private set; }
        public void Reset(){active=false;start=0;}
        public void Hit(DamageNotice notice,ProvingProfile profile)
        {
            if(notice.HealthLost<=0&&notice.ArmorLost<=0)return;
            start=Sample(notice.Time);began=notice.Time;active=true;
            HealthHit=notice.HealthLost>0;
            attack=profile.Get("ui.damageVignette.attackSeconds");
            fade=profile.Get("ui.damageVignette.fadeSeconds");
            maximum=profile.Get("ui.damageVignette.opacity");
        }
        public float Sample(double time)
        {
            if(!active||time<began)return 0;
            float age=(float)(time-began);
            if(age<attack)return Mathf.Lerp(start,maximum,Mathf.SmoothStep(0,1,age/attack));
            return maximum*(1-Mathf.SmoothStep(0,1,(age-attack)/fade));
        }
    }

    /// <summary>A smooth rectangular border under the seat HUD; never intercepts input.</summary>
    public sealed class DamageVignette:MaskableGraphic
    {
        // Semantic shield/health colors and tessellation are fixed UI design, not gameplay tuning.
        public static readonly Color ShieldColor=new Color32(40,130,255,255);
        public static readonly Color HealthColor=new Color32(235,45,50,255);
        const int Rings=16;
        readonly DamageVignettePulse pulse=new DamageVignettePulse();
        NativeCombatSession session;
        ProvingProfile profile;
        int participant=-1;
        float width;
        public float Opacity=>color.a;
        public bool HealthHit=>pulse.HealthHit;
        protected override void Awake(){base.Awake();raycastTarget=false;color=Color.clear;}
        public void Bind(NativeCombatSession next,int owner,ProvingProfile tuning)
        {
            if(session!=null){session.Damaged-=OnDamage;session.Died-=OnDeath;session.Respawned-=OnRespawn;session.StateRestored-=Clear;}
            session=next;participant=owner;profile=tuning;
            float nextWidth=profile.Get("ui.damageVignette.width");
            if(width!=nextWidth){width=nextWidth;SetVerticesDirty();}
            Clear();
            if(session!=null){session.Damaged+=OnDamage;session.Died+=OnDeath;session.Respawned+=OnRespawn;session.StateRestored+=Clear;}
        }
        void OnDamage(DamageNotice notice)
        {
            if(notice.Participant==participant&&notice.Life==session.Life(participant).Life)pulse.Hit(notice,profile);
        }
        void OnDeath(DeathNotice notice){if(notice.Seat==participant)Clear();}
        void OnRespawn(int owner){if(owner==participant)Clear();}
        public void Clear(){pulse.Reset();color=Color.clear;}
        public void Render(bool visible)
        {
            var tint=pulse.HealthHit?HealthColor:ShieldColor;
            tint.a=visible&&session!=null&&!session.Life(participant).Dead?pulse.Sample(session.Time):0;
            if(color!=tint)color=tint;
        }
        protected override void OnDestroy(){Bind(null,-1,profile??ProvingProfile.CreateDefault());base.OnDestroy();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(width<=0||color.a<=0)return;
            var rect=GetPixelAdjustedRect();
            for(int ring=0;ring<=Rings;ring++)
            {
                float t=(float)ring/Rings;
                float x=rect.width*width*t,y=rect.height*width*t;
                Color tint=color;tint.a*=1-Mathf.SmoothStep(0,1,t);
                mesh.AddVert(new Vector3(rect.xMin+x,rect.yMin+y),tint,Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin+x,rect.yMax-y),tint,Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMax-x,rect.yMax-y),tint,Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMax-x,rect.yMin+y),tint,Vector2.zero);
                if(ring==0)continue;
                int outer=(ring-1)*4,inner=ring*4;
                for(int edge=0;edge<4;edge++)
                {
                    int next=(edge+1)%4;
                    mesh.AddTriangle(outer+edge,outer+next,inner+next);
                    mesh.AddTriangle(outer+edge,inner+next,inner+edge);
                }
            }
        }
    }
}
