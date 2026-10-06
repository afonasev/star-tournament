#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Labelled placement/camera fixture, actual native shader and skin. Not human acceptance.</summary>
    public sealed class NativeColorSurfaceReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Camera review;
        IEnumerator Capture(string label)
        {
            yield return null;yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(ground.Composition.Read(),true));
            var diagnostics=new System.Text.StringBuilder();
            var identityShader=Resources.Load<Shader>("TrooperIdentity");
            diagnostics.AppendLine("Shader properties: "+string.Join(",",Enumerable.Range(0,identityShader.GetPropertyCount()).Select(identityShader.GetPropertyName)));
            foreach(var renderer in ground.GetComponentsInChildren<Renderer>(true))
                for(int i=0;i<renderer.sharedMaterials.Length;i++)
                {
                    var mat=renderer.sharedMaterials[i];if(!mat)continue;
                    if(!mat.HasProperty("_IdentityColor")){diagnostics.AppendLine(renderer.transform.parent.name+"/"+renderer.name+"/"+mat.name+" shader="+mat.shader.name+" NO_IDENTITY_PROPERTIES");continue;}
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
                    diagnostics.AppendLine(renderer.transform.parent.name+"/"+renderer.name+"/"+mat.name+" id="+mat.GetInstanceID()+" shader="+mat.shader.name+" material="+mat.GetColor("_IdentityColor")+" mode="+mat.GetFloat("_IdentityMode")+" tuning="+mat.GetVector("_IdentitySurface")+" block="+block.GetColor("_IdentityColor")+" mode="+block.GetFloat("_IdentityMode"));
                }
            File.WriteAllText(Path.Combine(directory,label+"-materials.txt"),diagnostics.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return new WaitForSecondsRealtime(.4f);
        }
        void Line(float yaw)
        {
            // QA-only stage/camera dimensions: these do not participate in gameplay or Balance Lab.
            for(int p=0;p<8;p++)ground.PlaceCombatReviewSeat(p,new Vector3((p-3.5f)*.7f,4,0),0,yaw);
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-colorSurfaceEvidence");
            directory=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"color-surface-review");Directory.CreateDirectory(directory);
            AudioListener.volume=0;
            // Autonomous material/camera fixture: foreground focus is not a gate for albedo.
            // Physical controls/focus are outside this review; no such acceptance is claimed.
            Application.runInBackground=true;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed); // Fixed QA capture size; no saved display preference write.
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            ground.StartParticipantReview(NativeParticipantReview.Mixed(1,false));yield return null;Time.timeScale=0;
            review=new GameObject("color-surface-fixture-camera").AddComponent<Camera>();review.depth=100;
            review.cullingMask=~(15<<15);review.fieldOfView=52;review.nearClipPlane=.05f;
            review.transform.position=new Vector3(0,5.1f,-4);review.transform.LookAt(new Vector3(0,4.9f,0));
            Line(180);yield return Capture("01-eight-front");Line(90);yield return Capture("02-eight-side");Line(0);yield return Capture("03-eight-rear");
            // Close native camera views bind helmet/front/side/rear and source texture detail.
            review.transform.position=new Vector3(-2.45f,5.45f,-1.65f);review.transform.LookAt(new Vector3(-2.45f,5.4f,0));
            Line(180);yield return Capture("04-helmet-front");Line(90);yield return Capture("05-helmet-side");Line(0);yield return Capture("06-helmet-rear");
            Destroy(review.gameObject);Time.timeScale=1;
            ground.StartParticipantReview(NativeParticipantReview.Mixed(4,false));yield return null;Time.timeScale=0;
            ground.PlaceCombatReviewSeat(ground.Composition.ParticipantAt(0),new Vector3(0,4,-3),0,0);
            for(int p=0;p<4;p++)ground.PlaceCombatReviewSeat(p,new Vector3((p-1.5f)*1.2f,4,0),0,180);
            yield return Capture("07-four-views");
            Time.timeScale=1;ground.StartParticipantReview(NativeParticipantReview.Mixed(4,true));yield return null;Time.timeScale=0;
            yield return Capture("08-team-first-person");
            Time.timeScale=1;ground.StartParticipantReview(NativeParticipantReview.Mixed(1,true));yield return null;Time.timeScale=0;
            yield return Capture("09-team-single-view");
            Time.timeScale=1;ground.StartParticipantReview(NativeParticipantReview.Mixed(1,false));yield return null;Time.timeScale=0;
            var boosted=ground.Session.Capture();boosted.DamageRemaining[ground.Composition.ParticipantAt(0)]=5;ground.Session.Restore(boosted);
            yield return Capture("10-white-first-person-boost");
            File.WriteAllText(Path.Combine(directory,"profile.json"),JsonUtility.ToJson(ground.ParticipantPaletteProfile,true));
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"complete\":true,\"muted\":true,\"placementCameraFixture\":true,\"humanAcceptance\":false}");
            Time.timeScale=1;Debug.Log("COLOR_SURFACE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){Time.timeScale=1;if(review)Destroy(review.gameObject);}
    }
}
#endif

#endif
