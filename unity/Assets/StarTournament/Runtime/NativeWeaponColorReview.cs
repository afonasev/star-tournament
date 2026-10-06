#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // Diagnostic only: frozen placement and snapshot boost fixture; no material changes.
    public sealed class NativeWeaponColorReview : MonoBehaviour
    {
        ProvingGround ground; Camera review; string directory;
        string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
        IEnumerator Capture(string label)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var report=new StringBuilder();
            foreach(var model in ground.GetComponentsInChildren<WeaponModelPresentation>(true))
            {
                report.AppendLine(PathOf(model.transform)+" shown="+model.ShownWeapon);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.name!="weapon:joined" && !renderer.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter"))continue;
                    report.AppendLine(" renderer="+PathOf(renderer.transform)+" active="+renderer.gameObject.activeInHierarchy+" enabled="+renderer.enabled+" layer="+renderer.gameObject.layer);
                    for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                    {
                        var mat=renderer.sharedMaterials[slot]; if(!mat)continue;
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,slot);
                        report.AppendLine("  slot="+slot+" material="+mat.name+" shader="+mat.shader.name+" blockEmpty="+block.isEmpty);
                        foreach(string prop in new[]{"_BaseColor","baseColorFactor","_Color","_EmissionColor","emissiveFactor","_IdentityColor"})
                            if(mat.HasProperty(prop))report.AppendLine("   "+prop+" material="+mat.GetColor(prop)+" block="+block.GetColor(prop));
                        foreach(string prop in new[]{"_BaseMap","baseColorTexture","_MainTex"})
                            if(mat.HasProperty(prop))report.AppendLine("   "+prop+" texture="+(mat.GetTexture(prop)?mat.GetTexture(prop).name:"null"));
                    }
                }
            }
            File.WriteAllText(System.IO.Path.Combine(directory,label+"-materials.txt"),report.ToString());
            File.WriteAllText(System.IO.Path.Combine(directory,label+"-snapshot.json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory,label+".png"));
            yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();
            directory=args[Array.IndexOf(args,"-weaponColorReview")+1];Directory.CreateDirectory(directory);
            AudioListener.volume=0;Application.runInBackground=true;Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            ground.StartParticipantReview(NativeParticipantReview.Mixed(1,false));ground.FullHealReviewManualTick=true;
            yield return null;Time.timeScale=0;
            int local=ground.Composition.ParticipantAt(0),remote=0;
            File.WriteAllText(System.IO.Path.Combine(directory,"participants.json"),"{\"local\":"+local+",\"remote\":"+remote+"}");
            ground.PlaceCombatReviewSeat(local,new Vector3(0,4,-2.7f),8,0);
            ground.PlaceCombatReviewSeat(remote,new Vector3(0,4,0),0,180);
            for(int p=0;p<ground.Session.ParticipantCount;p++)if(p!=local&&p!=remote)ground.PlaceCombatReviewSeat(p,new Vector3(20+p*2,4,0),0,180);
            review=new GameObject("weapon-color-fixture-camera").AddComponent<Camera>();review.depth=100;review.cullingMask=~(15<<15);review.fieldOfView=45;review.nearClipPlane=.05f;
            review.transform.position=new Vector3(.9f,5.15f,-1.8f);review.transform.LookAt(new Vector3(0,5.05f,-.1f));
            foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                foreach(string phase in new[]{"normal","boost","restored"})
                {
                    var snapshot=ground.Session.Capture();
                    foreach(int p in new[]{local,remote})
                    {
                        snapshot.Lives[p].ShotgunOwned=true;snapshot.Lives[p].RocketOwned=true;snapshot.Lives[p].CutterOwned=true;
                        snapshot.Lives[p].SelectedWeapon=weapon;snapshot.Lives[p].PendingWeapon=default;snapshot.Lives[p].SwitchRemaining=0;
                        snapshot.Lives[p].Ammo=CombatLife.AmmoFor(snapshot.Lives[p],weapon);
                        snapshot.DamageRemaining[p]=phase=="boost"?5:0;
                    }
                    ground.Session.Restore(snapshot);yield return null;yield return null;
                    review.enabled=true;yield return Capture(weapon+"-"+phase+"-world");
                    review.enabled=false;yield return Capture(weapon+"-"+phase+"-own");
                }
            }
            File.WriteAllText(System.IO.Path.Combine(directory,"complete.json"),"{\"complete\":true,\"muted\":true,\"placementAndSnapshotFixture\":true,\"humanAcceptance\":false}");
            Debug.Log("WEAPON_COLOR_REVIEW_COMPLETE "+directory);Time.timeScale=1;Application.Quit();
        }
        void OnDestroy(){Time.timeScale=1;if(review)Destroy(review.gameObject);}
    }
}
#endif

#endif
