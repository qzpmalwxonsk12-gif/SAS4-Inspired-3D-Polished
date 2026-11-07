"""Revamped SAS4-inspired bio-horror enemy kit. Blender 5.2, FBX + editable sources.
All dimensions in metres. Front is -Y in Blender, +Z in Unity.
Uses continuous cylindrical/anatomical UV projection so normal maps and organic textures wrap seamlessly without facet fragmentation.
"""
import bpy, math, os, random, json
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', 'Enemies', 'Models')
SOURCE = os.path.dirname(os.path.abspath(__file__))
os.makedirs(OUT, exist_ok=True)
random.seed(17)
report = []

def init():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global mats, parts
    parts = []
    mats = {}
    colors = {
        'Skin': (.54, .58, .44, 1),
        'Muscle': (.52, .14, .12, 1),
        'Mutant': (.22, .48, .41, 1),
        'Cloth': (.13, .16, .20, 1),
        'Dark': (.02, .025, .022, 1),
        'Bone': (.72, .66, .45, 1),
        'Metal': (.28, .31, .33, 1),
        'Acid': (.48, .85, .12, 1)
    }
    for n, c in colors.items():
        m = bpy.data.materials.new(n)
        m.diffuse_color = c
        mats[n] = m

def ell(name, pos, scale, mat='Skin', seg=18, rings=12, rot=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=pos)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    if rot:
        o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=bool(rot), scale=True)
    o.data.materials.append(mats[mat])
    parts.append(o)
    for f in o.data.polygons:
        f.use_smooth = True
    return o

def link(name, a, b, r1, r2=None, mat='Skin', sides=14):
    a, b = Vector(a), Vector(b)
    v = b - a
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides,
        radius1=r1,
        radius2=r2 if r2 is not None else r1,
        depth=max(0.005, v.length),
        location=(a + b) / 2
    )
    o = bpy.context.object
    o.name = name
    o.rotation_euler = v.to_track_quat('Z', 'Y').to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.materials.append(mats[mat])
    parts.append(o)
    for f in o.data.polygons:
        f.use_smooth = True
    return o

def box(name, pos, dim, mat, bevel=.012, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object
    o.name = name
    o.dimensions = dim
    if rot:
        o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=bool(rot), scale=True)
    o.data.materials.append(mats[mat])
    parts.append(o)
    if bevel:
        mod = o.modifiers.new('Worn edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def maw_ring(size):
    verts = []
    faces = []
    for i in range(32):
        a = i * math.tau / 32
        for j in range(8):
            b = j * math.tau / 8
            thickness = .065 * (1 + .18 * math.sin(a * 4))
            verts.append((
                (.29 + math.cos(b) * thickness) * math.cos(a) * size,
                (-.34 + math.sin(b) * thickness) * size,
                (1.66 + (.25 + math.cos(b) * thickness) * math.sin(a)) * size
            ))
    for i in range(32):
        for j in range(8):
            faces.append((i * 8 + j, ((i + 1) % 32) * 8 + j, ((i + 1) % 32) * 8 + (j + 1) % 8, i * 8 + (j + 1) % 8))
    mesh = bpy.data.meshes.new('Continuous fleshy lip')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new('Maw lip', mesh)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mats['Muscle'])
    for f in o.data.polygons:
        f.use_smooth = True
    parts.append(o)
    return o

def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o

def uv(o, default_mat='Skin'):
    """Continuous anatomical tri-planar/cylindrical UV projection into the 4 atlas quadrants.
    Eliminates smart_project triangle fragmentation so normal maps and muscle/skin textures flow organically.
    """
    if not o.data.uv_layers:
        o.data.uv_layers.new(name='UVMap')
    uv_layer = o.data.uv_layers.active.data
    quadrants = {
        'Skin': (0.0, 0.5),
        'Bone': (0.0, 0.5),
        'Muscle': (0.5, 0.5),
        'Dark': (0.5, 0.5),
        'Mutant': (0.0, 0.0),
        'Acid': (0.0, 0.0),
        'Cloth': (0.5, 0.0),
        'Metal': (0.5, 0.0)
    }
    verts = o.data.vertices
    for poly in o.data.polygons:
        mat_name = o.data.materials[poly.material_index].name if poly.material_index < len(o.data.materials) else default_mat
        off = quadrants.get(mat_name, (0.0, 0.5))
        n = poly.normal
        use_xz = abs(n.y) >= abs(n.x) and abs(n.y) >= abs(n.z)
        use_yz = abs(n.x) > abs(n.y) and abs(n.x) >= abs(n.z)
        for li in poly.loop_indices:
            vi = o.data.loops[li].vertex_index
            co = verts[vi].co
            if use_xz:
                u_raw = co.x * 0.85 + 0.5
                v_raw = co.z * 0.65
            elif use_yz:
                u_raw = co.y * 0.85 + 0.5
                v_raw = co.z * 0.65
            else:
                u_raw = co.x * 0.85 + 0.5
                v_raw = co.y * 0.85 + 0.5
            # Ping-pong wrap in [0, 1] so adjacent faces never have a hard UV seam jump
            u_mod = abs((u_raw % 2.0) - 1.0)
            v_mod = abs((v_raw % 2.0) - 1.0)
            uv_layer[li].uv = (off[0] + 0.03 + u_mod * 0.44, off[1] + 0.03 + v_mod * 0.44)

def distance_segment(p, a, b):
    d = b - a
    t = max(0, min(1, (p - a).dot(d) / max(.0001, d.length_squared)))
    return (p - (a + d * t)).length

def rig_and_export(name, bones, rigid, clips, base_skin='Skin', voxel=.028):
    skin = [o for o in parts if o not in rigid]
    body = join(skin, 'Body')
    body.data.materials.clear()
    body.data.materials.append(mats[base_skin])
    rem = body.modifiers.new('Unified anatomy', 'REMESH')
    rem.mode = 'VOXEL'
    rem.voxel_size = voxel
    rem.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=rem.name)
    sm = body.modifiers.new('Skin relaxation', 'SMOOTH')
    sm.factor = .65
    sm.iterations = 3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    dec = body.modifiers.new('Low-end topology', 'DECIMATE')
    dec.ratio = .40
    bpy.ops.object.modifier_apply(modifier=dec.name)
    uv(body, base_skin)

    arm = bpy.data.armatures.new(name + 'Skeleton')
    rig = bpy.data.objects.new(name + 'Rig', arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    body.select_set(False)
    bpy.ops.object.mode_set(mode='EDIT')
    for bn, (a, b, parent) in bones.items():
        bone = arm.edit_bones.new(bn)
        bone.head = a
        bone.tail = b
        if parent:
            bone.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')

    for ob in [body] + list(rigid):
        if ob != body:
            uv(ob, base_skin)
        groups = {n: ob.vertex_groups.new(name=n) for n in bones}
        fixed = rigid.get(ob)
        for v in ob.data.vertices:
            if fixed and fixed != 'AUTO':
                groups[fixed].add([v.index], 1, 'REPLACE')
                continue
            p = ob.matrix_world @ v.co
            ds = sorted((distance_segment(p, Vector(a), Vector(b)), n) for n, (a, b, _) in bones.items() if n not in ('Root', 'Jaw'))[:2]
            weights = [1 / max(.025, d) ** 4 for d, n in ds]
            total = sum(weights)
            for w, (_, n) in zip(weights, ds):
                groups[n].add([v.index], w / total, 'REPLACE')

    mesh = join([body] + list(rigid), name + 'Mesh')
    mesh.parent = rig
    mod = mesh.modifiers.new('Skeleton', 'ARMATURE')
    mod.object = rig
    for f in mesh.data.polygons:
        f.use_smooth = True

    scene = bpy.context.scene
    scene.render.fps = 30
    for clip, seconds, loop in clips:
        rig.animation_data_clear()
        frames = max(2, round(seconds * 30))
        for pb in rig.pose.bones:
            pb.rotation_mode = 'XYZ'
        for frame in sorted(set(range(1, frames + 2, 3)) | {frames + 1}):
            f = min(frame, frames + 1)
            t = (f - 1) / frames
            phase = t * 2 * math.pi
            for pb in rig.pose.bones:
                pb.rotation_euler = (0, 0, 0)
                pb.location = (0, 0, 0)
                pb.scale = (1, 1, 1)
            def rot(b, x=0, y=0, z=0):
                if b in rig.pose.bones:
                    rig.pose.bones[b].rotation_euler = tuple(math.radians(a) for a in (x, y, z))
            def loc(b, x=0, y=0, z=0):
                if b in rig.pose.bones:
                    rig.pose.bones[b].location = (x, y, z)
            worm = name in ('Worm', 'PukeWorm')
            heavy = name in ('Bloater', 'Regurgitator')
            if worm:
                for i in range(4):
                    rot('Segment' + str(i), 0, 0, math.sin(phase - i * .8) * (14 if clip == 'Move' else 4))
                if clip == 'Melee':
                    rot('Segment0', -math.sin(math.pi * t) * 38)
                if clip == 'Death':
                    loc('Root', 0, -.12 * t, 0)
                    rig.pose.bones['Root'].scale = (1, 1 - .5 * t, 1)
            elif clip == 'Idle':
                rot('Spine', (9 if name == 'Shambler' else 14 if name == 'Sprinter' else 4) + math.sin(phase) * 2.5)
                rot('Head', math.cos(phase) * 2.5, math.sin(phase) * 3.5, math.sin(phase) * 2.0)
                rot('Jaw', 5 + math.sin(phase * 2) * 3.5)
                for side, sgn in [('L', -1), ('R', 1)]:
                    rot('UpperArm' + side, -14 + math.sin(phase + sgn) * 3, 0, sgn * 8)
                    rot('Forearm' + side, -22 + math.cos(phase + sgn) * 4)
                loc('Hips', 0, math.sin(phase) * .015, 0)
            elif clip == 'Move':
                amp = 18 if heavy else 42 if name == 'Sprinter' else 25
                for side, offset in [('L', 0), ('R', math.pi)]:
                    a = math.sin(phase + offset) * (0.65 if name == 'Shambler' and side == 'R' else 1)
                    rot('Thigh' + side, a * amp)
                    rot('Shin' + side, max(0, -a) * (50 if name == 'Sprinter' else 27))
                    rot('Foot' + side, -a * 10)
                    rot('UpperArm' + side, -18 - a * (15 if heavy else 32), 0, (-1 if side == 'L' else 1) * 8)
                    rot('Forearm' + side, -24 - max(0, a) * 20)
                rot('Spine', 8 if name == 'Shambler' else 15 if name == 'Sprinter' else 3, 0, math.sin(phase) * 3.5)
                loc('Hips', 0, abs(math.sin(phase)) * (.028 if heavy else .048), 0)
                rot('Head', -5, math.sin(phase) * 3.5)
                rot('Jaw', 8 + abs(math.sin(phase)) * 6)
            elif clip == 'Melee':
                swing = math.sin(math.pi * min(t / .68, 1))
                rot('UpperArmR', -105 * swing, 0, -14 * swing)
                rot('ForearmR', -38 * swing)
                rot('UpperArmL', -70 * swing if name == 'Regurgitator' else -28 * swing, 0, 14 * swing)
                rot('Spine', 20 * math.sin(math.pi * t), 0, -10 * math.sin(math.pi * t))
                rot('Head', -12 * swing)
                rot('Jaw', 24 * swing)
            elif clip in ('Spit', 'Vomit'):
                pulse = math.sin(math.pi * t)
                rot('Spine', -14 * pulse if t < .4 else 26 * pulse)
                rot('Head', -20 * pulse)
                rot('Jaw', 36 * pulse)
                for side in ['L', 'R']:
                    rot('UpperArm' + side, -28 * pulse, 0, (1 if side == 'R' else -1) * 14 * pulse)
            elif clip == 'Hit':
                rot('Spine', -14 * math.sin(math.pi * t))
                rot('Head', 10 * math.sin(math.pi * t))
            elif clip == 'Death':
                fall = min(1, max(0, (t - .1) / .7))
                rot('Root', -86 * fall, 0, 8 * fall)
                loc('Root', 0, .12 * fall, 0)
                rot('UpperArmL', -22 * fall, 0, -25 * fall)
                rot('UpperArmR', 12 * fall, 0, 30 * fall)
                rot('ShinL', 32 * fall)
            for pb in rig.pose.bones:
                pb.keyframe_insert('rotation_euler', frame=f, group=pb.name)
                pb.keyframe_insert('location', frame=f, group=pb.name)
                pb.keyframe_insert('scale', frame=f, group=pb.name)
        action = rig.animation_data.action
        action.name = name + '_' + clip
        action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions[name + '_Idle']
    rig.animation_data.action_slot = rig.animation_data.action.slots[0]
    scene.frame_set(1)
    for pb in rig.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    scene.frame_start = 1
    scene.frame_end = 61
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE, name + '.blend'))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + '.fbx'),
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        axis_forward='-Z',
        axis_up='Y',
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_simplify_factor=0,
        mesh_smooth_type='FACE'
    )
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    report.append({'name': name, 'triangles': tris, 'bones': len(bones), 'clips': [c[0] for c in clips]})
    print('ENEMY_EXPORT', name, tris, 'triangles', len(bones), 'bones')

def humanoid(name):
    init()
    rigid = {}
    heavy = name in ('Bloater', 'Regurgitator')
    boss = name == 'Regurgitator'
    spr = name == 'Sprinter'
    spit = name == 'Spitter'
    size = 1.72 if boss else 1.25 if heavy else 1.03 if spit else 1.0
    width = 1.72 if boss else 1.66 if heavy else .84 if spr else 1.0
    skin = 'Mutant' if boss or spit else 'Muscle' if spr else 'Skin'

    def p(x, y, z):
        return (x * size, y * size, z * size)

    def e(label, pos, scale, material=skin, bone=None, rot=None):
        o = ell(label, p(*pos), tuple(v * size for v in scale), material, rot=rot)
        if bone:
            rigid[o] = bone
        return o

    bones = {
        'Root': (p(0, 0, 0), p(0, 0, .2), None),
        'Hips': (p(0, 0, .88), p(0, 0, 1.1), 'Root'),
        'Spine': (p(0, 0, 1.1), p(0, 0, 1.34), 'Hips'),
        'Chest': (p(0, 0, 1.34), p(0, 0, 1.54), 'Spine'),
        'Head': (p(0, -.06, 1.54), p(0, -.11, 1.82), 'Chest'),
        'Jaw': (p(0, -.12, 1.63), p(0, -.22, 1.57), 'Head')
    }

    # Core anatomical torso volumes (fused cleanly via voxel remesh)
    e('Pelvis', (0, .015, .94), (.22 * width, .16, .20))
    e('Waist', (0, 0, 1.12), (.17 * width, .14, .22))
    e('Rib cage', (0, .01, 1.37), (.29 * width, .19 if not heavy else .29, .27))
    e('Back hunch', (0, .12, 1.41), (.28 * width, .18, .24))
    for sgn in (-1, 1):
        e('Pectoral', (sgn * .11 * width, -.12 if not heavy else -.18, 1.40), (.13 * width, .09, .11))
        e('Lat muscle', (sgn * .22 * width, .04, 1.30), (.12 * width, .12, .22))
        e('Trap muscle', (sgn * .14 * width, .03, 1.53), (.14 * width, .11, .10))

    # Exposed dorsal vertebral column & bone spines along the hunched back
    spine_count = 7 if boss or spr else 5
    for i in range(spine_count):
        t = i / max(1, spine_count - 1)
        z_pos = 1.08 + t * 0.46
        y_pos = 0.16 + math.sin(t * math.pi) * (0.10 if not heavy else 0.15)
        b_target = 'Chest' if z_pos > 1.33 else 'Spine'
        e('Vertebra node', (0, y_pos, z_pos), (.042 * width, .052, .038), 'Bone', b_target)
        spike_len = 0.16 if (boss or spr) else 0.09
        o = link('Dorsal spine', p(0, y_pos, z_pos), p(0, y_pos + spike_len, z_pos + 0.06), .028 * size, .005 * size, 'Bone', 6)
        rigid[o] = b_target

    # Curved 3D skeletal ribs arching across the outer front ribcage
    if not heavy:
        for r_idx in range(4):
            rz = 1.21 + r_idx * 0.062
            ry = -0.205 if r_idx >= 1 else -0.185
            for sgn in (-1, 1):
                o = link('Exposed rib', p(sgn * .035, ry, rz), p(sgn * .21 * width, ry + 0.065, rz - 0.02), .017 * size, .010 * size, 'Bone', 7)
                rigid[o] = 'Chest' if r_idx >= 2 else 'Spine'
                o_muscle = link('Intercostal muscle', p(sgn * .04, ry + 0.012, rz - 0.025), p(sgn * .19 * width, ry + 0.072, rz - 0.04), .015 * size, .011 * size, 'Muscle', 6)
                rigid[o_muscle] = 'Chest' if r_idx >= 2 else 'Spine'

    if heavy:
        e('Distended belly', (0, -.12, 1.11), (.38 * width, .39, .43))
        # Jagged burst ribs & recessed abdominal slash wounds instead of a flat sphere
        for r_idx in range(4):
            rz = 1.16 + r_idx * 0.085
            for sgn in (-1, 1):
                o = link('Burst rib', p(sgn * .28 * width, -.28, rz), p(sgn * .12 * width, -.47, rz + 0.02), .028 * size, .007 * size, 'Bone', 7)
                rigid[o] = 'Chest' if r_idx >= 2 else 'Spine'
                o_wound = link('Abdominal wound', p(sgn * .24 * width, -.32, rz - 0.02), p(sgn * .04 * width, -.48, rz - 0.01), .032 * size, .018 * size, 'Muscle', 7)
                rigid[o_wound] = 'Chest' if r_idx >= 2 else 'Spine'

    if spit:
        e('Acid throat', (0, -.16, 1.52), (.18, .17, .23), 'Mutant')
        e('Back acid gland', (0, .20, 1.44), (.27, .22, .26), 'Mutant')
        for sx, sy, sz, sr, b_name in [
            (.06, -.21, 1.54, (.095, .085, .115), 'Chest'),
            (-.07, -.19, 1.46, (.080, .072, .095), 'Chest'),
            (-.23, .14, 1.53, (.13, .11, .15), 'Chest'),
            (.21, .15, 1.48, (.12, .10, .13), 'Chest'),
            (0, .28, 1.36, (.15, .12, .17), 'Spine'),
            (-.14, .22, 1.24, (.10, .085, .11), 'Spine'),
        ]:
            e('Acid sac', (sx, sy, sz), sr, 'Acid', b_name)
        for sgn in (-1, 1):
            o = link('Acid carotid vein', p(sgn * .09, -.16, 1.38), p(sgn * .06, -.17, 1.66), .016 * size, .011 * size, 'Acid', 6)
            rigid[o] = 'Chest'

    if not boss:
        e('Neck', (0, -.045, 1.59), (.105, .10, .12))
        e('Skull', (0, -.09, 1.76), (.148, .138, .192))
        e('Cheekbone L', (.095, -.165, 1.735), (.045, .042, .036), 'Bone', 'Head')
        e('Cheekbone R', (-.095, -.165, 1.735), (.045, .042, .036), 'Bone', 'Head')
        e('Jaw', (0, -.16, 1.635), (.112, .105, .082), 'Bone' if spr else skin, 'Jaw')
        for x in [-.064, .064]:
            e('Sunken socket', (x, -.202, 1.79), (.038, .018, .028), 'Dark', 'Head')
            e('Infected eye glow', (x, -.217, 1.79), (.014, .009, .014), 'Acid', 'Head')
            brow = e('Brow ridge', (x, -.194, 1.822), (.058, .030, .024), 'Bone' if spr else skin, 'Head')
            brow.rotation_euler.y = (.22 if x > 0 else -.22)
        if spit:
            for x in [-.095, .095]:
                e('Secondary eye', (x, -.185, 1.83), (.012, .009, .012), 'Acid', 'Head')
            o = link('Acid drool strand', p(0, -.245, 1.62), p(0, -.255, 1.48), .015 * size, .005 * size, 'Acid', 6)
            rigid[o] = 'Jaw'
        if spr:
            for sgn in (-1, 1):
                o = link('Cranial horn', p(sgn * .09, -.08, 1.84), p(sgn * .15, .08, 1.96), .028 * size, .005 * size, 'Bone', 6)
                rigid[o] = 'Head'
        e('Nasal cavity', (0, -.222, 1.735), (.024, .016, .036), 'Dark', 'Head')
        e('Open mouth', (0, -.240, 1.665), (.068, .022, .038), 'Dark', 'Jaw')
        for i, x in enumerate([-.048, -.024, 0.0, .024, .048]):
            fang_len = .026 if i in (0, 4) else .018
            u_tooth = link('Upper fang', p(x, -.245, 1.715), p(x, -.252, 1.715 - fang_len), .009 * size, .002 * size, 'Bone', 5)
            rigid[u_tooth] = 'Head'
            l_tooth = link('Lower fang', p(x * .92, -.252, 1.645), p(x * .92, -.248, 1.645 + fang_len), .009 * size, .002 * size, 'Bone', 5)
            rigid[l_tooth] = 'Jaw'
    else:
        # Regurgitator apex boss
        e('Maw back', (0, -.12, 1.66), (.38, .26, .30), skin)
        e('Maw cavity', (0, -.34, 1.66), (.29, .06, .25), 'Dark', 'Head')
        rigid[maw_ring(size)] = 'Head'
        for i in range(20):
            a = i * math.tau / 20
            x = math.cos(a) * .29
            z = 1.66 + math.sin(a) * .25
            o = link('Outer maw fang', p(x * .94, -.405, 1.66 + (z - 1.66) * .94), p(x * .58, -.425, 1.66 + (z - 1.66) * .58), .028 * size, .003 * size, 'Bone', 6)
            rigid[o] = 'Head'
        for i in range(12):
            a = (i + .5) * math.tau / 12
            x = math.cos(a) * .21
            z = 1.66 + math.sin(a) * .18
            o = link('Inner maw fang', p(x * .88, -.365, 1.66 + (z - 1.66) * .88), p(x * .48, -.395, 1.66 + (z - 1.66) * .48), .020 * size, .003 * size, 'Bone', 5)
            rigid[o] = 'Head'
        e('Maw tongue base', (0, -.38, 1.55), (.15, .035, .052), 'Muscle', 'Head')
        o = link('Barbed tongue tip', p(0, -.39, 1.55), p(0, -.52, 1.46), .042 * size, .010 * size, 'Acid', 7)
        rigid[o] = 'Head'
        for sgn in (-1, 1):
            for ex, ez, er in [(.25, 1.84, .026), (.31, 1.77, .020), (.21, 1.90, .018)]:
                e('Boss eye', (sgn * ex, -.28, ez), (er, er * .7, er), 'Acid', 'Head')
            o = link('Boss crown horn', p(sgn * .26, -.05, 1.82), p(sgn * .44, -.16, 2.14), .062 * size, .009 * size, 'Bone', 7)
            rigid[o] = 'Head'
            o2 = link('Boss dorsal horn', p(sgn * .34, .16, 1.66), p(sgn * .52, .32, 1.96), .054 * size, .008 * size, 'Bone', 7)
            rigid[o2] = 'Chest'

    for side, sgn in [('L', 1), ('R', -1)]:
        shoulder = p(sgn * .29 * width, 0, 1.45)
        elbow = p(sgn * (.42 * width + .08), -.04, 1.08)
        wrist = p(sgn * (.46 * width + .09), -.16, .76)
        hand = p(sgn * (.47 * width + .09), -.20, .63)
        hip = p(sgn * .15 * width, .005, .94)
        knee = p(sgn * .18 * width, -.04, .51)
        ankle = p(sgn * .19 * width, .015, .12)
        toe = p(sgn * .19 * width, -.19, .08)
        bones.update({
            'UpperArm' + side: (shoulder, elbow, 'Chest'),
            'Forearm' + side: (elbow, wrist, 'UpperArm' + side),
            'Hand' + side: (wrist, hand, 'Forearm' + side),
            'Thigh' + side: (hip, knee, 'Hips'),
            'Shin' + side: (knee, ankle, 'Thigh' + side),
            'Foot' + side: (ankle, toe, 'Shin' + side)
        })
        for label, a, b, ra, rb in [
            ('Arm', shoulder, elbow, .18 if heavy else .108, .14 if heavy else .082),
            ('Forearm', elbow, wrist, .155 if heavy else .084, .125 if heavy else .062),
            ('Thigh', hip, knee, .185 if heavy else .135, .125 if heavy else .088),
            ('Shin', knee, ankle, .125 if heavy else .088, .072)
        ]:
            link(label, a, b, ra * size, rb * size, skin)
            ell(label + ' joint', a, (ra * size, ra * size, ra * size), skin)

        if spr:
            blade_a = Vector(elbow) + Vector((sgn * .05 * size, .03 * size, 0))
            blade_b = Vector(wrist) + Vector((sgn * .11 * size, .09 * size, .18 * size))
            o_blade = link('Forearm bone scythe', blade_a, blade_b, .034 * size, .005 * size, 'Bone', 6)
            rigid[o_blade] = 'Forearm' + side

        ell('Hand', (Vector(wrist) + Vector(hand)) / 2, (.12 * size if heavy else .075 * size, .078 * size, .12 * size), skin)
        for i in range(4):
            spread = (i - 1.5) * .036 * size
            a = Vector(hand) + Vector((spread, -.01 * size, 0))
            b = a + Vector((spread * .25, -.055 * size, -.085 * size))
            c = b + Vector((0, -.045 * size, -.075 * size * (1.35 if spr or boss else 1.0)))
            f_base = link('Finger', a, b, .022 * size, .015 * size, skin, 6)
            rigid[f_base] = 'Hand' + side
            f_claw = link('Claw talon', b, c, .016 * size, .003 * size, 'Bone', 5)
            rigid[f_claw] = 'Hand' + side

        ell('Foot', (Vector(ankle) + Vector(toe)) / 2, (.105 * size, .19 * size, .09 * size), skin)
        for ti in range(3):
            ta = Vector(toe) + Vector(((ti - 1) * .04 * size, -.03 * size, -.01 * size))
            tb = ta + Vector((0, -.06 * size, -.015 * size))
            t_claw = link('Toe claw', ta, tb, .016 * size, .004 * size, 'Bone', 5)
            rigid[t_claw] = 'Foot' + side

        if not spr:
            o = link('Torn trousers', hip, Vector(knee) + Vector((0, 0, .05 * size)), (.195 if heavy else .148) * size, .125 * size, 'Cloth')
            rigid[o] = 'Thigh' + side
        if name == 'Shambler':
            o = link('Torn sleeve', shoulder, Vector(shoulder).lerp(Vector(elbow), .55), .128 * size, .112 * size, 'Cloth')
            rigid[o] = 'UpperArm' + side
            if side == 'L':
                o_spike = link('Shoulder bone shard', shoulder, Vector(shoulder) + Vector((.11 * size, -.04 * size, .15 * size)), .025 * size, .004 * size, 'Bone', 6)
                rigid[o_spike] = 'UpperArmL'
        if heavy:
            e('Shoulder bulk', (sgn * .33 * width, .02, 1.47), (.26, .25, .25), skin)
            o_sh_spike = link('Shoulder spike', p(sgn * .36 * width, .04, 1.56), p(sgn * .48 * width, .08, 1.78), .045 * size, .007 * size, 'Bone', 6)
            rigid[o_sh_spike] = 'Chest'

        if name == 'Bloater':
            if side == 'L':
                pauldron = box('Riot pauldron', Vector(shoulder) + Vector((.08 * size, 0, .06 * size)), (.28 * size, .26 * size, .18 * size), 'Metal', .015, rot=(0, .35, 0))
                rigid[pauldron] = 'UpperArmL'
                gauntlet = box('Spiked gauntlet', (Vector(elbow) + Vector(wrist)) / 2, (.18 * size, .18 * size, .24 * size), 'Metal', .012)
                rigid[gauntlet] = 'ForearmL'
            if side == 'R':
                center = Vector(hand) + Vector((0, 0, -.22 * size))
                o = link('Cleaver grip', hand, center, .038 * size, mat='Dark', sides=8)
                rigid[o] = 'HandR'
                blade = box('Cleaver blade', center + Vector((-.13 * size, 0, -.14 * size)), (.36 * size, .042 * size, .28 * size), 'Metal', .008)
                rigid[blade] = 'HandR'
                spine_bar = box('Cleaver spine', center + Vector((.03 * size, 0, -.14 * size)), (.06 * size, .065 * size, .30 * size), 'Dark', .006)
                rigid[spine_bar] = 'HandR'
                blood_edge = box('Cleaver blood edge', center + Vector((-.29 * size, 0, -.14 * size)), (.05 * size, .046 * size, .27 * size), 'Muscle', .005)
                rigid[blood_edge] = 'HandR'

        if boss:
            shackle = box('Containment shackle', Vector(wrist) + Vector((0, 0, .06 * size)), (.22 * size, .22 * size, .11 * size), 'Metal', .015)
            rigid[shackle] = 'Forearm' + side

    if name == 'Shambler':
        e('Tattered shirt', (0, .02, 1.33), (.295, .207, .26), 'Cloth', 'Chest')
        e('Shirt waist', (0, .025, 1.12), (.22, .178, .20), 'Cloth', 'Spine')
        e('Torn shirt tail', (-.08, .005, .99), (.15, .18, .14), 'Cloth', 'Hips')
        e('Neck bio pustule', (.07, -.13, 1.56), (.038, .032, .038), 'Acid', 'Chest')
    if not spr:
        e('Trouser waist', (0, .035, .91), (.26 * width, .19, .16), 'Cloth', 'Hips')
    if heavy:
        for i in range(5):
            nx = -.24 + i * .12
            nz = 1.04 + math.sin(i * 2.1) * .16
            e('Infected nodule', (nx, -.43, nz), (.062, .048, .072), 'Muscle', 'Spine')
            e('Glowing cyst core', (nx, -.465, nz), (.036, .028, .040), 'Acid', 'Spine')
        for i in range(4):
            e('Back brood sac', ((-.22 + i * .15), .25, 1.28 + (i % 2) * .14), (.10, .085, .11), 'Acid', 'Chest')

    clips = [
        ('Idle', 2, True),
        ('Move', 1.6 if heavy else .7 if spr else 1.2, True),
        ('Melee', 1.6 if heavy else .9, False),
        ('Hit', .4, False),
        ('Death', 1.5, False)
    ]
    if spit:
        clips.append(('Spit', 1.3, False))
    if boss:
        clips.append(('Vomit', 2.4, False))
    rig_and_export(name, bones, rigid, clips, skin, .045 if boss else .034 if heavy else .023)

def worm(name):
    init()
    size = 1.3 if name == 'PukeWorm' else 1.0
    skin = 'Mutant' if name == 'PukeWorm' else 'Skin'
    rigid = {}
    bones = {'Root': ((0, 0, 0), (0, 0, .1), None)}
    for i in range(4):
        y = -.34 + i * .22
        bone = 'Segment' + str(i)
        bones[bone] = ((0, y, .18 * size), (0, y + .22, .18 * size), 'Root' if i == 0 else 'Segment' + str(i - 1))
        ell('Larval segment', (0, y + .08, .18 * size), ((.185 - i * .025) * size, .21 * size, .155 * size), skin)
        ridge = ell('Dorsal plate', (0, y + .08, (.30 - i * .015) * size), ((.12 - i * .015) * size, .14 * size, .045 * size), 'Muscle' if name == 'Worm' else 'Acid')
        rigid[ridge] = bone
        for side in [-1, 1]:
            o = link('Larval bristle', (side * .13 * size, y + .04, .16 * size), (side * .25 * size, y - .03, .04), .020 * size, .004 * size, 'Bone', 5)
            rigid[o] = bone
            o2 = link('Dorsal spine', (side * .07 * size, y + .09, .28 * size), (side * .12 * size, y + .15, .38 * size), .015 * size, .003 * size, 'Bone', 5)
            rigid[o2] = bone
    o = ell('Larval maw', (0, -.49, .19 * size), (.115 * size, .025, .095 * size), 'Dark')
    rigid[o] = 'Segment0'
    for m_idx in range(6):
        ang = m_idx * math.tau / 6
        mx = math.cos(ang) * .095 * size
        mz = (.19 + math.sin(ang) * .075) * size
        hook = link('Mandible hook', (mx, -.47, mz), (mx * .45, -.58 * size, (.19 + math.sin(ang) * .03) * size), .018 * size, .003 * size, 'Bone', 5)
        rigid[hook] = 'Segment0'
    rig_and_export(name, bones, rigid, [('Idle', 1.3, True), ('Move', .55, True), ('Melee', .65, False), ('Hit', .3, False), ('Death', .8, False)], skin, .019)

for name in ['Shambler', 'Sprinter', 'Spitter', 'Bloater', 'Regurgitator']:
    humanoid(name)
for name in ['Worm', 'PukeWorm']:
    worm(name)
with open(os.path.join(SOURCE, 'model-report.json'), 'w') as f:
    json.dump(report, f, indent=2)
print('ENEMY_KIT_COMPLETE', json.dumps(report))
