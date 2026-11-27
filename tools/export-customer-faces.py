"""Run: blender -b --python tools/export-customer-faces.py"""

import bpy
from pathlib import Path


project = Path(__file__).resolve().parents[1]
output = project / "Assets/_Project/Resources/Faces"
output.mkdir(parents=True, exist_ok=True)
PREVIEW_ALL = False
exec((project / "tools/create-customer-faces.py").read_text(), globals())

bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.data.objects:
    if obj.type == "MESH":
        obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(output / "FaceParts.fbx"),
    use_selection=True,
    object_types={"MESH"},
    add_leaf_bones=False,
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    path_mode="STRIP",
)

# Keep the editable Blender source as five assembled, smoothly rounded heads.
for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    style = int(obj.name.split("_")[1])
    obj.location.x = (style - 2) * 0.43
for index in range(5):
    offset = (index - 2) * 0.43
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=20, radius=0.13, location=(offset, 0, 0))
    head = bpy.context.object
    head.name = f"Head_{index}"
    head.data.materials.append(skins[index])
    for polygon in head.data.polygons:
        polygon.use_smooth = True
bpy.ops.wm.save_as_mainfile(filepath=str(project / "tools/CustomerFaces.blend"))
