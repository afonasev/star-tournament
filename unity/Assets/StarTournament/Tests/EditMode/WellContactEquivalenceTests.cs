using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellContactEquivalenceTests
    {
        [Test] public void OptimizedVisibilityMatchesFrozenNativeRayReferenceExactly()
        {
            var random=new System.Random(72119);var boxes=new List<Bounds>();
            float R(float lo,float hi)=>(float)(lo+random.NextDouble()*(hi-lo));
            for(int i=0;i<80;i++)boxes.Add(new Bounds(new Vector3(R(-8,8),R(-3,6),R(-8,8)),new Vector3(R(.05f,5),R(.05f,5),R(.05f,5))));
            for(int i=0;i<3000;i++)
            {
                var normal=i%3==0?Vector3.up:i%3==1?Vector3.right:Vector3.forward;var tangent=i%3==0?Vector3.right:Vector3.up;var bitangent=Vector3.Cross(normal,tangent);
                var point=new Vector3(R(-10,10),R(-4,8),R(-10,10));
                float expected=LegacyWellContactReference.Visibility(point,normal,tangent,bitangent,boxes);
                float actual=WellContactMesh.Visibility(point,normal,tangent,bitangent,boxes);
                Assert.That(BitConverter.SingleToInt32Bits(actual),Is.EqualTo(BitConverter.SingleToInt32Bits(expected)),"point "+i);
            }
        }
    }
}
