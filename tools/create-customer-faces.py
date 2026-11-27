"""Five variants each of eyes, noses, lips and ears for low-poly visitors."""

import bpy
import math


PREVIEW_ALL = globals().get("PREVIEW_ALL", True)
SKIN = (
    (0.94, 0.75, 0.61),
    (0.75, 0.50, 0.34),
    (0.56, 0.34, 0.23),
    (0.34, 0.19, 0.13),
    (0.83, 0.64, 0.46),
)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*color, 1)
    mat.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = 0.85
    return mat


skins = [material(f"Skin_{i}", color) for i, color in enumerate(SKIN)]
lips = [material(f"Lip_{i}", tuple(c * k for c, k in zip(color, (0.77, 0.58, 0.59))))
        for i, color in enumerate(SKIN)]
white = material("Eye white", (0.94, 0.92, 0.86))
iris = material("Dark iris", (0.08, 0.055, 0.045))


def make_mesh(name, vertices, faces, mat, x_offset):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location.x = x_offset
    obj.data.materials.append(mat)
    return obj


def make_eyes(index, x_offset):
    # Width, height, spacing, slant, pupil shift: five recognizably different pairs.
    styles = (
        (0.029, 0.021, 0.052, 0.00, 0.000),
        (0.036, 0.015, 0.054, 0.11, 0.002),
        (0.025, 0.026, 0.048, -0.07, -0.002),
        (0.034, 0.012, 0.054, -0.10, 0.003),
        (0.030, 0.023, 0.059, 0.08, -0.003),
    )
    width, height, spacing, slant, pupil_shift = styles[index]
    whites, white_faces, pupils, pupil_faces = [], [], [], []
    sides = 10
    for side in (-1, 1):
        cx = side * spacing
        start = len(whites)
        whites.append((cx, -0.130, 0.037))
        for n in range(sides):
            a = n * math.tau / sides
            dx = width * math.cos(a)
            whites.append((cx + dx, -0.108, 0.037 + height * math.sin(a) + side * slant * dx))
        for n in range(sides):
            white_faces.append((start, start + 1 + n, start + 1 + (n + 1) % sides))
        p_start = len(pupils)
        pupil_x = cx + side * pupil_shift
        pupils.append((pupil_x, -0.136, 0.037))
        for n in range(sides):
            a = n * math.tau / sides
            pupils.append((pupil_x + width * 0.34 * math.cos(a), -0.132,
                           0.037 + height * 0.40 * math.sin(a)))
        for n in range(sides):
            pupil_faces.append((p_start, p_start + 1 + n, p_start + 1 + (n + 1) % sides))
    make_mesh(f"Eyes_{index}_White", whites, white_faces, white, x_offset)
    make_mesh(f"Eyes_{index}_Iris", pupils, pupil_faces, iris, x_offset)


def make_nose(index, x_offset):
    # Bridge width, tip width, length, projection, raised tip.
    styles = (
        (0.014, 0.020, 0.050, 0.035, 0.000),
        (0.012, 0.030, 0.039, 0.045, 0.002),
        (0.021, 0.035, 0.052, 0.034, -0.002),
        (0.012, 0.023, 0.076, 0.058, -0.004),
        (0.016, 0.026, 0.046, 0.050, 0.009),
    )
    bridge, tip, length, projection, lift = styles[index]
    sections = ((0.048, bridge * 0.7, 0.122),
                (0.048 - length * 0.65, bridge, 0.129 + projection * 0.25),
                (0.048 - length + lift, tip, 0.124 + projection))
    vertices = []
    for z, width, front in sections:
        vertices.extend(((-width, -0.110, z), (-width, -front, z),
                         (width, -front, z), (width, -0.110, z)))
    faces = [(3, 2, 1, 0), (8, 9, 10, 11)]
    for level in range(2):
        a, b = level * 4, (level + 1) * 4
        for side in range(4):
            nxt = (side + 1) % 4
            faces.append((a + side, a + nxt, b + nxt, b + side))
    make_mesh(f"Nose_{index}", vertices, faces, skins[index], x_offset)


def make_mouth(index, x_offset):
    paths = (
        [(-0.035, -0.051), (0, -0.054), (0.035, -0.051)],
        [(-0.047, -0.045), (-0.023, -0.060), (0, -0.064), (0.023, -0.060), (0.047, -0.045)],
        [(0.026 * math.cos(a), -0.049 + 0.018 * math.sin(a)) for a in (n * math.tau / 10 for n in range(10))],
        [(-0.040, -0.048), (-0.020, -0.055), (0, -0.058), (0.020, -0.055), (0.040, -0.047)],
        [(-0.038, -0.061), (-0.019, -0.051), (0, -0.047), (0.019, -0.051), (0.038, -0.061)],
    )
    path = paths[index]
    closed = index == 2
    radius = 0.006 if index == 2 else 0.0075
    vertices, faces = [], []
    for n, (x, z) in enumerate(path):
        prev = path[(n - 1) % len(path)] if closed or n else path[0]
        next_point = path[(n + 1) % len(path)] if closed or n < len(path) - 1 else path[-1]
        dx, dz = next_point[0] - prev[0], next_point[1] - prev[1]
        length = math.hypot(dx, dz)
        nx, nz = (-dz / length, dx / length) if length else (0, 1)
        for k in range(6):
            a = k * math.tau / 6
            vertices.append((x + nx * radius * math.cos(a), -0.127 - radius * math.sin(a),
                             z + nz * radius * math.cos(a)))
    segments = len(path) if closed else len(path) - 1
    for n in range(segments):
        following = (n + 1) % len(path)
        for k in range(6):
            k2 = (k + 1) % 6
            faces.append((n * 6 + k, following * 6 + k, following * 6 + k2, n * 6 + k2))
    if not closed:
        faces.extend((tuple(reversed(range(6))), tuple((len(path) - 1) * 6 + k for k in range(6))))
    make_mesh(f"Mouth_{index}", vertices, faces, lips[index], x_offset)


def make_ears(index, x_offset):
    # Horizontal width, height and tip angle vary; both ears share one skin mesh.
    styles = ((0.025, 0.040, 0.00), (0.021, 0.055, 0.00),
              (0.031, 0.035, 0.00), (0.020, 0.047, 0.018), (0.018, 0.030, 0.00))
    depth, height, point = styles[index]
    vertices, faces = [], []
    sides = 8
    for side in (-1, 1):
        start = len(vertices)
        x = side * 0.119
        for ring_x in (x, x + side * 0.019):
            for n in range(sides):
                a = n * math.tau / sides
                z = height * math.sin(a) + (point if math.sin(a) > 0.8 else 0)
                vertices.append((ring_x, -0.006 + depth * math.cos(a), z))
        faces.append(tuple(start + n for n in reversed(range(sides))))
        faces.append(tuple(start + sides + n for n in range(sides)))
        for n in range(sides):
            nxt = (n + 1) % sides
            faces.append((start + n, start + nxt, start + sides + nxt, start + sides + n))
    make_mesh(f"Ears_{index}", vertices, faces, skins[index], x_offset)


for index in range(5):
    offset = (index - 2) * 0.43 if PREVIEW_ALL else 0
    if PREVIEW_ALL:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=20, radius=0.13, location=(offset, 0, 0))
        head = bpy.context.object
        head.name = f"Head_{index}"
        head.data.materials.append(skins[index])
        for polygon in head.data.polygons:
            polygon.use_smooth = True
    make_eyes(index, offset)
    make_nose(index, offset)
    make_mouth(index, offset)
    make_ears(index, offset)
