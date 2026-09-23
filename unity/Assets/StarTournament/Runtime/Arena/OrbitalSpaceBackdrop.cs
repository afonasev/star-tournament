using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class OrbitalLeaguePresentation
    {
        // Fixed raster resolution, noise frequencies and palette are the authored texture encoding.
        // Visible scale, position and brightness are controlled by the named presentation profile.
        const int SpaceTextureSize=1024;
        void SpaceBackdrop()
        {
            float distance=p.Get("ring.spaceHalfExtent");var center=Vector3.up*p.Get("ring.spaceCenterY");
            var normals=new[]{Vector3.right,Vector3.left,Vector3.back,Vector3.forward,Vector3.up,Vector3.down};
            var names=new[]{"Blue planet","Rock planet","Sun","North stars","Zenith stars","Nadir stars"};
            for(int face=0;face<normals.Length;face++)
            {
                var normal=normals[face];var up=Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up;
                var right=Vector3.Cross(up,normal);var mesh=new Mesh{name="Distant space cube / "+names[face]};owned.Add(mesh);
                var origin=center+normal*distance;
                mesh.vertices=new[]{origin+(-right-up)*distance,origin+(right-up)*distance,origin+(right+up)*distance,origin+(-right+up)*distance};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Distant space texture / "+names[face]};owned.Add(material);
                material.SetTexture("_BaseMap",SpaceTexture(face));material.SetFloat("_Cull",0);
                var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        Texture2D SpaceTexture(int face)
        {
            var texture=new Texture2D(SpaceTextureSize,SpaceTextureSize,TextureFormat.RGB24,true){name="Space cube face "+face,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};owned.Add(texture);
            var pixels=new Color[SpaceTextureSize*SpaceTextureSize];
            float radius=p.Get(face==0?"ring.planetRadius":face==1?"ring.secondPlanetRadius":"ring.sunRadius");
            float cx=p.Get("ring.celestialHorizontal"),cy=p.Get("ring.celestialElevation"),brightness=p.Get("ring.skyValue");
            for(int y=0;y<SpaceTextureSize;y++)for(int x=0;x<SpaceTextureSize;x++)
            {
                float u=(x+.5f)/SpaceTextureSize,v=(y+.5f)/SpaceTextureSize;
                uint hash=unchecked((uint)(x*73856093)^(uint)(y*19349663)^(uint)(face*83492791));hash^=hash>>13;
                float cloud=Mathf.PerlinNoise(u*5+face*11,v*7+face*3)*Mathf.PerlinNoise(u*13+31,v*11+17);
                float band=Mathf.Exp(-Mathf.Pow((v-.4f-u*.15f)/.14f,2));
                var color=Color.Lerp(new Color(.009f,.015f,.04f),new Color(.1f,.22f,.38f),cloud*band)*brightness;
                if(hash%2203<3)color+=new Color(.7f,.82f,1)*(.4f+(hash%100)/100f)*brightness*3;
                float px=(u-cx)/radius,py=(v-cy)/radius,r=Mathf.Sqrt(px*px+py*py);
                if(face<2)
                {
                    // Shaded sphere painted into the cube face, with a thin atmospheric rim.
                    if(r<1)
                    {
                        float nz=Mathf.Sqrt(1-r*r);float light=Mathf.Clamp01(Vector3.Dot(new Vector3(px,py,nz),new Vector3(-.65f,.4f,.65f).normalized));
                        float terrain=Mathf.PerlinNoise(px*3.8f+19,py*4.2f+31);
                        Color surface=face==0?Color.Lerp(new Color(.035f,.12f,.3f),new Color(.18f,.46f,.58f),terrain):Color.Lerp(new Color(.18f,.09f,.06f),new Color(.57f,.36f,.2f),terrain);
                        float wisps=Mathf.PerlinNoise(px*8+13,py*13+27);surface=Color.Lerp(surface,new Color(.72f,.81f,.87f),Mathf.Clamp01((wisps-.51f)*4)*(face==0?.8f:.12f));
                        color=surface*(.025f+light*.85f);
                        color+=new Color(.12f,.38f,.68f)*Mathf.Pow(1-nz,4)*light*(face==0?.7f:.15f);
                    }
                    else color+=new Color(.1f,.3f,.62f)*Mathf.Exp(-(r-1)*80)*(face==0?.4f:.1f);
                }
                else if(face==2)
                {
                    float glow=Mathf.Exp(-r*r*.7f);color+=new Color(1,.38f,.08f)*glow*.4f;
                    if(r<1)color=Color.Lerp(new Color(1,.82f,.39f),Color.white,Mathf.Sqrt(1-r*r));
                }
                pixels[y*SpaceTextureSize+x]=color;
            }
            texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
    }
}
