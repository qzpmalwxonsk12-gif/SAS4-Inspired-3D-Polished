"""Low-poly ship fittings. Run in Blender background; exports FBX, preserves .blend source.
Blender coordinates: X width, -Y front, Z up. Unity imports Y up / +Z front.
"""
import bpy, os, math
from mathutils import Vector
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Onslaught', 'Models')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
materials = {}
for name in ['Structural', 'PanelAtlas', 'Glass', 'Phosphor', 'Amber']:
    materials[name] = bpy.data.materials.new(name)
    materials[name].diffuse_color = {'Structural':(.15,.18,.2,1),'PanelAtlas':(.5,.52,.53,1),'Glass':(.015,.025,.03,1),'Phosphor':(.12,.6,.7,1),'Amber':(.85,.4,.08,1)}[name]

def cube(name, location, size, mat='Structural', bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    ob=bpy.context.object; ob.name=name; ob.dimensions=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(materials[mat])
    if bevel:
        mod=ob.modifiers.new('Machined edge', 'BEVEL'); mod.width=bevel; mod.segments=1
        bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=ob.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob

def export(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    ob=bpy.context.object; ob.name=name
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    ob.hide_set(True)
    return ob

for variant in range(4):
    parts=[]
    # Solid bevelled frame, with atlas on the broad front face only.
    parts.append(cube('Armored backing',(0,.025,1.72),(2.8,.18,3.44)))
    panel=cube('Inset armor',(0,-.09,1.78),(2.58,.10,3.12),'PanelAtlas',.025)
    uv=panel.data.uv_layers.active
    for poly in panel.data.polygons:
        for loop in poly.loop_indices:
            v=panel.data.vertices[panel.data.loops[loop].vertex_index].co
            u=(v.x/2.58+.5)*.49+.005
            w=(v.z/3.12+.5)*.49+.005
            uv.data[loop].uv=(u+(variant%2)*.5,w+(1-variant//2)*.5)
    parts.append(panel)
    parts.append(cube('Kick rail',(0,-.13,.15),(2.8,.18,.23),bevel=.035))
    parts.append(cube('Upper edge',(0,-.12,3.36),(2.8,.16,.12),bevel=.025))
    export('ArmorPanel'+str(variant),parts)

parts=[]
parts.append(cube('Pedestal',(0,.02,.5),(1.55,.62,1.0),bevel=.08))
parts.append(cube('Foot',(0,.02,.10),(1.75,.82,.2),bevel=.05))
deck=cube('Sloped control deck',(0,-.1,1.05),(1.7,.85,.16),bevel=.035)
deck.rotation_euler.x=math.radians(10);parts.append(deck)
parts.append(cube('Monitor surround',(0,-.04,1.45),(1.72,.26,.72),bevel=.07))
parts.append(cube('Screen',(0,-.19,1.45),(1.49,.025,.49),'Glass',.015))
for i in range(4):
    parts.append(cube('Readout',(-.17,-.207,1.6-i*.1),(.8-i*.11,.009,.018),'Phosphor',0))
for i in range(3):
    parts.append(cube('Switch',(-.48+i*.31,-.40,1.09),(.10,.12,.035),'Amber' if i==0 else 'Phosphor',.008))
export('BridgeConsole',parts)

parts=[cube('Beam',(0,0,0),(1,.18,.24),bevel=.035)]
export('BevelledBeam',parts)

for ob in bpy.data.objects: ob.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','OnslaughtShipKit.blend'))
print('SHIP_KIT_EXPORTED',len(bpy.data.objects),'meshes',sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'polygons')
