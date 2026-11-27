"""Run: blender -b --python tools/export-cowboy-hats.py"""

import bpy
from pathlib import Path


project = Path(__file__).resolve().parents[1]
OUTPUT_DIR = project / "Assets/_Project/Resources/Hats"
PREVIEW_ALL = False
exec((project / "tools/create-cowboy-hats.py").read_text(), globals())

bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.data.objects:
    if obj.type == "MESH":
        obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT_DIR / "CowboyHat.fbx"),
    use_selection=True,
    object_types={"MESH"},
    add_leaf_bones=False,
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    path_mode="STRIP",
)
bpy.ops.wm.save_as_mainfile(filepath=str(project / "tools/CowboyHats.blend"))
