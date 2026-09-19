"""Existing v2 anatomical finger rotation convention for offline weapon adaptation."""
import math
from mathutils import Vector,Quaternion
from anatomy import SEGMENTS

def set_digits(rig,side,angles,opposition=30):
    sign=1 if side=='Left' else -1
    for digit,values in angles.items():
        for segment,angle in zip(SEGMENTS,values):
            name=side+digit+segment;bone=rig.data.bones[name]
            axis=bone.matrix_local.to_3x3().inverted()@Vector((0,sign,0))
            q=Quaternion(axis,math.radians(angle))
            if digit=='Thumb' and segment=='Proximal':
                ax=bone.matrix_local.to_3x3().inverted()@Vector((1,0,sign)).normalized()
                q=Quaternion(ax,math.radians(opposition))@q
            rig.pose.bones[name].rotation_quaternion=q
