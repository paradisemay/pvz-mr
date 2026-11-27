"""Run with Blender: blender -b tools/ITMO_Ozon_Cardboard.blend --python tools/export-itmo-box.py"""

import bpy
from mathutils import Matrix
from pathlib import Path


project = Path(__file__).resolve().parents[1]
output = project / "Assets/_Project/Resources/ITMO"
output.mkdir(parents=True, exist_ok=True)

for image in bpy.data.images:
    if image.name == "Cardboard surface artwork.001":
        image.filepath_raw = str(output / "Cardboard.png")
        image.file_format = "PNG"
        image.save()
    elif image.name == "ITMO label artwork.001":
        image.filepath_raw = str(output / "Label.png")
        image.file_format = "PNG"
        image.save()

root = bpy.data.objects["Clean_Closed_Root"]
origin = root.matrix_world.inverted()
scale = Matrix.Diagonal((0.34 / 2.7, 0.34 / 2.1, 0.26 / 2.0, 1))
centre = Matrix.Translation((0, 0, -1))
bpy.ops.object.select_all(action="DESELECT")

for source in bpy.data.objects:
    if not source.name.startswith("Clean_Closed_") or source.type != "MESH":
        continue
    part = source.copy()
    part.data = source.data.copy()
    bpy.context.collection.objects.link(part)
    part.parent = None
    part.data.transform(scale @ centre @ origin @ source.matrix_world)
    part.matrix_world = Matrix.Identity(4)
    part.name = source.name.removeprefix("Clean_Closed_")
    part.select_set(True)

bpy.ops.export_scene.fbx(
    filepath=str(output / "ITMO_Box.fbx"),
    use_selection=True,
    object_types={"MESH"},
    add_leaf_bones=False,
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    path_mode="STRIP",
)
