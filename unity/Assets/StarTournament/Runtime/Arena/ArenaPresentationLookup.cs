using System;
using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // Each builder reads the already constructed direct solid children thousands of times.
    internal sealed class ArenaPresentationLookup
    {
        readonly Transform root;
        readonly Dictionary<string,Transform> children=new Dictionary<string,Transform>(StringComparer.Ordinal);
        public ArenaPresentationLookup(Transform root)
        {
            this.root=root;
            for(int i=0;i<root.childCount;i++){var child=root.GetChild(i);if(!children.ContainsKey(child.name))children.Add(child.name,child);}
        }
        internal sealed class Geometry
        {
            public readonly Vector3[] Vertices,Normals;public readonly Vector2[] Uv;
            public Geometry(Mesh mesh){Vertices=mesh.vertices;Normals=mesh.normals;Uv=mesh.uv;}
        }
        readonly Dictionary<Mesh,Geometry> geometry=new Dictionary<Mesh,Geometry>();
        public Geometry Read(Mesh mesh)
        {
            if(!geometry.TryGetValue(mesh,out var data))geometry.Add(mesh,data=new Geometry(mesh));
            return data;
        }
        public Transform Find(string name)=>name.IndexOf('/')<0&&children.TryGetValue(name,out var child)?child:root.Find(name);
    }
}
