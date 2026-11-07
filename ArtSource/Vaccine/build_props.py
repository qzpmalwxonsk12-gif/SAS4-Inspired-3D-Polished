"""Original Vaccine prop models. Blender 5.2; metres, Unity Y-up coordinates below."""
import bpy, math, os, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT=os.path.join(ROOT,'Assets/Vaccine/Models')
os.makedirs(OUT,exist_ok=True)
def v(p): return Vector((p[0],-p[2],p[1]))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
M={}
for n,col in {'Helicopter':(.22,.25,.18,1),'Aircraft':(.65,.65,.59,1),'Steel':(.19,.22,.23,1),'Rubber':(.035,.04,.045,1),'Glass':(.055,.12,.15,1),'Teal':(.1,.25,.26,1),'PaintBlue':(.21,.32,.37,1),'PaintRed':(.31,.12,.09,1),'Cream':(.65,.63,.55,1),'Amber':(.7,.37,.08,1),'Concrete':(.42,.42,.39,1)}.items():
    m=bpy.data.materials.new(n);m.diffuse_color=col;M[n]=m
def finish(o,n,m,bevel=0):
    o.name=n;o.data.materials.append(M[m])
    if bevel:
        mod=o.modifiers.new('Small manufactured edge','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def box(n,p,s,m,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));o=bpy.context.object;o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,n,m,bevel)
def mesh(n,vs,fs,m):
    d=bpy.data.meshes.new(n);d.from_pydata([v(p) for p in vs],[],fs);d.update();o=bpy.data.objects.new(n,d);bpy.context.collection.objects.link(o);finish(o,n,m)
    bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
    return o
def rod(n,a,b,r,m,vertices=12,r2=None):
    mid=(v(a)+v(b))/2;delta=v(b)-v(a)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r if r2 is None else r2,depth=delta.length,location=mid)
    o=bpy.context.object;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();return finish(o,n,m)
def loft(n,rings,m,sides=12):
    vs=[];fs=[]
    for z,w,b,t in rings:
        for i in range(sides):
            a=2*math.pi*i/sides;vs.append((w*math.cos(a),(b+t)/2+(t-b)/2*math.sin(a),z))
    for k in range(len(rings)-1):
        for j in range(sides):
            a=k*sides+j;b=k*sides+(j+1)%sides;fs.append((a,b,b+sides,a+sides))
    fs.append(tuple(reversed(range(sides))));fs.append(tuple(range((len(rings)-1)*sides,len(rings)*sides)))
    return mesh(n,vs,fs,m)
def plate(n,pts,m):return mesh(n,pts,[tuple(range(len(pts)))],m)
def export(name):
    objs=list(bpy.context.scene.objects)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    # Project every face in physical metres, including bevels; authored model uses 2m textures.
    for old in list(o.data.uv_layers):o.data.uv_layers.remove(old)
    uv=o.data.uv_layers.new(name='MaterialMetres')
    for p in o.data.polygons:
        normal=p.normal;ax=max(range(3),key=lambda i:abs(normal[i]))
        for li in p.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=((co.y,co.z) if ax==0 else (co.x,co.z) if ax==1 else (co.x,co.y))
            uv.data[li].uv/=2
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),name+'.blend'))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False)
    print(name,'vertices',len(o.data.vertices),'polygons',len(o.data.polygons))
    bpy.ops.object.delete(use_global=False)

# Rescue utility helicopter: a shaped cabin, fully framed glazing and mechanical rotor head.
fuselage=loft('Armoured cabin',[(-3.8,.45,1.6,3),(-2.8,1.23,1.2,3.55),(-1.5,1.65,1.05,3.85),(1.25,1.65,1.05,3.85),(2.8,1.35,1.3,3.25),(3.7,.65,1.55,2.6),(4,.15,1.8,2.3)],'Helicopter',16)
fuselage.data.materials.append(M['Glass'])
# Glazing is part of the hull surface so it cannot disappear inside its curvature.
for p in fuselage.data.polygons:
    k,j=divmod(p.index,16)
    if k in [3,4] and j in [0,1,2,5,6,7]:p.material_index=1
    p.use_smooth=True
for j in [0,3,5,8]:
    a=j*math.pi/8
    rod('Cockpit structural frame',(1.67*math.cos(a),2.45+1.42*math.sin(a),1.25),(.67*math.cos(a),2.075+.55*math.sin(a),3.7),.045,'Helicopter',8)
for s in [-1,1]:
    front=[(s*.09,3.78,1.36),(s*1.18,3.43,1.62),(s*1.2,2.55,3.12),(s*.09,2.69,3.3)]
    plate('Sloped windshield',front,'Glass')
    for i in range(4):rod('Windshield seal',front[i],front[(i+1)%4],.04,'Rubber',8)
    pts=[(s*1.51,3.12,1.15),(s*1.62,2.48,1.2),(s*1.51,2.25,2.57),(s*1.3,2.85,2.53)]
    plate('Pilot side glazing',pts,'Glass')
    for i in range(4):rod('Pilot frame',pts[i],pts[(i+1)%4],.035,'Steel',8)
    box('Sliding cabin door',(s*1.64,2.25,-.65),(.09,1.75,2.5),'Helicopter',.03)
    box('Cabin side window',(s*1.695,2.73,-.65),(.025,.65,1.65),'Glass',.06)
    rod('Sliding door rail',(s*1.73,3.2,-2),(s*1.73,3.2,.75),.035,'Steel')
    box('Door handle',(s*1.75,1.95,.37),(.08,.06,.26),'Steel')
    box('Rescue stripe',(s*1.7,1.6,-.6),(.016,.18,2.5),'Cream',0)
    rod('Skid',(s*1.85,.2,-2.75),(s*1.85,.2,2.5),.105,'Steel')
    rod('Skid curved toe',(s*1.85,.2,2.5),(s*1.85,.46,3.1),.105,'Steel')
    for z in [-1.65,1.55]:rod('Skid brace',(s*1.85,.25,z),(s*1.05,1.35,z),.09,'Steel')
    rod('Turbine pod',(s*.72,3.7,-1.8),(s*.72,3.8,1),.48,'Helicopter',16)
    rod('Intake',(s*.72,3.8,1),(s*.72,3.8,1.03),.38,'Rubber',16)
    for k in range(6):box('Intake vane',(s*.72-.3+k*.12,3.8,1.04),(.035,.55,.04),'Steel',0)
    rod('Exhaust',(s*.72,3.7,-1.75),(s*.95,3.65,-2.3),.32,'Steel',16)
    rod('Exhaust hollow',(s*.95,3.65,-2.3),(s*.955,3.65,-2.32),.25,'Rubber',16)
loft('Tapered tail boom',[(-2.7,.65,2,3.3),(-5.7,.38,2.5,3.4),(-9.5,.19,2.95,3.4)],'Helicopter')
mesh('Swept tail fin',[(-.09,2.9,-9.7),(-.09,5.5,-10.2),(-.09,5.55,-9.3),(-.09,3.15,-8.2),(.09,2.9,-9.7),(.09,5.5,-10.2),(.09,5.55,-9.3),(.09,3.15,-8.2)],[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'Helicopter')
box('Tailplane',(0,3.05,-7.9),(3.4,.12,.7),'Helicopter')
rod('Tail rotor axle',(-.3,3.9,-9.6),(.55,3.9,-9.6),.12,'Steel')
for a in [0,math.pi/2,math.pi,3*math.pi/2]:
    rod('Tail rotor blade',(.55,3.9+math.cos(a)*.15,-9.6+math.sin(a)*.15),(.55,3.9+math.cos(a)*1.15,-9.6+math.sin(a)*1.15),.09,'Rubber',4)
rod('Main mast',(0,3.8,-.3),(0,5.2,-.3),.13,'Steel',16)
rod('Swashplate',(0,4.6,-.3),(0,4.72,-.3),.45,'Steel',16)
for a in [0,math.pi/2,math.pi,3*math.pi/2]:
    x,z=math.cos(a),math.sin(a)
    rod('Pitch link',(x*.34,4.6,z*.34-.3),(x*.65,5.15,z*.65-.3),.035,'Steel',8)
    pts=[(x*.5-z*.18,5.22,z*.5+x*.18-.3),(x*7-z*.16,5.1,z*7+x*.16-.3),(x*7+z*.24,5.1,z*7-x*.24-.3),(x*.5+z*.27,5.22,z*.5-x*.27-.3)]
    plate('Rotor blade',pts,'Rubber')
    rod('Rotor root',(0,5.2,-.3),(x*.8,5.2,z*.8-.3),.09,'Steel')
rod('Nose pitot',(.4,1.5,3.4),(.4,1.3,4.15),.025,'Steel',8)
rod('Radio aerial',(-.3,3.6,-2.1),(-.3,4.55,-2.55),.025,'Steel',6)
export('RescueHelicopter')

def car(van=False):
    paint='Cream' if van else 'PaintBlue'
    box('Body',(0,.73,0),(2.05,.85,4.7),paint,.18)
    box('Undercarriage',(0,.45,0),(1.65,.2,4.2),'Rubber')
    if van:
        box('Cargo cabin',(0,1.55,-.65),(1.96,1.5,3.25),paint,.15)
        pts=[(-.89,2.25,.9),(.89,2.25,.9),(.9,1.35,1.6),(-.9,1.35,1.6)]
        plate('Windscreen',pts,'Glass')
        for s in [-1,1]:box('Van side glazing',(s*1,1.8,.5),(.02,.64,1.1),'Glass',.03)
        box('Rear door split',(0,1.45,-2.29),(.025,1.65,.03),'Steel')
    else:
        mesh('Shaped cabin',[(-.96,1,1.35),(.96,1,1.35),(-.75,1.83,.5),(.75,1.83,.5),(-.75,1.83,-.95),(.75,1.83,-.95),(-.96,1,-1.65),(.96,1,-1.65)],[(0,1,3,2),(2,3,5,4),(4,5,7,6),(0,2,4,6),(1,7,5,3),(0,6,7,1)],paint)
        plate('Windscreen',[(-.85,1.12,1.24),(.85,1.12,1.24),(.69,1.78,.55),(-.69,1.78,.55)],'Glass')
        plate('Rear glass',[(-.7,1.78,-.98),(.7,1.78,-.98),(.86,1.13,-1.55),(-.86,1.13,-1.55)],'Glass')
        for s in [-1,1]:
            plate('Side front glass',[(s*.97,1.12,1.1),(s*.78,1.74,.45),(s*.78,1.74,-.13),(s*.97,1.12,-.13)],'Glass')
            plate('Side rear glass',[(s*.97,1.12,-.23),(s*.78,1.74,-.23),(s*.78,1.74,-.9),(s*.97,1.12,-1.45)],'Glass')
    for s in [-1,1]:
        for z in [-1.55,1.5]:
            rod('Tire',(s*.91,.45,z),(s*1.15,.45,z),.44,'Rubber',20)
            rod('Wheel rim',(s*1.151,.45,z),(s*1.17,.45,z),.27,'Steel',16)
            for a in range(5):
                an=a*math.pi*2/5;rod('Rim spoke',(s*1.18,.45,z),(s*1.18,.45+math.sin(an)*.22,z+math.cos(an)*.22),.025,'Cream',6)
        for z in [-.75,.65]:box('Door handle',(s*1.037,1.04,z),(.04,.045,.24),'Steel')
        box('Wing mirror',(s*1.16,1.4,1),(.25,.16,.24),paint)
        box('Headlamp',(s*.72,.91,2.35),(.44,.22,.05),'Cream',.025)
        box('Rear lamp',(s*.8,.88,-2.36),(.27,.25,.05),'PaintRed',.025)
    box('Front bumper',(0,.48,2.39),(2,.18,.12),'Steel')
    box('Rear bumper',(0,.48,-2.39),(2,.18,.12),'Steel')
    box('Grille',(0,.8,2.36),(.8,.24,.035),'Rubber')
    for x in [-.3,-.15,0,.15,.3]:box('Grille slat',(x,.8,2.39),(.03,.22,.02),'Steel',0)
    export('ServiceVan' if van else 'AbandonedSedan')
car();car(True)

# Moulded metro seating, origin at seat's floor centre. Faces local -X.
box('Seat shell',(0,.49,0),(.82,.11,.86),'Teal',.07)
box('Seat back',(.37,.92,0),(.13,.84,.86),'Teal',.07)
rod('Bench pedestal',(0,.08,0),(0,.43,0),.07,'Steel')
box('Floor foot',(0,.055,0),(.55,.07,.5),'Steel')
for z in [-.48,.48]:
    rod('Arm support',(.18,.45,z),(.18,.78,z),.025,'Steel',8)
    rod('Armrest',(-.3,.78,z),(.27,.78,z),.035,'Steel',8)
export('MetroSeat')

random.seed(19)
for idx in range(3):
    # Unit-sized fractured concrete pieces; Unity keeps the approved collision footprint.
    vs=[]
    for z in [-.5,.5]:
        for y in [-.5,.5]:
            for x in [-.5,.5]:vs.append((x+random.uniform(-.12,.12),y+random.uniform(-.12,.12),z+random.uniform(-.12,.12)))
    mesh('Fractured slab',vs,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],'Concrete')
    export('ConcreteDebris'+str(idx))
print('VACCINE PROPS COMPLETE')
