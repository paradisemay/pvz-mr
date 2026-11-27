"""Editable low-poly cowboy hats. OUTPUT_DIR and PREVIEW_ALL may be set by a caller."""

import bpy
import math
from pathlib import Path


COLORS = (
    (0.72, 0.48, 0.27),  # tan
    (0.32, 0.18, 0.12),  # dark leather
    (0.62, 0.29, 0.19),  # rust
    (0.22, 0.29, 0.38),  # slate blue
    (0.38, 0.42, 0.27),  # olive
)
SIDES = 16
output_dir = globals().get("OUTPUT_DIR")
preview_all = globals().get("PREVIEW_ALL", True)
if output_dir:
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)


def leather_texture(index, color):
    image = bpy.data.images.new(f"Hat_{index}", width=256, height=256)
    pixels = []
    for y in range(256):
        for x in range(256):
            grain = math.sin(x * 0.59) * math.sin(y * 0.61) * 0.045
            grain += math.sin(x * 0.17 + y * 0.23) * 0.025
            grain += (((x * 73 + y * 151 + x * y * 17) % 97) / 97 - 0.5) * 0.035
            shade = 0.92 + grain
            pixels.extend((color[0] * shade, color[1] * shade, color[2] * shade, 1))
    image.pixels[:] = pixels
    image.pack()
    if output_dir:
        image.filepath_raw = str(output_dir / f"Hat_{index}.png")
        image.file_format = "PNG"
        image.save()
    material = bpy.data.materials.new(f"Leather_{index}")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    nodes.get("Principled BSDF").inputs["Roughness"].default_value = 0.82
    return material


def solid(name, color, metallic=0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = 0.55
    return material


band_material = solid("Dark stitched band", (0.095, 0.055, 0.035))
buckle_material = solid("Brass buckle", (0.72, 0.50, 0.18), 0.65)


def ring(rx, ry, z_function):
    return [
        (rx * math.cos(i * math.tau / SIDES), ry * math.sin(i * math.tau / SIDES), z_function(i * math.tau / SIDES))
        for i in range(SIDES)
    ]


def mesh_part(name, rings, material, offset_x=0, underside=False, cap=False, cylindrical=False):
    vertices = [point for group in rings for point in group]
    faces = []
    for j in range(len(rings) - 1):
        for i in range(SIDES):
            a, b = j * SIDES + i, j * SIDES + (i + 1) % SIDES
            c, d = (j + 1) * SIDES + (i + 1) % SIDES, (j + 1) * SIDES + i
            faces.append((a, b, c, d) if not underside else (d, c, b, a))
    if cap:
        centre = len(vertices)
        vertices.append((0, 0, 0.166))
        for i in range(SIDES):
            faces.append((centre, (len(rings) - 1) * SIDES + i, (len(rings) - 1) * SIDES + (i + 1) % SIDES))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new()
    for polygon in mesh.polygons:
        angles = []
        if cylindrical:
            for loop_index in polygon.loop_indices:
                point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
                angles.append((math.atan2(point.y, point.x) / math.tau) % 1)
        for loop_index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if cylindrical:
                angle = (math.atan2(point.y, point.x) / math.tau) % 1
                if max(angles) - min(angles) > 0.5 and angle < 0.5:
                    angle += 1
                uv.data[loop_index].uv = (angle * 3, point.z * 7)
            else:
                uv.data[loop_index].uv = (point.x * 3 + 0.5, point.y * 3 + 0.5)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location.x = offset_x
    obj.data.materials.append(material)
    return obj


def make_hat(index, leather):
    x = (index - 2) * 0.65 if preview_all else 0
    side_lift = lambda angle: abs(math.cos(angle)) ** 4
    front_dip = lambda angle: abs(math.sin(angle)) ** 4
    inner = ring(0.127, 0.116, lambda a: 0.014)
    middle = ring(0.186, 0.168, lambda a: 0.015 + 0.013 * side_lift(a))
    outer = ring(0.265, 0.221, lambda a: 0.014 + 0.075 * side_lift(a) - 0.022 * front_dip(a))
    mesh_part(f"Hat{index}_Brim", [inner, middle, outer], leather, x, underside=True)
    mesh_part(f"Hat{index}_BrimUnderside", [[(px, py, pz - 0.006) for px, py, pz in group]
                                                  for group in (inner, middle, outer)], leather, x)
    mesh_part(f"Hat{index}_BrimEdge", [outer, [(px, py, pz - 0.006) for px, py, pz in outer]],
              leather, x, underside=True)
    crown_base = ring(0.126, 0.115, lambda a: 0.018)
    crown_mid = ring(0.121, 0.109, lambda a: 0.125)
    crown_top = ring(0.100, 0.087, lambda a: 0.214 + 0.016 * side_lift(a) - 0.004 * front_dip(a))
    mesh_part(f"Hat{index}_Crown", [crown_base, crown_mid, crown_top], leather, x, cap=True, cylindrical=True)
    band_low = ring(0.129, 0.118, lambda a: 0.033)
    band_high = ring(0.128, 0.117, lambda a: 0.069)
    mesh_part(f"Hat{index}_Band", [band_low, band_high], band_material, x, cylindrical=True)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, -0.121, 0.051))
    buckle = bpy.context.object
    buckle.name = f"Hat{index}_Buckle"
    buckle.scale = (0.057, 0.006, 0.032)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    buckle.data.materials.append(buckle_material)


for number, color in enumerate(COLORS):
    material = leather_texture(number, color)
    if preview_all or number == 0:
        make_hat(number, material)
